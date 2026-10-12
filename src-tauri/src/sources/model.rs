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
    /// `None` for rows winget only knows locally (ARP/MSIX entries): display-only, not updatable.
    pub source: Option<SourceKind>,
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

#[allow(dead_code)] // wired into the job queue in P3
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum OpKind {
    Install,
    Upgrade,
    Uninstall,
}

#[allow(dead_code)] // wired into the job queue in P3
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct OpRequest {
    pub kind: OpKind,
    pub target: SourceRef,
    pub version: Option<PkgVersion>,
}

#[cfg(test)]
mod tests {
    use super::*;

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
