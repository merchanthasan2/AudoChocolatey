//! Per-app update rules: "skip this version" (ends by itself when a newer version appears) and
//! "never update". Kept in `rules.json`; a forged file can only hide updates, never run anything.

use crate::catalog::Catalog;
use crate::error::{AppError, AppResult};
use crate::fsutil::{read_json, set_aside, write_atomic, Read};
use crate::ids::{PkgVersion, SourceRef};
use crate::sources::model::UpgradeCandidate;
use crate::versions::compare;
use serde::{Deserialize, Serialize};
use std::cmp::Ordering;
use std::path::PathBuf;
use ts_rs::TS;

pub const SCHEMA_VERSION: u32 = 1;
pub const MAX_RULES: usize = 500;
const MAX_BYTES: u64 = 512 * 1024;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(tag = "kind", rename_all = "camelCase")]
#[ts(export)]
pub enum RuleKind {
    Never,
    SkipVersion { version: PkgVersion },
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

/// Splits updates into shown and hidden. A skip ends (and is removed) once a newer version than
/// the skipped one is offered; the bool says whether `rules` changed.
pub fn apply_rules(
    raw: Vec<UpgradeCandidate>,
    rules: &mut Vec<Rule>,
    cat: &Catalog,
) -> (Vec<UpgradeCandidate>, Vec<HiddenUpdate>, bool) {
    let mut shown = Vec::new();
    let mut hidden = Vec::new();
    let mut expired: Vec<String> = Vec::new();
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
            RuleKind::SkipVersion { version } => {
                match compare(&c.available_version, version.as_str()) {
                    Ordering::Equal => true,
                    Ordering::Greater => {
                        expired.push(key.clone());
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
    let before = rules.len();
    rules.retain(|r| !expired.contains(&r.key));
    let changed = rules.len() != before;
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

    #[cfg(test)]
    pub fn rules(&self) -> &[Rule] {
        &self.rules
    }

    pub fn rules_mut(&mut self) -> &mut Vec<Rule> {
        &mut self.rules
    }

    /// Adds or replaces the rule for that app.
    pub fn set(&mut self, rule: Rule) -> AppResult<()> {
        let exists = self.rules.iter().any(|r| r.key == rule.key);
        if !exists && self.rules.len() >= MAX_RULES {
            return Err(AppError::new(
                "invalid_input",
                "Too many hidden updates. Restore some first.",
            ));
        }
        self.rules.retain(|r| r.key != rule.key);
        self.rules.push(rule);
        self.save()
    }

    pub fn remove(&mut self, key: &str) -> AppResult<()> {
        self.rules.retain(|r| r.key != key);
        self.save()
    }

    pub fn save(&self) -> AppResult<()> {
        if !self.writable {
            return Err(AppError::new(
                "settings",
                "Update rules were saved by a newer version of Quantum Updater and are read-only.",
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
        RuleKind::SkipVersion {
            version: PkgVersion::try_from(v.to_string()).unwrap(),
        }
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
