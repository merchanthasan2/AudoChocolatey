//! A queued package operation and the phases the UI animates (Get -> ring -> spin -> tick).

use crate::ids::SourceRef;
use crate::jobs::failure::FailureReason;
use crate::jobs::verify::Verify;
use crate::sources::model::OpRequest;
use serde::{Deserialize, Serialize};
use ts_rs::TS;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
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

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, TS)]
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
    /// Why it failed (drives the message and backup-source decisions).
    pub reason: Option<FailureReason>,
    /// Whether the installed version really changed (set after success).
    pub verify: Option<Verify>,
    pub from_version: Option<String>,
    pub to_version: Option<String>,
    /// 0 for the first try, 1 when a backup source was used.
    pub attempt: u8,
    /// The package actually used when it differs from `op.target` (backup source).
    pub via: Option<SourceRef>,
    /// Unix epoch milliseconds.
    #[ts(type = "number | null")]
    pub started_at: Option<u64>,
    #[ts(type = "number | null")]
    pub ended_at: Option<u64>,
    /// "Clear finished" hides it from the activity panel; it stays in History.
    #[serde(default)]
    pub dismissed: bool,
    /// Who signed the installer it ran (a note only).
    #[serde(default)]
    pub signer: Option<crate::jobs::signer::Signer>,
}

/// Apps whose finished change asked for a restart that hasn't happened yet (it finished after
/// Windows last started). Each name once, in order.
pub fn restarts_needed(jobs: &[Job], boot_ms: u64) -> Vec<String> {
    let mut names: Vec<String> = Vec::new();
    for j in jobs {
        let pending = j.phase == JobPhase::Succeeded
            && j.reboot_required
            && j.ended_at.is_some_and(|t| t >= boot_ms);
        if pending && !names.contains(&j.display_name) {
            names.push(j.display_name.clone());
        }
    }
    names
}

/// When Windows last started (ms since the epoch).
pub fn boot_ms() -> u64 {
    // SAFETY: no arguments; returns milliseconds since boot.
    let up = unsafe { windows_sys::Win32::System::SystemInformation::GetTickCount64() };
    now_ms().saturating_sub(up)
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
            reason: None,
            verify: None,
            from_version: None,
            to_version: None,
            attempt: 0,
            via: None,
            started_at: None,
            ended_at: None,
            dismissed: false,
            signer: None,
        }
    }

    /// Killing an installer mid-install can leave a broken app, so only pre-install phases cancel.
    pub fn cancellable_in(phase: JobPhase) -> bool {
        matches!(
            phase,
            JobPhase::Queued | JobPhase::Preparing | JobPhase::Waiting | JobPhase::Downloading
        )
    }

    /// Starts over from Preparing with a backup source (the only way a phase moves back).
    pub fn restart_attempt(&mut self, via: SourceRef) {
        self.attempt += 1;
        self.via = Some(via);
        self.signer = None;
        self.phase = JobPhase::Preparing;
        self.cancellable = true;
        self.detail = Some("Trying another source".into());
        self.error = None;
        self.reason = None;
        self.percent = None;
        self.bytes_done = None;
        self.bytes_total = None;
    }

    /// Ends the job as Failed with a reason and its plain message.
    pub fn fail(&mut self, reason: FailureReason) {
        self.reason = Some(reason);
        self.error = Some(reason.message(&self.display_name));
        self.set_phase(JobPhase::Failed);
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
                mode: Default::default(),
            },
            "Firefox".into(),
        )
    }

    #[test]
    fn restarts_are_needed_only_since_the_last_start() {
        let mut a = job();
        a.set_phase(JobPhase::Succeeded);
        a.reboot_required = true;
        a.ended_at = Some(2_000);
        let mut b = a.clone();
        b.id = "j2".into();
        b.display_name = "VLC".into();
        b.ended_at = Some(500); // before the last start
        let mut c = a.clone();
        c.id = "j3".into();
        c.reboot_required = false;
        let mut d = a.clone();
        d.id = "j4".into(); // same app again
        assert_eq!(
            restarts_needed(&[a, b, c, d], 1_000),
            vec!["Firefox".to_string()]
        );
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
