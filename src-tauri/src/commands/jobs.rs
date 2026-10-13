//! Queue commands: everything that changes software on the PC goes through here.

use crate::error::AppError;
use crate::ids::{SourceKind, SourceRef};
use crate::jobs::model::Job;
use crate::jobs::worker::{JobSink, QueueSnapshot};
use crate::sources::model::{OpKind, OpRequest, UpgradeCandidate};
use crate::state::AppState;
use serde::Deserialize;
use tauri::{AppHandle, Emitter, State};
use ts_rs::TS;

#[derive(Debug, Clone, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct EnqueueItem {
    pub op: OpRequest,
    pub display_name: String,
}

/// Forwards queue activity to the UI as Tauri events.
pub struct TauriSink(pub AppHandle);

impl JobSink for TauriSink {
    fn changed(&self, job: &Job) {
        let _ = self.0.emit("job://changed", job);
    }
    fn progress(&self, job: &Job) {
        let _ = self.0.emit("job://progress", job);
    }
    fn queue(&self, snapshot: &QueueSnapshot) {
        let _ = self.0.emit("queue://changed", snapshot);
    }
}

pub fn upgrades_to_ops(items: Vec<UpgradeCandidate>) -> Vec<(OpRequest, String)> {
    items
        .into_iter()
        .filter_map(|c| {
            let target = SourceRef::new(c.source, c.id).ok()?;
            Some((
                OpRequest {
                    kind: OpKind::Upgrade,
                    target,
                    version: None,
                },
                c.name,
            ))
        })
        .collect()
}

/// The "Use Chocolatey" setting is a promise that choco never runs; the UI hides those buttons,
/// and this holds the line if a button slips through.
pub fn refuse_disabled_choco(
    ops: &[(OpRequest, String)],
    choco_enabled: bool,
) -> Result<(), AppError> {
    if !choco_enabled
        && ops
            .iter()
            .any(|(o, _)| o.target.source() == SourceKind::Choco)
    {
        return Err(AppError::new(
            "choco_disabled",
            "Chocolatey is turned off. Turn it on in Settings to use Chocolatey apps.",
        ));
    }
    Ok(())
}

/// Closing the app kills its child processes (Job Object), so closing mid-install asks first.
pub fn close_blocked(jobs: &[Job]) -> bool {
    jobs.iter().any(|j| {
        matches!(
            j.phase,
            crate::jobs::model::JobPhase::Installing | crate::jobs::model::JobPhase::Verifying
        )
    })
}

/// The user chose to close while an installer runs.
#[tauri::command]
pub fn quit_anyway(app: AppHandle) {
    app.exit(0);
}

/// Installing, updating or removing apps needs admin rights; dev builds run without them.
pub fn refuse_unelevated(elevated: bool) -> Result<(), AppError> {
    if elevated {
        return Ok(());
    }
    Err(AppError::new(
        "needs_admin",
        "Quantum Updater needs administrator rights to install or change apps. Restart it as administrator.",
    ))
}

fn display_name(name: String) -> String {
    let n: String = name.chars().filter(|c| !c.is_control()).take(120).collect();
    if n.trim().is_empty() {
        "App".into()
    } else {
        n
    }
}

#[tauri::command]
pub fn enqueue(state: State<'_, AppState>, ops: Vec<EnqueueItem>) -> Result<Vec<String>, AppError> {
    let ops: Vec<_> = ops
        .into_iter()
        .map(|i| (i.op, display_name(i.display_name)))
        .collect();
    refuse_unelevated(crate::commands::system::is_elevated())?;
    refuse_disabled_choco(&ops, state.settings().choco.enabled)?;
    Ok(state.queue.enqueue(ops))
}

#[tauri::command]
pub async fn update_all(state: State<'_, AppState>) -> Result<Vec<String>, AppError> {
    refuse_unelevated(crate::commands::system::is_elevated())?;
    let updates = crate::commands::library::collect_updates(&state).await;
    Ok(state.queue.enqueue(upgrades_to_ops(updates.items)))
}

