//! A queued package operation and the phases the UI animates (Get -> ring -> spin -> tick).

use crate::sources::model::OpRequest;
use serde::Serialize;
use ts_rs::TS;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum JobPhase {
    Queued,
    Preparing,
    /// Another installer holds the Windows Installer mutex.
    Waiting,
    Downloading,
    Installing,
    Verifying,
    Succeeded,
    Failed,
    Cancelled,
    Skipped,
}

impl JobPhase {
    pub fn is_finished(self) -> bool {
        matches!(
            self,
            Self::Succeeded | Self::Failed | Self::Cancelled | Self::Skipped
        )
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct Job {
    pub id: String,
    pub op: OpRequest,
    pub display_name: String,
    pub phase: JobPhase,
    pub detail: Option<String>,
    /// 0..=100, only when it is a real measurement.
    pub percent: Option<f32>,
    #[ts(type = "number | null")]
    pub bytes_done: Option<u64>,
    #[ts(type = "number | null")]
    pub bytes_total: Option<u64>,
    pub cancellable: bool,
    pub reboot_required: bool,
    pub error: Option<String>,
    /// Unix epoch milliseconds.
    #[ts(type = "number | null")]
    pub started_at: Option<u64>,
    #[ts(type = "number | null")]
    pub ended_at: Option<u64>,
}

pub fn now_ms() -> u64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_millis() as u64)
        .unwrap_or(0)
}

impl Job {
    pub fn new(id: String, op: OpRequest, display_name: String) -> Self {
        Self {
            id,
            op,
            display_name,
            phase: JobPhase::Queued,
            detail: None,
            percent: None,
            bytes_done: None,
            bytes_total: None,
            cancellable: true,
            reboot_required: false,
            error: None,
            started_at: None,
            ended_at: None,
        }
    }

    /// Killing an installer mid-install can leave a broken app, so only pre-install phases cancel.
    pub fn cancellable_in(phase: JobPhase) -> bool {
        matches!(
            phase,
            JobPhase::Queued | JobPhase::Preparing | JobPhase::Waiting | JobPhase::Downloading
        )
    }

    pub fn set_phase(&mut self, phase: JobPhase) {
        if self.phase.is_finished() {
            return;
        }
        self.phase = phase;
        self.cancellable = Self::cancellable_in(phase);
        if phase == JobPhase::Preparing && self.started_at.is_none() {
            self.started_at = Some(now_ms());
        }
        if phase.is_finished() {
            self.ended_at = Some(now_ms());
        }
        if phase != JobPhase::Downloading {
            self.percent = None;
            self.bytes_done = None;
            self.bytes_total = None;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::{SourceKind, SourceRef};
    use crate::sources::model::{OpKind, OpRequest};

    fn job() -> Job {
        Job::new(
            "j1".into(),
            OpRequest {
                kind: OpKind::Install,
                target: SourceRef::new(SourceKind::Winget, "Mozilla.Firefox").unwrap(),
                version: None,
            },
            "Firefox".into(),
        )
    }

    #[test]
    fn new_job_is_queued_and_cancellable() {
        let j = job();
        assert_eq!(j.phase, JobPhase::Queued);
        assert!(j.cancellable);
        assert_eq!(j.percent, None);
    }

    #[test]
    fn cancellable_only_before_installing() {
        for p in [
            JobPhase::Queued,
            JobPhase::Preparing,
            JobPhase::Waiting,
            JobPhase::Downloading,
        ] {
            assert!(Job::cancellable_in(p), "{p:?}");
        }
        for p in [
            JobPhase::Installing,
            JobPhase::Verifying,
            JobPhase::Succeeded,
            JobPhase::Failed,
        ] {
            assert!(!Job::cancellable_in(p), "{p:?}");
        }
    }

    #[test]
    fn set_phase_updates_cancellable_and_finished() {
        let mut j = job();
        j.set_phase(JobPhase::Installing);
        assert!(!j.cancellable);
        assert!(!j.phase.is_finished());
        j.set_phase(JobPhase::Succeeded);
        assert!(j.phase.is_finished());
        assert!(j.ended_at.is_some());
    }

    #[test]
    fn finished_jobs_never_restart() {
        let mut j = job();
        j.set_phase(JobPhase::Cancelled);
        j.set_phase(JobPhase::Preparing);
        assert_eq!(j.phase, JobPhase::Cancelled);
    }

    #[test]
    fn serializes_camel_case() {
        let mut j = job();
        j.set_phase(JobPhase::Downloading);
        let v = serde_json::to_value(&j).unwrap();
        assert_eq!(v["phase"], "downloading");
        assert_eq!(v["displayName"], "Firefox");
        assert_eq!(v["rebootRequired"], false);
        assert_eq!(v["op"]["target"]["id"], "Mozilla.Firefox");
    }
}
