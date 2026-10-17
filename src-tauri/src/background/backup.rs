//! Scheduled backups of the app list to a folder the person picked. Only files this module
//! names (`quantum-updater-apps-YYYY-MM-DD.json`) are ever written or removed there.

use crate::apps_file::{to_json, FileApp};
use crate::settings::schema::BackupEvery;
use std::path::Path;

const DAY_MS: u64 = 24 * 3_600_000;
const PREFIX: &str = "quantum-updater-apps-";

pub fn due(now: u64, last: Option<u64>, every: BackupEvery) -> bool {
    let gap = match every {
        BackupEvery::Daily => DAY_MS,
        BackupEvery::Weekly => 7 * DAY_MS,
    };
    match last {
        None => true,
        // A last time in the future means the clock went back: start over.
        Some(t) if t > now => true,
        Some(t) => now - t >= gap,
    }
}

pub fn file_name((y, m, d): (u16, u16, u16)) -> String {
    format!("{PREFIX}{y:04}-{m:02}-{d:02}.json")
}

fn is_ours(name: &str) -> bool {
    let Some(rest) = name
        .strip_prefix(PREFIX)
        .and_then(|r| r.strip_suffix(".json"))
    else {
        return false;
    };
    let b = rest.as_bytes();
    b.len() == 10
        && b[4] == b'-'
        && b[7] == b'-'
        && b.iter()
            .enumerate()
            .all(|(i, c)| i == 4 || i == 7 || c.is_ascii_digit())
}

/// Our backups beyond the newest `keep` (the input order doesn't matter).
pub fn to_prune(names: &[String], keep: u32) -> Vec<String> {
    let mut ours: Vec<&String> = names.iter().filter(|n| is_ours(n)).collect();
    ours.sort_unstable_by(|a, b| b.cmp(a));
    ours.into_iter().skip(keep as usize).cloned().collect()
}

/// Writes today's backup and removes older ones beyond `keep`. Returns the file name.
pub fn write(
    folder: &Path,
    apps: &[FileApp],
    date: (u16, u16, u16),
    exported_at: String,
    keep: u32,
) -> std::io::Result<String> {
    if !folder.is_dir() {
        return Err(std::io::Error::new(
            std::io::ErrorKind::NotFound,
            "The backup folder isn't there any more.",
        ));
    }
    let name = file_name(date);
    crate::fsutil::write_atomic(
        &folder.join(&name),
        &to_json(Some("Backup"), apps, exported_at),
    )?;
    let names: Vec<String> = std::fs::read_dir(folder)?
        .filter_map(|e| e.ok())
        // Plain files only: never follow a link out of the folder.
        .filter(|e| e.file_type().is_ok_and(|t| t.is_file()))
        .filter_map(|e| e.file_name().into_string().ok())
        .collect();
    for old in to_prune(&names, keep) {
        let _ = std::fs::remove_file(folder.join(old));
    }
    Ok(name)
}

/// Today's local date.
pub fn local_date() -> (u16, u16, u16) {
    use windows_sys::Win32::Foundation::SYSTEMTIME;
    use windows_sys::Win32::System::SystemInformation::GetLocalTime;
    // SAFETY: plain out-parameter struct.
    let mut t: SYSTEMTIME = unsafe { std::mem::zeroed() };
    // SAFETY: `t` is a valid, writable SYSTEMTIME.
    unsafe { GetLocalTime(&mut t) };
    (t.wYear, t.wMonth, t.wDay)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind;

    #[test]
    fn due_daily_weekly_and_clock_changes() {
        let now = 100 * DAY_MS;
        assert!(due(now, None, BackupEvery::Weekly));
        assert!(!due(now, Some(now - DAY_MS + 1), BackupEvery::Daily));
        assert!(due(now, Some(now - DAY_MS), BackupEvery::Daily));
        assert!(!due(now, Some(now - 6 * DAY_MS), BackupEvery::Weekly));
        assert!(due(now, Some(now - 7 * DAY_MS), BackupEvery::Weekly));
        assert!(due(now, Some(now + DAY_MS), BackupEvery::Weekly));
    }

    #[test]
    fn only_our_files_are_pruned_oldest_first() {
        let names: Vec<String> = [
            "quantum-updater-apps-2026-10-01.json",
            "quantum-updater-apps-2026-10-08.json",
            "quantum-updater-apps-2026-10-15.json",
            "quantum-updater-apps-2026-10-15.json.tmp",
            "quantum-updater-apps-latest.json",
            "quantum-updater-apps-2026-1-01.json",
            "my notes.txt",
        ]
        .iter()
        .map(|s| s.to_string())
        .collect();
        assert_eq!(
            to_prune(&names, 2),
            vec!["quantum-updater-apps-2026-10-01.json".to_string()]
        );
        assert!(to_prune(&names, 5).is_empty());
    }

    #[test]
    fn writes_and_keeps_the_newest() {
        let dir = tempfile::tempdir().unwrap();
        std::fs::write(dir.path().join("keep me.txt"), "x").unwrap();
        let apps = vec![FileApp {
            key: None,
            source: SourceKind::Winget,
            id: "A.B".into(),
            name: "A".into(),
        }];
        for d in 1..=4 {
            write(dir.path(), &apps, (2026, 10, d), "t".into(), 2).unwrap();
        }
        let mut names: Vec<String> = std::fs::read_dir(dir.path())
            .unwrap()
            .map(|e| e.unwrap().file_name().into_string().unwrap())
            .collect();
        names.sort();
        assert_eq!(
            names,
            vec![
                "keep me.txt",
                "quantum-updater-apps-2026-10-03.json",
                "quantum-updater-apps-2026-10-04.json"
            ]
        );
        let back = std::fs::read(dir.path().join("quantum-updater-apps-2026-10-04.json")).unwrap();
        let parsed = crate::apps_file::parse(&back, crate::catalog::Catalog::embedded()).unwrap();
        assert_eq!(parsed.apps, apps);
        assert!(write(
            &dir.path().join("gone"),
            &apps,
            (2026, 10, 5),
            "t".into(),
            2
        )
        .is_err());
    }
}
