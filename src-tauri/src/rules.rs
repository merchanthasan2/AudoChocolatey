//! Per-app update rules: "skip this version" (ends by itself when a newer version appears),
//! "never update", and whether shown updates install automatically or ask first. Kept in
//! `rules.json`; a forged file can only hide updates or pick Automatic for an app that already
//! has an update shown, never run anything else.

use crate::catalog::Catalog;
use crate::error::{AppError, AppResult};
use crate::fsutil::{read_json, set_aside, write_atomic, Read};
use crate::ids::{PkgVersion, SourceRef};
use crate::settings::schema::AutoUpdate;
use crate::sources::model::UpgradeCandidate;
use crate::versions::compare;
use serde::{Deserialize, Serialize};
use std::cmp::Ordering;
use std::path::PathBuf;
use ts_rs::TS;

/// 2 added Automatic / Ask first; version-1 builds open a version-2 file read-only.
pub const SCHEMA_VERSION: u32 = 2;
pub const MAX_RULES: usize = 500;
const MAX_BYTES: u64 = 512 * 1024;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(tag = "kind", rename_all = "camelCase")]
#[ts(export)]
pub enum RuleKind {
    Never,
    /// Hides one version. When a newer one arrives the rule becomes `after` (or ends).
    SkipVersion {
        version: PkgVersion,
        #[serde(default, skip_serializing_if = "Option::is_none")]
        #[ts(optional)]
        after: Option<Policy>,
    },
    Auto,
    Ask,
}

/// What happens to an app's shown updates.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum Policy {
    Auto,
    Ask,
}

impl From<Policy> for RuleKind {
    fn from(p: Policy) -> Self {
        match p {
            Policy::Auto => RuleKind::Auto,
            Policy::Ask => RuleKind::Ask,
        }
    }
}

/// Whether an app's shown update installs by itself. The app's own choice wins over the
/// Settings mode; "Never" is never automatic (its updates are hidden anyway).
pub fn effective(mode: AutoUpdate, rule: Option<&RuleKind>) -> Policy {
    let default = match mode {
        AutoUpdate::Notify => Policy::Ask,
        AutoUpdate::Automatic => Policy::Auto,
    };
    match rule {
        Some(RuleKind::Never) => Policy::Ask,
        Some(RuleKind::Auto) => Policy::Auto,
        Some(RuleKind::Ask) => Policy::Ask,
        Some(RuleKind::SkipVersion { after: Some(p), .. }) => *p,
        Some(RuleKind::SkipVersion { after: None, .. }) | None => default,
    }
}

