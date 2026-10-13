//! Human display names for installed apps and updates, and which source owns an update.
//! Names are presentation only: they never change which package an operation targets.

use super::Catalog;
use crate::ids::SourceKind;
use crate::sources::model::UpgradeCandidate;
use std::collections::HashSet;

/// "CrystalDiskInfo 9.9.2" (version 9.9.2) -> "CrystalDiskInfo". Only an exact trailing version
/// (optionally prefixed with "v") is removed, and the result is never empty.
pub fn strip_version_tail(name: &str, version: &str) -> String {
    let v = version.trim();
    if v.is_empty() {
        return name.to_string();
    }
    for suffix in [format!(" {v}"), format!(" v{v}")] {
        if let Some(head) = name.strip_suffix(&suffix) {
            if !head.trim().is_empty() {
                return head.trim_end().to_string();
            }
        }
    }
    name.to_string()
}

/// Catalog name first, then the Chocolatey package title, then the source name without its
/// version tail.
pub fn display_name(
    source: SourceKind,
    id: &str,
    raw: &str,
    version: &str,
    cat: &Catalog,
    choco_title: Option<&str>,
) -> String {
    let mapped = cat.by_source(source, id);
    if let Some(app) = mapped {
        return app.name.clone();
    }
    if source == SourceKind::Choco {
        if let Some(t) = choco_title.map(str::trim).filter(|t| !t.is_empty()) {
            return t.to_string();
        }
    }
    strip_version_tail(raw, version)
}

/// When both sources offer an update for the same catalog app, Chocolatey owns it (it only lists
/// packages it installed), so winget's duplicate row is dropped.
pub fn owner_dedupe(cands: Vec<UpgradeCandidate>, cat: &Catalog) -> Vec<UpgradeCandidate> {
    let choco_owned: HashSet<&str> = cands
        .iter()
        .filter(|c| c.source == SourceKind::Choco)
        .filter_map(|c| cat.by_choco(&c.id).map(|a| a.key.as_str()))
        .collect();
    cands
        .iter()
        .filter(|c| {
            c.source != SourceKind::Winget
                || cat
                    .by_winget(&c.id)
                    .is_none_or(|a| !choco_owned.contains(a.key.as_str()))
        })
        .cloned()
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind;
    use crate::sources::model::UpgradeCandidate;

    #[test]
    fn strips_only_an_exact_version_tail() {
        assert_eq!(
            strip_version_tail("CrystalDiskInfo 9.9.2", "9.9.2"),
            "CrystalDiskInfo"
        );
        assert_eq!(strip_version_tail("WinRAR v7.23", "7.23"), "WinRAR");
        assert_eq!(
            strip_version_tail("Python 3.13.15 (64-bit)", "3.13.15150.1013"),
            "Python 3.13.15 (64-bit)"
        );
        assert_eq!(strip_version_tail("7-Zip 24.08", "23.01"), "7-Zip 24.08");
        assert_eq!(strip_version_tail("9.9.2", "9.9.2"), "9.9.2", "never empty");
    }

    #[test]
    fn display_name_priority() {
        let cat = Catalog::embedded();
        assert_eq!(
            display_name(
                SourceKind::Winget,
                "CrystalDewWorld.CrystalDiskInfo",
                "CrystalDiskInfo 9.9.2",
                "9.9.2",
                cat,
                None
            ),
            "CrystalDiskInfo"
        );
        assert_eq!(
            display_name(
                SourceKind::Choco,
                "vscode",
                "vscode",
                "1.0",
                cat,
                Some("Visual Studio Code (choco)")
            ),
            "Visual Studio Code"
        );
        assert_eq!(
            display_name(
                SourceKind::Choco,
                "vcredist140",
                "vcredist140",
                "14.4",
                cat,
                Some("Microsoft Visual C++ Redistributable for Visual Studio 2015-2022")
            ),
            "Microsoft Visual C++ Redistributable for Visual Studio 2015-2022"
        );
        assert_eq!(
            display_name(
                SourceKind::Choco,
                "vcredist140",
                "vcredist140",
                "14.4",
                cat,
                None
            ),
            "vcredist140"
        );
        assert_eq!(
            display_name(
                SourceKind::Winget,
                "Some.Tool",
                "Some Tool 2.0",
                "2.0",
                cat,
                None
            ),
            "Some Tool"
        );
    }

    fn cand(source: SourceKind, id: &str) -> UpgradeCandidate {
        UpgradeCandidate {
            name: id.into(),
            id: id.into(),
            installed_version: "1".into(),
            available_version: "2".into(),
            source,
        }
    }

    #[test]
    fn chocolatey_owns_updates_it_installed() {
        let cat = Catalog::embedded();
        let out = owner_dedupe(
            vec![
                cand(SourceKind::Winget, "Microsoft.VisualStudioCode"),
                cand(SourceKind::Choco, "vscode"),
                cand(SourceKind::Winget, "Some.Tool"),
                cand(SourceKind::Choco, "some-tool"),
            ],
            cat,
        );
        let ids: Vec<_> = out.iter().map(|c| c.id.as_str()).collect();
        assert_eq!(ids, ["vscode", "Some.Tool", "some-tool"]);
    }
}
