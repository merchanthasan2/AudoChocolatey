//! The queue and its recent history survive a restart (`jobs.json` in the app data folder).
//! The file is user-writable and the app starts elevated at logon, so nothing in it ever runs
//! by itself: unfinished jobs come back as stopped, and every retry needs a click.

use crate::fsutil::{read_json, set_aside, write_atomic, Read};
use crate::jobs::failure::FailureReason;
use crate::jobs::model::{now_ms, Job, JobPhase};
use serde::{Deserialize, Serialize};
use std::path::Path;

pub const SCHEMA_VERSION: u32 = 1;
/// Finished jobs kept (active ones are always kept).
pub const HISTORY: usize = 200;
const MAX_BYTES: u64 = 2 * 1024 * 1024;

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct File {
    schema_version: u32,
    jobs: Vec<Job>,
}

pub struct Loaded {
    pub jobs: Vec<Job>,
    /// False when the file can't be safely replaced (newer app version, unreadable).
    pub writable: bool,
}

/// Jobs that were unfinished when the app stopped end as stopped or interrupted; none restart.
pub fn recover(jobs: Vec<Job>) -> Vec<Job> {
    let cat = crate::catalog::Catalog::embedded();
    jobs.into_iter()
        .map(|mut j| {
            // The file is user-writable: show the real package (never a stored label) and never
            // keep a pinned version, so "Run them now" can't be dressed up as another app.
            j.display_name = cat
                .by_source(j.op.target.source(), j.op.target.id())
                .map(|a| a.name.clone())
                .unwrap_or_else(|| j.op.target.id().to_string());
            j.op.version = None;
            match j.phase {
                JobPhase::Queued => {
                    j.set_phase(JobPhase::Cancelled);
                    j.detail = Some("Not started".into());
                }
                JobPhase::Preparing | JobPhase::Waiting | JobPhase::Downloading => {
                    j.set_phase(JobPhase::Cancelled);
                    j.detail = Some("Stopped when the app closed".into());
                }
                JobPhase::Installing | JobPhase::Verifying => j.fail(FailureReason::Interrupted),
                _ => {}
            }
            if j.ended_at.is_none() {
                j.ended_at = Some(now_ms());
            }
            j.cancellable = false;
            j
        })
        .collect()
}

/// Keeps every active job and the newest `HISTORY` finished ones, in their original order.
pub fn trim(jobs: Vec<Job>) -> Vec<Job> {
    let finished = jobs.iter().filter(|j| j.phase.is_finished()).count();
    let mut drop = finished.saturating_sub(HISTORY);
    jobs.into_iter()
        .filter(|j| {
            if drop > 0 && j.phase.is_finished() {
                drop -= 1;
                false
            } else {
                true
            }
        })
        .collect()
}

/// Highest `jN` number in use, so new ids never collide with restored ones.
pub fn next_id(jobs: &[Job]) -> u64 {
    jobs.iter()
        .filter_map(|j| j.id.strip_prefix('j')?.parse::<u64>().ok())
        .max()
        .unwrap_or(0)
}

pub fn load(path: &Path) -> Loaded {
    let fresh = |writable| Loaded {
        jobs: Vec::new(),
        writable,
    };
    let value = match read_json(path, MAX_BYTES) {
        Read::Missing => return fresh(true),
        Read::Unreadable => return fresh(false),
        Read::Corrupt => return fresh(set_aside(path)),
        Read::Json(v) => v,
    };
    let version = value
        .get("schemaVersion")
        .and_then(|v| v.as_u64())
        .unwrap_or(0);
    if version > u64::from(SCHEMA_VERSION) {
        return fresh(false);
    }
    // Ids are revalidated by SourceRef's deserializer; one bad entry rejects the file.
    match serde_json::from_value::<File>(value) {
        Ok(f) => Loaded {
            jobs: trim(recover(f.jobs)),
            writable: true,
        },
        Err(_) => fresh(set_aside(path)),
    }
}

