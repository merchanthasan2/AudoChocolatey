//! Linking "Installed directly" apps (Programs-list entries no source manages) to a catalog app,
//! so they can be kept up to date. A wrong link would run a different program's installer as
//! administrator, so a link needs everything to agree (ADR 031):
//! - a machine-wide entry (per-user entries can be written without admin rights);
//! - the catalog's publisher, exactly;
//! - an architecture the catalog lists;
//! - the catalog's registry key, or its anchored name pattern;
//! - exactly one catalog app for the entry and exactly one entry for the app;
//! - no copy of the app already managed by a source, and its source switched on.

use super::{Catalog, CatalogApp, LocalMatch};
use crate::ids::{SourceKind, SourceRef};
use std::collections::{HashMap, HashSet};

/// A Programs-list entry as Windows has it (machine-wide, read from HKLM).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LocalEntry {
    /// The registry key name (the last part of winget's `ARP\Machine\<arch>\<key>` id).
    pub key: String,
    pub x64: bool,
    pub display_name: String,
    pub publisher: Option<String>,
    pub version: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LocalLink {
    pub app_key: String,
    pub target: SourceRef,
}

/// Whether one catalog rule matches one entry.
pub fn rule_matches(rule: &LocalMatch, e: &LocalEntry) -> bool {
    let publisher_ok = e
        .publisher
        .as_deref()
        .is_some_and(|p| p.trim().eq_ignore_ascii_case(rule.publisher.trim()));
    let arch = if e.x64 { "x64" } else { "x86" };
    let arch_ok = rule.arch.iter().any(|a| a == arch);
    let key_ok = rule.keys.iter().any(|k| k.eq_ignore_ascii_case(&e.key));
    let name_ok = rule.name_pattern.as_deref().is_some_and(|p| {
        p.starts_with('^')
            && p.ends_with('$')
            && regex::Regex::new(p).is_ok_and(|r| r.is_match(e.display_name.trim()))
    });
    publisher_ok && arch_ok && (key_ok || name_ok)
}

/// The package a linked app updates through.
pub fn target_of(app: &CatalogApp) -> Option<SourceRef> {
    let rule = app.local_match.as_ref()?;
    let id = match rule.via {
        SourceKind::Winget => app.winget.clone()?,
        SourceKind::Choco => app.choco.clone()?,
        SourceKind::Msstore => return None,
    };
    SourceRef::new(rule.via, id).ok()
}

/// Links for display-only entries, by installed-row id. `managed` holds the catalog keys of
/// apps a source already manages on this PC; `enabled` says whether a source is switched on.
pub fn link_all(
    entries: &[(String, LocalEntry)],
    managed: &HashSet<String>,
    cat: &Catalog,
    enabled: impl Fn(SourceKind) -> bool,
) -> HashMap<String, LocalLink> {
    let mut by_app: HashMap<String, Vec<(String, LocalLink)>> = HashMap::new();
    for (row_id, e) in entries {
        let candidates: Vec<&CatalogApp> = cat
            .apps
            .iter()
            .filter(|a| a.local_match.as_ref().is_some_and(|r| rule_matches(r, e)))
            .collect();
        let [app] = candidates.as_slice() else {
            continue; // none, or more than one: never guess
        };
        if managed.contains(&app.key) {
            continue;
        }
        let Some(target) = target_of(app) else {
            continue;
        };
        if !enabled(target.source()) {
            continue;
        }
        by_app.entry(app.key.clone()).or_default().push((
            row_id.clone(),
            LocalLink {
                app_key: app.key.clone(),
                target,
            },
        ));
    }
    by_app
        .into_values()
        .filter(|v| v.len() == 1)
        .flatten()
        .collect()
}

/// Whether an installed version should be offered `latest`.
pub fn offers(installed: &str, latest: &str, same_major: bool) -> bool {
    let has_digit = |v: &str| v.trim().starts_with(|c: char| c.is_ascii_digit());
    if !has_digit(installed) || !has_digit(latest) {
        return false;
    }
    let major = |v: &str| v.split(['.', '-']).next().unwrap_or("").to_string();
    if same_major && major(installed) != major(latest) {
        return false;
    }
    crate::versions::compare(latest, installed) == std::cmp::Ordering::Greater
}

#[cfg(test)]
mod tests {
    use super::*;

    fn entry(key: &str, name: &str, publisher: &str, x64: bool) -> LocalEntry {
        LocalEntry {
            key: key.into(),
            x64,
            display_name: name.into(),
            publisher: Some(publisher.into()),
            version: Some("1.0".into()),
        }
    }

    fn rule(keys: &[&str], pattern: Option<&str>) -> LocalMatch {
        LocalMatch {
            via: SourceKind::Choco,
            publisher: "Tim Kosse".into(),
            arch: vec!["x64".into(), "x86".into()],
            keys: keys.iter().map(|k| k.to_string()).collect(),
            name_pattern: pattern.map(String::from),
            same_major: false,
        }
    }

