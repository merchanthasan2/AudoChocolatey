//! Data returned by package sources. These cross the IPC boundary (camelCase, ts-rs exported).

use crate::ids::{PkgVersion, SourceKind, SourceRef};
use serde::{Deserialize, Serialize};
use ts_rs::TS;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct InstalledPackage {
    pub name: String,
    pub id: String,
    pub version: String,
    pub available: Option<String>,
    /// `None` for rows winget only knows locally (ARP/MSIX entries). Not updated here
    /// unless linked. `removable` is set when that app's own uninstaller can be opened.
    pub source: Option<SourceKind>,
    /// Where it comes from, as people see it.
    pub label: crate::sources::label::SourceLabel,
    /// A runtime, codec or other Windows component (Installed hides these by default).
    pub component: bool,
    /// Installed directly, but linked to a catalog app this app can update.
    pub linked: bool,
    /// Its own uninstaller can be opened. Catalog apps stay on the normal remove path.
    pub removable: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct UpgradeCandidate {
    pub name: String,
    pub id: String,
    pub installed_version: String,
    pub available_version: String,
    pub source: SourceKind,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct SourcePackage {
    pub name: String,
    pub id: String,
    pub version: Option<String>,
    pub source: SourceKind,
    pub matched_on: Option<String>,
    pub description: Option<String>,
    pub homepage: Option<String>,
    pub icon_url: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(tag = "state", rename_all = "lowercase")]
#[ts(export)]
pub enum SourceState {
    Ready { version: String },
    Missing,
    Broken { reason: String },
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[ts(export)]
pub struct SourceStatus {
    pub kind: SourceKind,
    #[serde(flatten)]
    #[ts(flatten)]
    pub state: SourceState,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum OpKind {
    Install,
    Upgrade,
    Uninstall,
    /// Back to an earlier version (`OpRequest.version`). Only Rust's `rollback` command makes
    /// one; the page can't.
    Rollback,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct OpRequest {
    pub kind: OpKind,
    pub target: SourceRef,
    pub version: Option<PkgVersion>,
    /// Normal (silent), with the installer's own window, or reinstall. Older saved jobs load
    /// as Normal.
    #[serde(default)]
    #[ts(optional, as = "Option<OpMode>")]
    pub mode: OpMode,
}

#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum OpMode {
    #[default]
    Normal,
    /// Shows the installer's own window, for apps that need choices made.
    Interactive,
    /// Installs the same version again over the current one.
    Reinstall,
}

/// Which modes each kind of change may use, per source. Rust is the only authority.
pub fn mode_allowed(kind: OpKind, mode: OpMode, source: SourceKind) -> bool {
    let tool = matches!(source, SourceKind::Winget | SourceKind::Choco);
    match (kind, mode) {
        (_, OpMode::Normal) => true,
        (OpKind::Install | OpKind::Upgrade, OpMode::Interactive) => tool,
        (OpKind::Upgrade, OpMode::Reinstall) => tool,
        _ => false,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn mode_table() {
        use OpKind::*;
        use OpMode::*;
        use SourceKind::*;
        for s in [Winget, Choco, Msstore] {
            for k in [Install, Upgrade, Uninstall, Rollback] {
                assert!(mode_allowed(k, Normal, s));
            }
        }
        for s in [Winget, Choco] {
            assert!(mode_allowed(Install, Interactive, s));
            assert!(mode_allowed(Upgrade, Interactive, s));
            assert!(mode_allowed(Upgrade, Reinstall, s));
            assert!(!mode_allowed(Install, Reinstall, s));
            assert!(!mode_allowed(Uninstall, Interactive, s));
            assert!(!mode_allowed(Uninstall, Reinstall, s));
            assert!(!mode_allowed(Rollback, Reinstall, s));
        }
        assert!(!mode_allowed(Install, Interactive, Msstore));
        assert!(!mode_allowed(Upgrade, Reinstall, Msstore));
    }

    #[test]
    fn saved_jobs_without_a_mode_load_as_normal() {
        let op: OpRequest = serde_json::from_str(
            r#"{"kind":"upgrade","target":{"source":"winget","id":"A.B"},"version":null}"#,
        )
        .unwrap();
        assert_eq!(op.mode, OpMode::Normal);
    }

    #[test]
    fn source_status_serializes_flat_and_tagged() {
        let s = SourceStatus {
            kind: SourceKind::Winget,
            state: SourceState::Ready {
                version: "1.29.380".into(),
            },
        };
        assert_eq!(
            serde_json::to_string(&s).unwrap(),
            r#"{"kind":"winget","state":"ready","version":"1.29.380"}"#
        );
        let m = SourceStatus {
            kind: SourceKind::Choco,
            state: SourceState::Missing,
        };
        assert_eq!(
            serde_json::to_string(&m).unwrap(),
            r#"{"kind":"choco","state":"missing"}"#
        );
    }

    #[test]
    fn op_request_rejects_invalid_target() {
        let ok: OpRequest = serde_json::from_str(
            r#"{"kind":"install","target":{"source":"winget","id":"Mozilla.Firefox"},"version":null}"#,
        )
        .unwrap();
        assert_eq!(ok.kind, OpKind::Install);
        assert!(serde_json::from_str::<OpRequest>(
            r#"{"kind":"install","target":{"source":"winget","id":"-x"},"version":null}"#
        )
        .is_err());
    }
}