pub fn save(path: &Path, jobs: &[Job]) -> std::io::Result<()> {
    let file = File {
        schema_version: SCHEMA_VERSION,
        jobs: trim(jobs.to_vec()),
    };
    let json = serde_json::to_vec(&file).expect("jobs serialize");
    write_atomic(path, &json)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::{SourceKind, SourceRef};
    use crate::jobs::failure::FailureReason;
    use crate::jobs::model::{Job, JobPhase};
    use crate::sources::model::{OpKind, OpRequest};

    fn job(n: u64, phase: JobPhase) -> Job {
        let mut j = Job::new(
            format!("j{n}"),
            OpRequest {
                kind: OpKind::Install,
                target: SourceRef::new(SourceKind::Winget, "Mozilla.Firefox").unwrap(),
                version: None,
            },
            "Firefox".into(),
        );
        j.phase = phase;
        j
    }

    #[test]
    fn recovery_never_restarts_anything() {
        let out = recover(vec![
            job(1, JobPhase::Queued),
            job(2, JobPhase::Preparing),
            job(3, JobPhase::Downloading),
            job(4, JobPhase::Installing),
            job(5, JobPhase::Verifying),
            job(6, JobPhase::Succeeded),
            job(7, JobPhase::Failed),
        ]);
        let p: Vec<_> = out.iter().map(|j| j.phase).collect();
        use JobPhase::*;
        assert_eq!(
            p,
            [Cancelled, Cancelled, Cancelled, Failed, Failed, Succeeded, Failed]
        );
        assert_eq!(out[0].detail.as_deref(), Some("Not started"));
        assert_eq!(
            out[1].detail.as_deref(),
            Some("Stopped when the app closed")
        );
        assert_eq!(out[3].reason, Some(FailureReason::Interrupted));
        assert!(out.iter().all(|j| j.phase.is_finished()));
        assert!(out.iter().all(|j| !j.cancellable));
        assert!(out[3].ended_at.is_some());
    }

    #[test]
    fn history_keeps_active_jobs_and_the_newest_finished_ones() {
        let mut jobs: Vec<Job> = (1..=HISTORY as u64 + 5)
            .map(|n| job(n, JobPhase::Succeeded))
            .collect();
        jobs.insert(0, job(9999, JobPhase::Queued));
        let kept = trim(jobs);
        assert_eq!(kept.len(), HISTORY + 1);
        assert_eq!(kept[0].id, "j9999");
        assert_eq!(kept[1].id, "j6", "oldest finished jobs go first");
    }

    #[test]
    fn ids_continue_after_a_restart() {
        assert_eq!(
            next_id(&[job(7, JobPhase::Failed), job(41, JobPhase::Succeeded)]),
            41
        );
        assert_eq!(next_id(&[]), 0);
    }

    #[test]
    fn round_trip_and_corrupt_file() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("jobs.json");
        assert!(load(&p).jobs.is_empty());
        let mut done = job(3, JobPhase::Failed);
        done.reason = Some(FailureReason::HashMismatch);
        done.error = Some("x".into());
        done.cancellable = false;
        done.ended_at = Some(1);
        save(&p, &[done.clone(), job(4, JobPhase::Installing)]).unwrap();
        let back = load(&p).jobs;
        assert_eq!(back[0], done);
        assert_eq!(back[1].reason, Some(FailureReason::Interrupted));

        std::fs::write(&p, "{ nope").unwrap();
        let l = load(&p);
        assert!(l.jobs.is_empty() && l.writable);
        assert!(dir.path().join("jobs.json.bad").exists());
    }

    #[test]
    fn invalid_ids_in_the_file_are_rejected() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("jobs.json");
        save(&p, &[job(1, JobPhase::Succeeded)]).unwrap();
        let text = std::fs::read_to_string(&p)
            .unwrap()
            .replace("Mozilla.Firefox", "-evil --flag");
        std::fs::write(&p, text).unwrap();
        assert!(load(&p).jobs.is_empty());
    }

    #[test]
    fn restored_jobs_show_the_real_package_and_never_pin_a_version() {
        let mut forged = job(1, JobPhase::Queued);
        forged.op.target = SourceRef::new(SourceKind::Winget, "Evil.Tool").unwrap();
        forged.op.version = Some(crate::ids::PkgVersion::try_from("0.1".to_string()).unwrap());
        forged.display_name = "Firefox".into();
        let mut real = job(2, JobPhase::Queued);
        real.display_name = "anything".into();
        let out = recover(vec![forged, real]);
        assert_eq!(
            out[0].display_name, "Evil.Tool",
            "unknown apps show their id"
        );
        assert_eq!(out[0].op.version, None);
        assert_eq!(
            out[1].display_name, "Firefox",
            "catalog apps show the catalog name"
        );
    }

    #[test]
    fn a_newer_file_is_left_alone() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("jobs.json");
        std::fs::write(&p, r#"{"schemaVersion":99,"jobs":[]}"#).unwrap();
        let l = load(&p);
        assert!(l.jobs.is_empty());
        assert!(!l.writable, "a newer app's history is never overwritten");
        assert!(p.exists());
    }
}