    #[test]
    fn each_rule_kind() {
        let e = entry("FileZilla Client", "FileZilla 3.68.1", "Tim Kosse", true);
        assert!(
            rule_matches(&rule(&["FileZilla Client"], None), &e),
            "key only"
        );
        assert!(
            rule_matches(&rule(&[], Some(r"^FileZilla \d+(\.\d+){1,3}$")), &e),
            "pattern only"
        );
        assert!(rule_matches(
            &rule(&["FileZilla Client"], Some(r"^FileZilla \d+(\.\d+){1,3}$")),
            &e
        ));
    }

    #[test]
    fn near_misses_and_guards() {
        let r = rule(&[], Some(r"^FileZilla \d+(\.\d+){1,3}$"));
        for e in [
            entry("x", "FileZilla Server 1.8.2", "Tim Kosse", true),
            entry("x", "FileZilla Pro 3.69", "Tim Kosse", true),
            entry("x", "FileZilla 3.68.1", "Someone Else", true),
            entry("x", "FileZilla 3.68.1 evil", "Tim Kosse", true),
        ] {
            assert!(!rule_matches(&r, &e), "{e:?}");
        }
        let no_pub = LocalEntry {
            publisher: None,
            ..entry("FileZilla Client", "FileZilla 3.68.1", "", true)
        };
        assert!(!rule_matches(&rule(&["FileZilla Client"], None), &no_pub));
        let x86_only = LocalMatch {
            arch: vec!["x86".into()],
            ..rule(&["FileZilla Client"], None)
        };
        assert!(!rule_matches(
            &x86_only,
            &entry("FileZilla Client", "FileZilla 3", "Tim Kosse", true)
        ));
        // An unanchored pattern is never used.
        assert!(!rule_matches(
            &rule(&[], Some("FileZilla")),
            &entry("x", "FileZilla 3.68.1", "Tim Kosse", true)
        ));
    }

    #[test]
    fn catalog_links_filezilla_and_winscp_only_when_certain() {
        let cat = Catalog::embedded();
        let fz = (
            r"ARP\Machine\X64\FileZilla Client".to_string(),
            entry("FileZilla Client", "FileZilla 3.68.1", "Tim Kosse", true),
        );
        let ws = (
            r"ARP\Machine\X86\winscp3_is1".to_string(),
            entry("winscp3_is1", "WinSCP 6.3.5", "Martin Prikryl", false),
        );
        let all_on = |_| true;
        let links = link_all(&[fz.clone(), ws.clone()], &HashSet::new(), cat, all_on);
        assert_eq!(links[&fz.0].target.id(), "filezilla");
        assert_eq!(links[&fz.0].target.source(), SourceKind::Choco);
        assert_eq!(links[&ws.0].target.source(), SourceKind::Winget);

        // A managed twin on this PC: no link.
        let managed: HashSet<String> = ["filezilla".to_string()].into();
        assert!(!link_all(std::slice::from_ref(&fz), &managed, cat, all_on).contains_key(&fz.0));
        // The source is switched off: no link.
        let no_choco = |s| s != SourceKind::Choco;
        assert!(link_all(std::slice::from_ref(&fz), &HashSet::new(), cat, no_choco).is_empty());
        // Two entries for one app: neither links.
        let fz2 = (
            r"ARP\Machine\X86\FileZilla Client".to_string(),
            entry("FileZilla Client", "FileZilla 3.60.0", "Tim Kosse", false),
        );
        assert!(link_all(&[fz, fz2], &HashSet::new(), cat, all_on).is_empty());
    }

    #[test]
    fn versions_offered() {
        assert!(offers("3.68.1", "3.69.0", false));
        assert!(!offers("3.69.0", "3.69.0", false));
        assert!(!offers("Unknown", "3.69.0", false));
        assert!(!offers("24.1", "26.0", true), "sameMajor blocks 24 -> 26");
        assert!(offers("24.1", "24.2", true));
    }

    #[test]
    fn catalog_rules_are_sound() {
        let cat = Catalog::embedded();
        let mut keys = HashSet::new();
        for app in &cat.apps {
            let Some(r) = &app.local_match else {
                continue;
            };
            assert!(!r.publisher.trim().is_empty(), "{}", app.key);
            assert!(
                !r.arch.is_empty() && r.arch.iter().all(|a| a == "x86" || a == "x64"),
                "{}",
                app.key
            );
            assert!(
                !r.keys.is_empty() || r.name_pattern.is_some(),
                "{}",
                app.key
            );
            assert!(
                target_of(app).is_some(),
                "{}: via must name one of its ids",
                app.key
            );
            for k in &r.keys {
                assert!(keys.insert(k.to_ascii_lowercase()), "key {k} shared");
            }
            if let Some(p) = &r.name_pattern {
                assert!(
                    p.starts_with('^') && p.ends_with('$'),
                    "{}: anchor the pattern",
                    app.key
                );
                let re = regex::Regex::new(p).unwrap();
                for other in cat.apps.iter().filter(|o| o.key != app.key) {
                    assert!(
                        !re.is_match(&other.name),
                        "{} pattern matches {}",
                        app.key,
                        other.name
                    );
                }
            }
        }
    }
}
