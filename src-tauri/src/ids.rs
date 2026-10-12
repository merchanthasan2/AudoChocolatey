//! Validated identifiers. Every value that ends up in a package-manager argv passes through one
//! of these types, so validation happens once, at the IPC boundary (serde `try_from`).
//! The leading-alphanumeric rule makes argument injection (`-y`, `--source=x`) impossible.

use crate::error::{AppError, AppResult};
use regex::Regex;
use serde::{Deserialize, Serialize};
use std::sync::LazyLock;
use ts_rs::TS;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize, TS)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum SourceKind {
    Winget,
    Choco,
}

static WINGET_ID: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"^[A-Za-z0-9][A-Za-z0-9.+_\-]{0,127}$").unwrap());
static CHOCO_ID: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"^[A-Za-z0-9][A-Za-z0-9._\-]{0,99}$").unwrap());
static VERSION: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"^[A-Za-z0-9][A-Za-z0-9.+_\-]{0,63}$").unwrap());

macro_rules! validated_string {
    ($name:ident, $re:ident, $what:literal) => {
        #[derive(Debug, Clone, PartialEq, Eq, Hash, Serialize, Deserialize, TS)]
        #[serde(try_from = "String", into = "String")]
        #[ts(type = "string")]
        pub struct $name(String);

        impl $name {
            pub fn as_str(&self) -> &str {
                &self.0
            }
        }

        impl TryFrom<String> for $name {
            type Error = AppError;
            fn try_from(s: String) -> AppResult<Self> {
                if $re.is_match(&s) {
                    Ok(Self(s))
                } else {
                    Err(AppError::new(
                        "invalid_input",
                        format!("invalid {}: {:?}", $what, s),
                    ))
                }
            }
        }

        impl From<$name> for String {
            fn from(v: $name) -> String {
                v.0
            }
        }

        impl std::fmt::Display for $name {
            fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                f.write_str(&self.0)
            }
        }
    };
}

validated_string!(WingetId, WINGET_ID, "winget package id");
validated_string!(ChocoId, CHOCO_ID, "Chocolatey package id");
validated_string!(PkgVersion, VERSION, "package version");

/// A package in a specific source; the id is validated against that source's rules.
#[derive(Debug, Clone, PartialEq, Eq, Hash, Serialize, Deserialize, TS)]
#[serde(try_from = "RawSourceRef")]
#[ts(export)]
pub struct SourceRef {
    source: SourceKind,
    id: String,
}

#[derive(Deserialize)]
struct RawSourceRef {
    source: SourceKind,
    id: String,
}

impl SourceRef {
    pub fn new(source: SourceKind, id: impl Into<String>) -> AppResult<Self> {
        let id = id.into();
        match source {
            SourceKind::Winget => WingetId::try_from(id.clone()).map(|_| ()),
            SourceKind::Choco => ChocoId::try_from(id.clone()).map(|_| ()),
        }?;
        Ok(Self { source, id })
    }

    pub fn source(&self) -> SourceKind {
        self.source
    }

    pub fn id(&self) -> &str {
        &self.id
    }
}

impl TryFrom<RawSourceRef> for SourceRef {
    type Error = AppError;
    fn try_from(r: RawSourceRef) -> AppResult<Self> {
        SourceRef::new(r.source, r.id)
    }
}

/// A user search term: trimmed, control characters removed, 1..=100 chars, no leading '-'.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SearchQuery(String);

impl SearchQuery {
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl TryFrom<String> for SearchQuery {
    type Error = AppError;
    fn try_from(s: String) -> AppResult<Self> {
        let cleaned: String = s.chars().filter(|c| !c.is_control()).collect();
        let q = cleaned.trim();
        let len = q.chars().count();
        if len == 0 || len > 100 {
            return Err(AppError::new(
                "invalid_input",
                "search must be 1 to 100 characters",
            ));
        }
        if q.starts_with('-') {
            return Err(AppError::new(
                "invalid_input",
                "search cannot start with '-'",
            ));
        }
        Ok(Self(q.to_string()))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn winget_id_accepts_real_ids() {
        for id in [
            "Mozilla.Firefox",
            "Notepad++.Notepad++",
            "CrystalDewWorld.CrystalDiskInfo",
            "7zip.7zip",
            "Microsoft.VCRedist.2015+.x64",
        ] {
            assert!(WingetId::try_from(id.to_string()).is_ok(), "{id}");
        }
    }

    #[test]
    fn winget_id_rejects_injection_and_local_ids() {
        let long = "a".repeat(129);
        for id in [
            "",
            "-y",
            "--source=evil",
            "a b",
            "x;calc",
            r"ARP\Machine\X64\Android Studio",
            "Mozilla.Firefox\u{2026}",
            "\u{FF26}irefox",
            long.as_str(),
        ] {
            assert!(WingetId::try_from(id.to_string()).is_err(), "{id}");
        }
    }

    #[test]
    fn choco_id_rules() {
        for id in [
            "vscode",
            "chocolatey-core.extension",
            "visualstudio2022-workload-vctools",
            "KB2919355",
        ] {
            assert!(ChocoId::try_from(id.to_string()).is_ok(), "{id}");
        }
        for id in ["-y", "a+b", "a b"] {
            assert!(ChocoId::try_from(id.to_string()).is_err(), "{id}");
        }
    }

    #[test]
    fn version_rules() {
        for v in ["1.95.2", "0.0.311", "117.14.19", "2.0.0-beta+1"] {
            assert!(PkgVersion::try_from(v.to_string()).is_ok(), "{v}");
        }
        for v in ["-1", "1 2", ""] {
            assert!(PkgVersion::try_from(v.to_string()).is_err(), "{v}");
        }
    }

    #[test]
    fn source_ref_validates_per_source() {
        let ok: SourceRef =
            serde_json::from_str(r#"{"source":"winget","id":"Notepad++.Notepad++"}"#).unwrap();
        assert_eq!(ok.id(), "Notepad++.Notepad++");
        assert_eq!(ok.source(), SourceKind::Winget);
        assert!(serde_json::from_str::<SourceRef>(
            r#"{"source":"choco","id":"Notepad++.Notepad++"}"#
        )
        .is_err());
        assert!(serde_json::from_str::<SourceRef>(r#"{"source":"winget","id":"-x"}"#).is_err());
    }

    #[test]
    fn search_query_normalizes_and_rejects() {
        assert_eq!(
            SearchQuery::try_from("  firefox ".to_string())
                .unwrap()
                .as_str(),
            "firefox"
        );
        assert_eq!(
            SearchQuery::try_from("fire\u{0007}fox".to_string())
                .unwrap()
                .as_str(),
            "firefox"
        );
        assert_eq!(
            SearchQuery::try_from("visual studio".to_string())
                .unwrap()
                .as_str(),
            "visual studio"
        );
        for q in ["", "   ", "-y"] {
            assert!(SearchQuery::try_from(q.to_string()).is_err(), "{q:?}");
        }
        assert!(SearchQuery::try_from("a".repeat(101)).is_err());
    }

    #[test]
    fn newtypes_serialize_as_plain_strings() {
        let id = WingetId::try_from("Mozilla.Firefox".to_string()).unwrap();
        assert_eq!(serde_json::to_string(&id).unwrap(), r#""Mozilla.Firefox""#);
        assert!(serde_json::from_str::<WingetId>(r#""-x""#).is_err());
    }
}