/// A new skip keeps the app's Automatic / Ask first choice for when it ends.
pub fn carry_policy(new: RuleKind, existing: Option<&RuleKind>) -> RuleKind {
    match new {
        RuleKind::SkipVersion {
            version,
            after: None,
        } => RuleKind::SkipVersion {
            version,
            after: match existing {
                Some(RuleKind::Auto) => Some(Policy::Auto),
                Some(RuleKind::Ask) => Some(Policy::Ask),
                Some(RuleKind::SkipVersion { after, .. }) => *after,
                _ => None,
            },
        },
        other => other,
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct Rule {
    pub key: String,
    pub target: SourceRef,
    pub name: String,
    pub kind: RuleKind,
    #[ts(type = "number")]
    pub created_at: u64,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct HiddenUpdate {
    pub candidate: UpgradeCandidate,
    pub rule: Rule,
}

/// Catalog apps share one key across sources, so a rule survives an owner change.
pub fn rule_key(target: &SourceRef, cat: &Catalog) -> String {
    match cat.by_source(target.source(), target.id()) {
        Some(a) => format!("app:{}", a.key),
        None => format!(
            "{}:{}",
            target.source().as_str(),
            target.id().to_ascii_lowercase()
        ),
    }
}

/// Splits updates into shown and hidden. A skip ends once a newer version than the skipped one
/// is offered: it becomes the app's earlier Automatic / Ask first choice, or is removed. The
/// bool says whether `rules` changed.
pub fn apply_rules(
    raw: Vec<UpgradeCandidate>,
    rules: &mut Vec<Rule>,
    cat: &Catalog,
) -> (Vec<UpgradeCandidate>, Vec<HiddenUpdate>, bool) {
    let mut shown = Vec::new();
    let mut hidden = Vec::new();
    let mut expired: Vec<(String, Option<Policy>)> = Vec::new();
    for c in raw {
        let Ok(target) = SourceRef::new(c.source, c.id.clone()) else {
            shown.push(c);
            continue;
        };
        let key = rule_key(&target, cat);
        let Some(rule) = rules.iter().find(|r| r.key == key) else {
            shown.push(c);
            continue;
        };
        let hide = match &rule.kind {
            RuleKind::Never => true,
            RuleKind::Auto | RuleKind::Ask => false,
            RuleKind::SkipVersion { version, after } => {
                match compare(&c.available_version, version.as_str()) {
                    Ordering::Equal => true,
                    Ordering::Greater => {
                        expired.push((key.clone(), *after));
                        false
                    }
                    Ordering::Less => false,
                }
            }
        };
        if hide {
            hidden.push(HiddenUpdate {
                candidate: c,
                rule: rule.clone(),
            });
        } else {
            shown.push(c);
        }
    }
    let changed = !expired.is_empty();
    for (key, after) in expired {
        match after {
            Some(p) => {
                for r in rules.iter_mut().filter(|r| r.key == key) {
                    r.kind = p.into();
                }
            }
            None => rules.retain(|r| r.key != key),
        }
    }
    (shown, hidden, changed)
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct File {
    schema_version: u32,
    rules: Vec<Rule>,
}

pub struct RuleStore {
    path: PathBuf,
    rules: Vec<Rule>,
    writable: bool,
}

impl RuleStore {
    pub fn open(path: PathBuf) -> Self {
        let (rules, writable) = match read_json(&path, MAX_BYTES) {
            Read::Missing => (Vec::new(), true),
            Read::Unreadable => (Vec::new(), false),
            Read::Corrupt => (Vec::new(), set_aside(&path)),
            Read::Json(v) => {
                let version = v.get("schemaVersion").and_then(|v| v.as_u64()).unwrap_or(0);
                if version > u64::from(SCHEMA_VERSION) {
                    (Vec::new(), false)
                } else {
                    match serde_json::from_value::<File>(v) {
                        Ok(f) => {
                            let mut r = f.rules;
                            r.truncate(MAX_RULES);
                            (r, true)
                        }
                        Err(_) => (Vec::new(), set_aside(&path)),
                    }
                }
            }
        };
        Self {
            path,
            rules,
            writable,
        }
    }

    pub fn rules(&self) -> &[Rule] {
        &self.rules
    }

    pub fn rules_mut(&mut self) -> &mut Vec<Rule> {
        &mut self.rules
    }

    /// Adds or replaces the rule for that app. A new skip keeps the app's Automatic / Ask first
    /// choice for when the skip ends.
    pub fn set(&mut self, rule: Rule) -> AppResult<()> {
        self.put(rule)?;
        self.save()
    }

    /// Several rules, saved once; all or nothing.
    pub fn set_many(&mut self, rules: Vec<Rule>) -> AppResult<()> {
        let before = self.rules.clone();
        for r in rules {
            if let Err(e) = self.put(r) {
                self.rules = before;
                return Err(e);
            }
        }
        if let Err(e) = self.save() {
            self.rules = before;
            return Err(e);
        }
        Ok(())
    }

    fn put(&mut self, mut rule: Rule) -> AppResult<()> {
        let existing = self.rules.iter().find(|r| r.key == rule.key);
        if existing.is_none() && self.rules.len() >= MAX_RULES {
            return Err(AppError::new(
                "invalid_input",
                "Too many app choices saved. Reset some apps first.",
            ));
        }
        rule.kind = carry_policy(rule.kind, existing.map(|r| &r.kind));
        self.rules.retain(|r| r.key != rule.key);
        self.rules.push(rule);
        Ok(())
    }

    /// "Show again": a skip goes back to the app's earlier Automatic / Ask first choice; any
    /// other rule is removed, so the app follows Settings again.
    pub fn remove(&mut self, key: &str) -> AppResult<()> {
        let mut keep = false;
        for r in self.rules.iter_mut().filter(|r| r.key == key) {
            if let RuleKind::SkipVersion { after: Some(p), .. } = r.kind {
                r.kind = p.into();
                keep = true;
            }
        }
        if !keep {
            self.rules.retain(|r| r.key != key);
        }
        self.save()
    }

    pub fn save(&self) -> AppResult<()> {
        if !self.writable {
            return Err(AppError::new(
                "settings",
                "Your app choices were saved by a newer version of Quantum Updater and are read-only.",
            ));
        }
        let json = serde_json::to_vec_pretty(&File {
            schema_version: SCHEMA_VERSION,
            rules: self.rules.clone(),
        })
        .expect("rules serialize");
        write_atomic(&self.path, &json)
            .map_err(|e| AppError::new("settings", format!("could not save update rules: {e}")))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::{PkgVersion, SourceKind, SourceRef};

    fn cand(source: SourceKind, id: &str, avail: &str) -> UpgradeCandidate {
        UpgradeCandidate {
            name: id.into(),
            id: id.into(),
            installed_version: "1.0".into(),
            available_version: avail.into(),
            source,
        }
    }

    fn rule(source: SourceKind, id: &str, kind: RuleKind) -> Rule {
        let target = SourceRef::new(source, id).unwrap();
        Rule {
            key: rule_key(&target, Catalog::embedded()),
            target,
            name: id.into(),
            kind,
            created_at: 1,
        }
    }

    fn skip(v: &str) -> RuleKind {
        skip_then(v, None)
    }

    fn skip_then(v: &str, after: Option<Policy>) -> RuleKind {
        RuleKind::SkipVersion {
            version: PkgVersion::try_from(v.to_string()).unwrap(),
            after,
        }
    }

    #[test]
    fn an_expired_skip_falls_back_to_the_apps_earlier_choice() {
        let cat = Catalog::embedded();
        let mut rules = vec![
            rule(
                SourceKind::Winget,
                "A.A",
                skip_then("2.0", Some(Policy::Auto)),
            ),
            rule(
                SourceKind::Winget,
                "B.B",
                skip_then("2.0", Some(Policy::Ask)),
            ),
        ];
        let (shown, hidden, changed) = apply_rules(
            vec![
                cand(SourceKind::Winget, "A.A", "2.1"),
                cand(SourceKind::Winget, "B.B", "2.0"),
            ],
            &mut rules,
            cat,
        );
        assert!(changed);
        assert_eq!(shown.len(), 1);
        assert_eq!(hidden.len(), 1);
        assert_eq!(rules[0].kind, RuleKind::Auto, "A.A's skip ended");
        assert_eq!(rules[1].kind, skip_then("2.0", Some(Policy::Ask)));
    }

    #[test]
    fn automatic_and_ask_first_never_hide_anything() {
        let cat = Catalog::embedded();
        let mut rules = vec![
            rule(SourceKind::Winget, "A.A", RuleKind::Auto),
            rule(SourceKind::Winget, "B.B", RuleKind::Ask),
        ];
        let (shown, hidden, changed) = apply_rules(
            vec![
                cand(SourceKind::Winget, "A.A", "2.0"),
                cand(SourceKind::Winget, "B.B", "2.0"),
            ],
            &mut rules,
            cat,
        );
        assert!(shown.len() == 2 && hidden.is_empty() && !changed);
    }

    #[test]
    fn effective_choice_per_app() {
        use AutoUpdate::{Automatic, Notify};
        let cases: &[(AutoUpdate, Option<RuleKind>, Policy)] = &[
            (Notify, None, Policy::Ask),
            (Automatic, None, Policy::Auto),
            (Notify, Some(RuleKind::Auto), Policy::Auto),
            (Automatic, Some(RuleKind::Ask), Policy::Ask),
            (Automatic, Some(RuleKind::Never), Policy::Ask),
            (Automatic, Some(skip("2.0")), Policy::Auto),
            (Notify, Some(skip("2.0")), Policy::Ask),
            (
                Notify,
                Some(skip_then("2.0", Some(Policy::Auto))),
                Policy::Auto,
            ),
            (
                Automatic,
                Some(skip_then("2.0", Some(Policy::Ask))),
                Policy::Ask,
            ),
        ];
        for (mode, rule, want) in cases {
            assert_eq!(effective(*mode, rule.as_ref()), *want, "{mode:?} {rule:?}");
        }
    }

    #[test]
    fn a_new_skip_keeps_the_apps_choice_and_show_again_restores_it() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("rules.json");
        let mut s = RuleStore::open(p.clone());
        s.set(rule(SourceKind::Winget, "A.A", RuleKind::Auto))
            .unwrap();
        s.set(rule(SourceKind::Winget, "A.A", skip("2.0"))).unwrap();
        assert_eq!(s.rules()[0].kind, skip_then("2.0", Some(Policy::Auto)));
        s.remove("winget:a.a").unwrap();
        assert_eq!(
            s.rules()[0].kind,
            RuleKind::Auto,
            "show again keeps Automatic"
        );
        s.remove("winget:a.a").unwrap();
        assert!(s.rules().is_empty(), "then it follows Settings again");

        // Never, then skip: nothing to carry.
        s.set(rule(SourceKind::Winget, "B.B", RuleKind::Never))
            .unwrap();
        s.set(rule(SourceKind::Winget, "B.B", skip("3.0"))).unwrap();
        assert_eq!(s.rules()[0].kind, skip("3.0"));
    }

    #[test]
    fn version_1_files_still_load_and_are_saved_as_version_2() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("rules.json");
        std::fs::write(
            &p,
            r#"{"schemaVersion":1,"rules":[{"key":"winget:a.a","target":{"source":"winget","id":"A.A"},"name":"A","kind":{"kind":"skipVersion","version":"2.0"},"createdAt":1},{"key":"winget:b.b","target":{"source":"winget","id":"B.B"},"name":"B","kind":{"kind":"never"},"createdAt":1}]}"#,
        )
        .unwrap();
        let s = RuleStore::open(p.clone());
        assert_eq!(s.rules().len(), 2);
        assert_eq!(s.rules()[0].kind, skip("2.0"));
        s.save().unwrap();
        let v: serde_json::Value =
            serde_json::from_str(&std::fs::read_to_string(&p).unwrap()).unwrap();
        assert_eq!(v["schemaVersion"], 2);
        assert!(v["rules"][0]["kind"].get("after").is_none());
    }

    #[test]
    fn several_rules_are_saved_together_or_not_at_all() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("rules.json");
        let mut s = RuleStore::open(p.clone());
        s.set_many(vec![
            rule(SourceKind::Winget, "A.A", skip("2.0")),
            rule(SourceKind::Winget, "B.B", skip("3.0")),
        ])
        .unwrap();
        assert_eq!(RuleStore::open(p.clone()).rules().len(), 2);
        // Over the cap: nothing changes.
        let many: Vec<Rule> = (0..MAX_RULES)
            .map(|i| rule(SourceKind::Winget, &format!("App.N{i}"), RuleKind::Never))
            .collect();
        assert!(s.set_many(many).is_err());
        assert_eq!(s.rules().len(), 2);
    }

    #[test]
    fn a_newer_file_is_read_only() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("rules.json");
        std::fs::write(&p, r#"{"schemaVersion":3,"rules":[]}"#).unwrap();
        let mut s = RuleStore::open(p.clone());
        assert_eq!(
            s.set(rule(SourceKind::Winget, "A.A", RuleKind::Auto))
                .unwrap_err()
                .code,
            "settings"
        );
        assert_eq!(
            std::fs::read_to_string(&p).unwrap(),
            r#"{"schemaVersion":3,"rules":[]}"#
        );
    }

    #[test]
    fn keys_follow_the_catalog_app_across_sources() {
        let cat = Catalog::embedded();
        let w = SourceRef::new(SourceKind::Winget, "Mozilla.Firefox").unwrap();
        let c = SourceRef::new(SourceKind::Choco, "Firefox").unwrap();
        assert_eq!(rule_key(&w, cat), "app:firefox");
        assert_eq!(rule_key(&c, cat), "app:firefox");
        let other = SourceRef::new(SourceKind::Winget, "Contoso.Widget").unwrap();
        assert_eq!(rule_key(&other, cat), "winget:contoso.widget");
    }

    #[test]
    fn skip_hides_only_that_version_and_ends_when_a_newer_one_arrives() {
        let cat = Catalog::embedded();
        let mut rules = vec![rule(SourceKind::Winget, "A.A", skip("2.0"))];
        let (shown, hidden, changed) = apply_rules(
            vec![cand(SourceKind::Winget, "A.A", "2.0")],
            &mut rules,
            cat,
        );
        assert!(shown.is_empty() && hidden.len() == 1 && !changed);

        // A source going back to an older version shows it, but the skip stays.
        let (shown, hidden, changed) = apply_rules(
            vec![cand(SourceKind::Winget, "A.A", "1.9")],
            &mut rules,
            cat,
        );
        assert!(shown.len() == 1 && hidden.is_empty() && !changed);
        assert_eq!(rules.len(), 1);

        let (shown, hidden, changed) = apply_rules(
            vec![cand(SourceKind::Winget, "A.A", "2.1")],
            &mut rules,
            cat,
        );
        assert!(shown.len() == 1 && hidden.is_empty() && changed);
        assert!(rules.is_empty(), "the skip expired");
    }

    #[test]
    fn never_hides_every_version_and_matches_the_other_source() {
        let cat = Catalog::embedded();
        let mut rules = vec![rule(SourceKind::Winget, "Mozilla.Firefox", RuleKind::Never)];
        let (shown, hidden, _) = apply_rules(
            vec![
                cand(SourceKind::Choco, "Firefox", "200.0"),
                cand(SourceKind::Winget, "B.B", "3.0"),
            ],
            &mut rules,
            cat,
        );
        assert_eq!(shown.len(), 1);
        assert_eq!(shown[0].id, "B.B");
        assert_eq!(hidden[0].candidate.id, "Firefox");
        assert_eq!(rules.len(), 1);
    }

    #[test]
    fn store_saves_loads_replaces_and_caps() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("rules.json");
        let mut s = RuleStore::open(p.clone());
        s.set(rule(SourceKind::Winget, "A.A", RuleKind::Never))
            .unwrap();
        s.set(rule(SourceKind::Winget, "A.A", skip("2.0"))).unwrap();
        assert_eq!(s.rules().len(), 1, "same app replaces its rule");
        let back = RuleStore::open(p.clone());
        assert_eq!(back.rules(), s.rules());
        s.remove("winget:a.a").unwrap();
        assert!(RuleStore::open(p.clone()).rules().is_empty());

        for i in 0..MAX_RULES {
            s.rules.push(rule(
                SourceKind::Winget,
                &format!("X.App{i}"),
                RuleKind::Never,
            ));
        }
        assert_eq!(
            s.set(rule(SourceKind::Winget, "Y.Y", RuleKind::Never))
                .unwrap_err()
                .code,
            "invalid_input"
        );
    }

    #[test]
    fn a_tampered_file_is_set_aside() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("rules.json");
        std::fs::write(
            &p,
            r#"{"schemaVersion":1,"rules":[{"key":"x","target":{"source":"winget","id":"-x --evil"},"name":"x","kind":{"kind":"never"},"createdAt":1}]}"#,
        )
        .unwrap();
        assert!(RuleStore::open(p.clone()).rules().is_empty());
        assert!(dir.path().join("rules.json.bad").exists());
    }
}
