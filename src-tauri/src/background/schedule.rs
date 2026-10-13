//! When to check for updates in the background, and which results are new enough to mention.

use crate::fsutil::{read_json, write_atomic, Read};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;
use std::path::Path;

/// The first background check waits this long after start, so sign-in stays quick.
pub const FIRST_CHECK_DELAY_MS: u64 = 2 * 60_000;
const STATE_MAX_BYTES: u64 = 64 * 1024;

pub fn due(now: u64, launched_at: u64, last_check: Option<u64>, interval_hours: u32) -> bool {
    if now < launched_at + FIRST_CHECK_DELAY_MS {
        return false;
    }
    match last_check {
        None => true,
        Some(t) if t > now => true, // the clock went back
        Some(t) => now - t >= u64::from(interval_hours) * 3_600_000,
    }
}

/// Update keys (`<rule key>@<version>`) in `next` that weren't in `prev`.
pub fn diff_new(prev: &BTreeSet<String>, next: &BTreeSet<String>) -> Vec<String> {
    next.difference(prev).cloned().collect()
}

/// `background.json`: when we last checked and what we already told the person about.
/// A forged file can only make a notification appear or not.
#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", default)]
pub struct BackgroundState {
    pub last_check_at: Option<u64>,
    pub notified: BTreeSet<String>,
}

impl BackgroundState {
    pub fn load(path: &Path) -> Self {
        match read_json(path, STATE_MAX_BYTES) {
            Read::Json(v) => serde_json::from_value(v).unwrap_or_default(),
            _ => Self::default(),
        }
    }

    pub fn save(&self, path: &Path) -> std::io::Result<()> {
        write_atomic(path, &serde_json::to_vec(self).expect("state serializes"))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::BTreeSet;

    const MIN: u64 = 60_000;
    const HOUR: u64 = 60 * MIN;

    #[test]
    fn first_check_waits_two_minutes_after_start() {
        let start = 1_000 * HOUR;
        assert!(!due(start + MIN, start, None, 6));
        assert!(due(start + 2 * MIN, start, None, 6));
    }

    #[test]
    fn then_every_interval() {
        let start = 1_000 * HOUR;
        let last = start + 2 * MIN;
        assert!(!due(last + 5 * HOUR, start, Some(last), 6));
        assert!(due(last + 6 * HOUR, start, Some(last), 6));
        // Interval shortened in Settings: due straight away.
        assert!(due(last + 2 * HOUR, start, Some(last), 1));
    }

    #[test]
    fn a_clock_that_jumped_back_counts_as_due() {
        let start = 1_000 * HOUR;
        assert!(due(start + 3 * MIN, start, Some(start + 10 * HOUR), 6));
    }

    #[test]
    fn only_new_updates_are_mentioned() {
        let set = |v: &[&str]| v.iter().map(|s| s.to_string()).collect::<BTreeSet<_>>();
        let prev = set(&["app:firefox@2", "winget:a.a@1"]);
        assert_eq!(
            diff_new(&prev, &set(&["app:firefox@2", "winget:a.a@1"])),
            Vec::<String>::new()
        );
        assert_eq!(
            diff_new(&prev, &set(&["app:firefox@2"])),
            Vec::<String>::new(),
            "removals stay quiet"
        );
        assert_eq!(
            diff_new(&prev, &set(&["app:firefox@3", "winget:a.a@1"])),
            vec!["app:firefox@3"]
        );
    }

    #[test]
    fn state_file_round_trip_and_cap() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("background.json");
        assert_eq!(BackgroundState::load(&p), BackgroundState::default());
        let s = BackgroundState {
            last_check_at: Some(42),
            notified: ["app:firefox@3".to_string()].into(),
        };
        s.save(&p).unwrap();
        assert_eq!(BackgroundState::load(&p), s);
        std::fs::write(&p, vec![b' '; 70 * 1024]).unwrap();
        assert_eq!(BackgroundState::load(&p), BackgroundState::default());
    }
}
