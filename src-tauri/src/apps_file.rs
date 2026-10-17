//! The app list file: what "Export", "My bundles" and scheduled backups write, and what
//! "Import" reads. It names apps, never versions or commands. Everything read back is
//! re-validated here, and installs only ever use the ids this module accepted.

use crate::catalog::{AppEntry, Catalog};
use crate::ids::{SourceKind, SourceRef};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use ts_rs::TS;

pub const FORMAT: &str = "quantum-updater-apps";
pub const SCHEMA_VERSION: u32 = 1;
pub const MAX_BYTES: u64 = 1024 * 1024;
pub const MAX_APPS: usize = 500;
const MAX_NAME: usize = 120;

/// One app in a file or bundle. `key` is the catalog key when the catalog knows the app.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct FileApp {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    #[ts(optional)]
    pub key: Option<String>,
    pub source: SourceKind,
    pub id: String,
    pub name: String,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct OutFile<'a> {
    format: &'a str,
    schema_version: u32,
    exported_at: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    name: Option<&'a str>,
    apps: &'a [FileApp],
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct InFile {
    format: String,
    schema_version: u32,
    #[serde(default)]
    name: Option<String>,
    apps: Vec<serde_json::Value>,
}

/// A row as it arrives; other fields (a catalog key, a pinned version) are ignored: the key is
/// worked out again from the id.
#[derive(Debug, Deserialize)]
struct InApp {
    source: SourceKind,
    id: String,
    #[serde(default)]
    name: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Parsed {
    pub name: Option<String>,
    pub apps: Vec<FileApp>,
    /// Rows left out: not readable, an id that isn't valid, or a duplicate.
    pub skipped: u32,
}

pub fn clean_name(s: &str) -> String {
    s.chars()
        .filter(|c| !c.is_control())
        .take(MAX_NAME)
        .collect::<String>()
        .trim()
        .to_string()
}

/// Validates one app: the id must be a valid id for its source. A catalog app is stored under
/// its catalog key and name, so a file can't relabel a known app.
pub fn validate(source: SourceKind, id: &str, name: &str, cat: &Catalog) -> Option<FileApp> {
    let target = SourceRef::new(source, id.trim()).ok()?;
    if let Some(app) = cat.by_source(source, target.id()) {
        return Some(FileApp {
            key: Some(app.key.clone()),
            source,
            id: target.id().to_string(),
            name: app.name.clone(),
        });
    }
    let name = clean_name(name);
    Some(FileApp {
        key: None,
        source,
        id: target.id().to_string(),
        name: if name.is_empty() {
            target.id().to_string()
        } else {
            name
        },
    })
}

/// One app per catalog app, or per source and id.
pub fn identity(a: &FileApp) -> String {
    match &a.key {
        Some(k) => format!("app:{k}"),
        None => format!("{}:{}", a.source.as_str(), a.id.to_ascii_lowercase()),
    }
}

pub fn dedupe(apps: Vec<FileApp>) -> (Vec<FileApp>, u32) {
    let mut seen = HashSet::new();
    let mut dropped = 0;
    let out = apps
        .into_iter()
        .filter(|a| {
            let fresh = seen.insert(identity(a));
            if !fresh {
                dropped += 1;
            }
            fresh
        })
        .collect();
    (out, dropped)
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ParseError {
    TooLarge,
    NotOurs,
    Newer,
    TooMany,
}

impl ParseError {
    pub fn message(&self) -> &'static str {
        match self {
            Self::TooLarge => "That file is too large to be an app list.",
            Self::NotOurs => "That file isn't an app list from Quantum Updater.",
            Self::Newer => "That app list was made by a newer version of Quantum Updater. Update Quantum Updater, then try again.",
            Self::TooMany => "That app list has more than 500 apps. Split it into smaller lists.",
        }
    }
}

pub fn parse(bytes: &[u8], cat: &Catalog) -> Result<Parsed, ParseError> {
    if bytes.len() as u64 > MAX_BYTES {
        return Err(ParseError::TooLarge);
    }
    let f: InFile = serde_json::from_slice(bytes).map_err(|_| ParseError::NotOurs)?;
    if f.format != FORMAT {
        return Err(ParseError::NotOurs);
    }
    if f.schema_version > SCHEMA_VERSION {
        return Err(ParseError::Newer);
    }
    if f.schema_version == 0 {
        return Err(ParseError::NotOurs);
    }
    if f.apps.len() > MAX_APPS {
        return Err(ParseError::TooMany);
    }
    let mut skipped = 0u32;
    let mut apps = Vec::new();
    for v in f.apps {
        let row = serde_json::from_value::<InApp>(v)
            .ok()
            .and_then(|r| validate(r.source, &r.id, r.name.as_deref().unwrap_or(""), cat));
        match row {
            Some(a) => apps.push(a),
            None => skipped += 1,
        }
    }
    let (apps, dupes) = dedupe(apps);
    Ok(Parsed {
        name: f.name.map(|n| clean_name(&n)).filter(|n| !n.is_empty()),
        apps,
        skipped: skipped + dupes,
    })
}

pub fn to_json(name: Option<&str>, apps: &[FileApp], exported_at: String) -> Vec<u8> {
    serde_json::to_vec_pretty(&OutFile {
        format: FORMAT,
        schema_version: SCHEMA_VERSION,
        exported_at,
        name,
        apps,
    })
    .expect("app list serializes")
}

/// How the UI shows a saved app: the catalog entry when known, else the source and id as-is.
pub fn entry(a: &FileApp, cat: &Catalog) -> AppEntry {
    if let Some(app) = a.key.as_deref().and_then(|k| cat.by_key(k)) {
        return cat.entry(app);
    }
    let id = Some(a.id.clone());
    AppEntry {
        key: identity(a),
        name: a.name.clone(),
        publisher: None,
        blurb: None,
        category: None,
        winget: (a.source == SourceKind::Winget).then(|| a.id.clone()),
        choco: (a.source == SourceKind::Choco).then(|| a.id.clone()),
        msstore: (a.source == SourceKind::Msstore).then_some(id).flatten(),
        version: None,
        homepage: None,
        best: a.source,
        curated: false,
        icon_key: format!("{}:{}", a.source.as_str(), a.id),
    }
}

/// The install a saved app leads to: a catalog app installs through its best source.
pub fn install_target(a: &FileApp, cat: &Catalog) -> Option<SourceRef> {
    if let Some(app) = a.key.as_deref().and_then(|k| cat.by_key(k)) {
        let e = cat.entry(app);
        let id = match e.best {
            SourceKind::Winget => e.winget,
            SourceKind::Choco => e.choco,
            SourceKind::Msstore => e.msstore,
        }?;
        return SourceRef::new(e.best, id).ok();
    }
    SourceRef::new(a.source, a.id.clone()).ok()
}

/// `2026-10-17T09:30:00Z` for ms since the epoch (UTC).
pub fn iso_utc(ms: u64) -> String {
    let secs = ms / 1000;
    let (y, m, d) = civil_from_days((secs / 86_400) as i64);
    let t = secs % 86_400;
    format!(
        "{y:04}-{m:02}-{d:02}T{:02}:{:02}:{:02}Z",
        t / 3600,
        (t / 60) % 60,
        t % 60
    )
}

/// Days since 1970-01-01 to (year, month, day); Howard Hinnant's algorithm.
fn civil_from_days(z: i64) -> (i64, u32, u32) {
    let z = z + 719_468;
    let era = z.div_euclid(146_097);
    let doe = z.rem_euclid(146_097);
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = (doy - (153 * mp + 2) / 5 + 1) as u32;
    let m = if mp < 10 { mp + 3 } else { mp - 9 } as u32;
    let y = yoe + era * 400 + i64::from(m <= 2);
    (y, m, d)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn file(apps: &str) -> Vec<u8> {
        format!(r#"{{"format":"quantum-updater-apps","schemaVersion":1,"exportedAt":"x","apps":[{apps}]}}"#)
            .into_bytes()
    }

    #[test]
    fn a_good_file_round_trips() {
        let cat = Catalog::embedded();
        let apps = vec![
            validate(SourceKind::Winget, "Mozilla.Firefox", "whatever", cat).unwrap(),
            validate(SourceKind::Winget, "Some.Tool", "Some Tool", cat).unwrap(),
        ];
        assert!(apps[0].key.is_some(), "catalog app keeps its key");
        assert_eq!(apps[0].name, cat.by_winget("Mozilla.Firefox").unwrap().name);
        let bytes = to_json(Some("Work PC"), &apps, iso_utc(0));
        let p = parse(&bytes, cat).unwrap();
        assert_eq!(p.apps, apps);
        assert_eq!(p.name.as_deref(), Some("Work PC"));
        assert_eq!(p.skipped, 0);
    }

    #[test]
    fn wrong_format_newer_schema_and_size_are_refused() {
        let cat = Catalog::embedded();
        assert_eq!(parse(b"not json", cat), Err(ParseError::NotOurs));
        assert_eq!(
            parse(br#"{"format":"other","schemaVersion":1,"apps":[]}"#, cat),
            Err(ParseError::NotOurs)
        );
        assert_eq!(
            parse(
                br#"{"format":"quantum-updater-apps","schemaVersion":2,"apps":[]}"#,
                cat
            ),
            Err(ParseError::Newer)
        );
        let big = vec![b' '; MAX_BYTES as usize + 1];
        assert_eq!(parse(&big, cat), Err(ParseError::TooLarge));
    }

    #[test]
    fn more_than_500_apps_is_refused() {
        let cat = Catalog::embedded();
        let row = r#"{"source":"winget","id":"A.B","name":"A"}"#;
        let rows = vec![row; 501].join(",");
        assert_eq!(parse(&file(&rows), cat), Err(ParseError::TooMany));
        let rows = vec![row; 500].join(",");
        assert!(parse(&file(&rows), cat).is_ok());
    }

    #[test]
    fn bad_ids_and_duplicates_are_left_out_and_versions_ignored() {
        let cat = Catalog::embedded();
        let rows = [
            r#"{"source":"winget","id":"Good.App","name":"Good","version":"1.0"}"#,
            r#"{"source":"winget","id":"good.app","name":"Dupe"}"#,
            r#"{"source":"winget","id":"Bad\" --override x","name":"Bad"}"#,
            r#"{"source":"choco","id":"a b","name":"Space"}"#,
            r#"{"source":"winget","id":"--source evil","name":"Flag"}"#,
            r#"{"source":"nope","id":"X.Y","name":"Unknown source"}"#,
            r#"{"id":"No.Source"}"#,
            r#"{"source":"winget","id":"Mozilla.Firefox","name":"Fake name"}"#,
            r#"{"source":"choco","id":"firefox","name":"Same app again"}"#,
        ]
        .join(",");
        let p = parse(&file(&rows), cat).unwrap();
        let ids: Vec<&str> = p.apps.iter().map(|a| a.id.as_str()).collect();
        assert_eq!(ids, vec!["Good.App", "Mozilla.Firefox"]);
        assert_ne!(
            p.apps[1].name, "Fake name",
            "catalog names can't be relabelled"
        );
        assert_eq!(p.skipped, 7);
    }

    #[test]
    fn names_are_cleaned() {
        let cat = Catalog::embedded();
        let a = validate(SourceKind::Winget, "X.Y", "  Bad\u{7}Name\n  ", cat).unwrap();
        assert_eq!(a.name, "BadName");
        let a = validate(SourceKind::Winget, "X.Y", "", cat).unwrap();
        assert_eq!(a.name, "X.Y");
        let long = "n".repeat(500);
        assert_eq!(
            validate(SourceKind::Winget, "X.Y", &long, cat)
                .unwrap()
                .name
                .len(),
            120
        );
    }

    #[test]
    fn catalog_apps_install_through_their_best_source() {
        let cat = Catalog::embedded();
        let a = validate(SourceKind::Choco, "firefox", "", cat).unwrap();
        let t = install_target(&a, cat).unwrap();
        assert_eq!(t.source(), SourceKind::Winget);
        let b = validate(SourceKind::Choco, "some-tool", "Some tool", cat).unwrap();
        let t = install_target(&b, cat).unwrap();
        assert_eq!((t.source(), t.id()), (SourceKind::Choco, "some-tool"));
        assert!(!entry(&b, cat).curated);
        assert_eq!(entry(&b, cat).choco.as_deref(), Some("some-tool"));
    }

    #[test]
    fn utc_timestamps() {
        assert_eq!(iso_utc(0), "1970-01-01T00:00:00Z");
        assert_eq!(iso_utc(1_792_255_168_083), "2026-10-17T16:39:28Z");
        assert_eq!(iso_utc(951_782_400_000), "2000-02-29T00:00:00Z");
    }
}