#[tauri::command]
pub fn get_queue(state: State<'_, AppState>) -> QueueSnapshot {
    state.queue.queue_snapshot()
}

#[tauri::command]
pub fn pause_queue(state: State<'_, AppState>) {
    state.queue.pause()
}

#[tauri::command]
pub fn resume_queue(state: State<'_, AppState>) {
    state.queue.resume()
}

#[tauri::command]
pub fn cancel_job(state: State<'_, AppState>, id: String) -> Result<(), AppError> {
    state.queue.cancel(&id)
}

#[tauri::command]
pub fn retry_job(state: State<'_, AppState>, id: String) -> Result<String, AppError> {
    refuse_unelevated(crate::commands::system::is_elevated())?;
    state.queue.retry(&id)
}

#[tauri::command]
pub fn clear_finished(state: State<'_, AppState>) {
    state.queue.clear_finished()
}

#[tauri::command]
pub fn get_job_log(state: State<'_, AppState>, id: String) -> Vec<String> {
    state.queue.log(&id)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sources::model::UpgradeCandidate;

    fn cand(source: SourceKind, id: &str) -> UpgradeCandidate {
        UpgradeCandidate {
            name: format!("{id} name"),
            id: id.into(),
            installed_version: "1".into(),
            available_version: "2".into(),
            source,
        }
    }

    #[test]
    fn closing_is_held_only_while_an_installer_runs() {
        use crate::jobs::model::JobPhase;
        let mk = |p: JobPhase| {
            let mut j = Job::new(
                "j1".into(),
                upgrades_to_ops(vec![cand(SourceKind::Winget, "A.A")])
                    .remove(0)
                    .0,
                "A".into(),
            );
            j.phase = p;
            j
        };
        assert!(!close_blocked(&[
            mk(JobPhase::Downloading),
            mk(JobPhase::Queued)
        ]));
        assert!(close_blocked(&[mk(JobPhase::Installing)]));
        assert!(close_blocked(&[mk(JobPhase::Verifying)]));
        assert!(!close_blocked(&[]));
    }

    #[test]
    fn changes_are_refused_without_admin_rights() {
        let err = refuse_unelevated(false).unwrap_err();
        assert_eq!(err.code, "needs_admin");
        assert!(err.message.contains("administrator"));
        assert!(refuse_unelevated(true).is_ok());
    }

    #[test]
    fn chocolatey_jobs_are_refused_while_chocolatey_is_off() {
        let ops = upgrades_to_ops(vec![
            cand(SourceKind::Winget, "Mozilla.Firefox"),
            cand(SourceKind::Choco, "filezilla"),
        ]);
        let err = refuse_disabled_choco(&ops, false).unwrap_err();
        assert_eq!(err.code, "choco_disabled");
        assert!(refuse_disabled_choco(&ops, true).is_ok());
        assert!(refuse_disabled_choco(&ops[..1], false).is_ok());
    }

    #[test]
    fn update_all_maps_each_candidate_to_an_upgrade_in_its_source() {
        let ops = upgrades_to_ops(vec![
            cand(SourceKind::Winget, "Mozilla.Firefox"),
            cand(SourceKind::Choco, "vcredist140"),
            cand(SourceKind::Winget, "-bad id"),
        ]);
        assert_eq!(ops.len(), 2);
        assert_eq!(ops[0].0.kind, OpKind::Upgrade);
        assert_eq!(ops[0].0.target.source(), SourceKind::Winget);
        assert_eq!(ops[0].0.target.id(), "Mozilla.Firefox");
        assert_eq!(ops[0].1, "Mozilla.Firefox name");
        assert_eq!(ops[1].0.target.source(), SourceKind::Choco);
        assert_eq!(ops[1].0.version, None);
    }
}
