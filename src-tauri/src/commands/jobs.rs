//! Queue commands: everything that changes software on the PC goes through here.

use crate::error::AppError;
use crate::ids::{SourceKind, SourceRef};
use crate::jobs::model::Job;
use crate::jobs::worker::JobSink;
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
    fn queue(&self, jobs: &[Job]) {
        let _ = self.0.emit("queue://changed", jobs);
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
    refuse_disabled_choco(&ops, state.settings().choco.enabled)?;
    Ok(state.queue.enqueue(ops))
}

#[tauri::command]
pub async fn update_all(state: State<'_, AppState>) -> Result<Vec<String>, AppError> {
    let updates = crate::commands::library::collect_updates(&state).await;
    Ok(state.queue.enqueue(upgrades_to_ops(updates.items)))
}

#[tauri::command]
pub fn get_queue(state: State<'_, AppState>) -> Vec<Job> {
    state.queue.snapshot()
}

#[tauri::command]
pub fn cancel_job(state: State<'_, AppState>, id: String) -> Result<(), AppError> {
    state.queue.cancel(&id)
}

#[tauri::command]
pub fn retry_job(state: State<'_, AppState>, id: String) -> Result<String, AppError> {
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
