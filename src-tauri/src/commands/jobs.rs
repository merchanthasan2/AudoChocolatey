//! Queue commands: everything that changes software on the PC goes through here.

use crate::background::toast::{batch_lines, BatchWatch};
use crate::error::AppError;
use crate::ids::{SourceKind, SourceRef};
use crate::jobs::model::Job;
use crate::jobs::worker::{JobSink, QueueSnapshot};
use crate::sources::model::{OpKind, OpRequest, UpgradeCandidate};
use crate::state::AppState;
use serde::Deserialize;
use tauri::{AppHandle, Emitter, Manager, State};
use ts_rs::TS;

#[derive(Debug, Clone, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct EnqueueItem {
    pub op: OpRequest,
    pub display_name: String,
}

/// Forwards queue activity to the UI as Tauri events.
pub struct TauriSink {
    app: AppHandle,
    batch: std::sync::Mutex<BatchWatch>,
}

impl TauriSink {
    pub fn new(app: AppHandle) -> Self {
        Self {
            app,
            batch: Default::default(),
        }
    }
}

impl JobSink for TauriSink {
    fn changed(&self, job: &Job) {
        let _ = self.app.emit("job://changed", job);
    }
    fn progress(&self, job: &Job) {
        let _ = self.app.emit("job://progress", job);
    }
    fn queue(&self, snapshot: &QueueSnapshot) {
        let _ = self.app.emit("queue://changed", snapshot);
        let Some(done) = self
            .batch
            .lock()
            .expect("batch lock")
            .observe(&snapshot.jobs)
        else {
            return;
        };
        let Some(state) = self.app.try_state::<AppState>() else {
            return;
        };
        let s = state.settings();
        if crate::background::shell::should_notify(&self.app) {
            for (title, body) in batch_lines(&done, s.notify_finished, s.notify_failed) {
                crate::background::toast::show(&title, &body);
            }
        }
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

/// Same promise for Microsoft Store apps.
pub fn refuse_disabled_store(
    ops: &[(OpRequest, String)],
    store_enabled: bool,
) -> Result<(), AppError> {
    if !store_enabled
        && ops
            .iter()
            .any(|(o, _)| o.target.source() == SourceKind::Msstore)
    {
        return Err(AppError::new(
            "store_disabled",
            "Microsoft Store apps are turned off. Turn them on in Settings to install this app.",
        ));
    }
    Ok(())
}

/// Store apps install into the account running the app; refuse unless that's the person
/// signed in at the desktop (`same_user` from `system::shell_user_matches`).
pub fn refuse_store_other_user(
    ops: &[(OpRequest, String)],
    same_user: Option<bool>,
) -> Result<(), AppError> {
    let store = ops
        .iter()
        .any(|(o, _)| o.target.source() == SourceKind::Msstore);
    if store && same_user != Some(true) {
        return Err(AppError::new(
            "other_user",
            "Microsoft Store apps install into your own Windows account, but Quantum Updater is running as a different administrator, so they can't be installed here.",
        ));
    }
    Ok(())
}

/// Apps Quantum Updater itself, its window or its sources need. Removing them would break the
/// app (or Windows features), so it's refused for single and batch removals alike.
const PROTECTED: &[(SourceKind, &str)] = &[
    (SourceKind::Winget, "QuantumStep.QuantumUpdater"),
    (SourceKind::Winget, "Microsoft.EdgeWebView2Runtime"),
    (SourceKind::Winget, "Microsoft.AppInstaller"),
    (SourceKind::Winget, "Chocolatey.Chocolatey"),
    (SourceKind::Msstore, "9NBLGGH4NNS1"),
    (SourceKind::Choco, "chocolatey"),
    (SourceKind::Choco, "webview2-runtime"),
];

/// At most this many removals in one go.
pub const MAX_REMOVE_BATCH: usize = 50;

pub fn refuse_protected_uninstall(ops: &[(OpRequest, String)]) -> Result<(), AppError> {
    let removals = ops.iter().filter(|(o, _)| o.kind == OpKind::Uninstall);
    if removals.clone().count() > MAX_REMOVE_BATCH {
        return Err(AppError::new(
            "invalid_input",
            "Remove at most 50 apps at a time.",
        ));
    }
    for (o, name) in removals {
        let hit = PROTECTED
            .iter()
            .any(|(src, id)| *src == o.target.source() && id.eq_ignore_ascii_case(o.target.id()));
        if hit {
            return Err(AppError::new(
                "protected",
                format!("{name} is needed by Quantum Updater or Windows, so it can't be removed here. Nothing was removed."),
            ));
        }
    }
    Ok(())
}

/// Closing the app kills its child processes (Job Object), so closing while a job runs asks first.
pub fn close_blocked(jobs: &[Job]) -> bool {
    // Any running job: phase lines are only recognised in English, so "Installing" can't be
    // relied on to tell when an installer is running.
    jobs.iter()
        .any(|j| !j.phase.is_finished() && j.phase != crate::jobs::model::JobPhase::Queued)
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
    allowed(&state, &ops)?;
    Ok(state.queue.enqueue(ops))
}

/// Every check a change to the PC must pass, for new jobs and retries alike.
fn allowed(state: &AppState, ops: &[(OpRequest, String)]) -> Result<(), AppError> {
    refuse_unelevated(crate::commands::system::is_elevated())?;
    refuse_protected_uninstall(ops)?;
    let g = gates(state);
    refuse_disabled_choco(ops, g.choco)?;
    refuse_disabled_store(ops, g.store)?;
    refuse_store_other_user(ops, g.same_user)
}

/// The per-source switches a change must pass.
#[derive(Debug, Clone, Copy)]
pub struct Gates {
    pub choco: bool,
    pub store: bool,
    pub same_user: Option<bool>,
}

pub fn gates(state: &AppState) -> Gates {
    let s = state.settings();
    Gates {
        choco: s.choco.enabled,
        store: s.store.enabled,
        same_user: crate::commands::system::shell_user_matches(),
    }
}

/// Keeps only the operations a single click would allow, so "Update all" never does more.
pub fn permitted(ops: Vec<(OpRequest, String)>, g: Gates) -> Vec<(OpRequest, String)> {
    ops.into_iter()
        .filter(|op| {
            let one = std::slice::from_ref(op);
            refuse_disabled_choco(one, g.choco).is_ok()
                && refuse_disabled_store(one, g.store).is_ok()
                && refuse_store_other_user(one, g.same_user).is_ok()
        })
        .collect()
}

#[tauri::command]
pub async fn update_all(state: State<'_, AppState>) -> Result<Vec<String>, AppError> {
    refuse_unelevated(crate::commands::system::is_elevated())?;
    let updates = crate::commands::library::collect_updates(&state).await;
    // `items` already excludes apps the user skipped or set to never update.
    let ops = permitted(upgrades_to_ops(updates.items), gates(&state));
    Ok(state.queue.enqueue(ops))
}

#[tauri::command]
pub fn get_queue(state: State<'_, AppState>) -> QueueSnapshot {
    state.queue.queue_snapshot()
}

#[tauri::command]
pub fn pause_queue(app: AppHandle, state: State<'_, AppState>) {
    state.queue.pause();
    crate::background::shell::set_paused(&app, true);
}

#[tauri::command]
pub fn resume_queue(app: AppHandle, state: State<'_, AppState>) {
    state.queue.resume();
    crate::background::shell::set_paused(&app, false);
}

#[tauri::command]
pub fn cancel_job(state: State<'_, AppState>, id: String) -> Result<(), AppError> {
    state.queue.cancel(&id)
}

#[tauri::command]
pub fn retry_job(state: State<'_, AppState>, id: String) -> Result<String, AppError> {
    if let Some(j) = state.queue.snapshot().into_iter().find(|j| j.id == id) {
        allowed(&state, &[(j.op, j.display_name)])?;
    }
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
    fn store_jobs_are_refused_while_the_store_is_off() {
        let ops = vec![(
            OpRequest {
                kind: OpKind::Install,
                target: SourceRef::new(SourceKind::Msstore, "9NKSQGP7F2NH").unwrap(),
                version: None,
            },
            "WhatsApp".to_string(),
        )];
        assert_eq!(
            refuse_disabled_store(&ops, false).unwrap_err().code,
            "store_disabled"
        );
        assert!(refuse_disabled_store(&ops, true).is_ok());
    }

    #[test]
    fn store_jobs_need_the_signed_in_user() {
        let ops = vec![(
            OpRequest {
                kind: OpKind::Install,
                target: SourceRef::new(SourceKind::Msstore, "9NKSQGP7F2NH").unwrap(),
                version: None,
            },
            "WhatsApp".to_string(),
        )];
        assert!(refuse_store_other_user(&ops, Some(true)).is_ok());
        assert_eq!(
            refuse_store_other_user(&ops, Some(false)).unwrap_err().code,
            "other_user"
        );
        assert_eq!(
            refuse_store_other_user(&ops, None).unwrap_err().code,
            "other_user"
        );
        let winget_only = upgrades_to_ops(vec![cand(SourceKind::Winget, "A.A")]);
        assert!(refuse_store_other_user(&winget_only, Some(false)).is_ok());
    }

    #[test]
    fn update_all_skips_what_a_single_update_would_refuse() {
        let store = (
            OpRequest {
                kind: OpKind::Upgrade,
                target: SourceRef::new(SourceKind::Msstore, "9NKSQGP7F2NH").unwrap(),
                version: None,
            },
            "WhatsApp".to_string(),
        );
        let mut ops = upgrades_to_ops(vec![
            cand(SourceKind::Winget, "A.A"),
            cand(SourceKind::Choco, "b"),
        ]);
        ops.push(store);
        let all_on = Gates {
            choco: true,
            store: true,
            same_user: Some(true),
        };
        assert_eq!(permitted(ops.clone(), all_on).len(), 3);
        assert_eq!(
            permitted(
                ops.clone(),
                Gates {
                    store: false,
                    ..all_on
                }
            )
            .len(),
            2
        );
        let kept = permitted(
            ops.clone(),
            Gates {
                same_user: Some(false),
                ..all_on
            },
        );
        assert!(kept
            .iter()
            .all(|(o, _)| o.target.source() != SourceKind::Msstore));
        let kept = permitted(
            ops,
            Gates {
                choco: false,
                ..all_on
            },
        );
        assert!(kept
            .iter()
            .all(|(o, _)| o.target.source() != SourceKind::Choco));
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
        // Phase lines are English-only, so any running job holds the close (on German Windows a
        // winget job never leaves Preparing in our eyes).
        assert!(!close_blocked(&[
            mk(JobPhase::Queued),
            mk(JobPhase::Succeeded)
        ]));
        assert!(close_blocked(&[mk(JobPhase::Preparing)]));
        assert!(close_blocked(&[mk(JobPhase::Downloading)]));
        assert!(close_blocked(&[mk(JobPhase::Installing)]));
        assert!(close_blocked(&[mk(JobPhase::Verifying)]));
        assert!(!close_blocked(&[]));
    }

    fn remove(source: SourceKind, id: &str) -> (OpRequest, String) {
        (
            OpRequest {
                kind: OpKind::Uninstall,
                target: SourceRef::new(source, id).unwrap(),
                version: None,
            },
            id.to_string(),
        )
    }

    #[test]
    fn apps_the_updater_needs_cannot_be_removed() {
        for (src, id) in [
            (SourceKind::Winget, "QuantumStep.QuantumUpdater"),
            (SourceKind::Winget, "microsoft.edgewebview2runtime"),
            (SourceKind::Winget, "Microsoft.AppInstaller"),
            (SourceKind::Msstore, "9NBLGGH4NNS1"),
            (SourceKind::Choco, "Chocolatey"),
        ] {
            let ops = [remove(SourceKind::Winget, "VideoLAN.VLC"), remove(src, id)];
            let err = refuse_protected_uninstall(&ops).unwrap_err();
            assert_eq!(err.code, "protected", "{id}");
            assert!(err.message.contains("Nothing was removed"));
        }
        assert!(refuse_protected_uninstall(&[remove(SourceKind::Winget, "VideoLAN.VLC")]).is_ok());
        // Updating them is fine.
        let upd = upgrades_to_ops(vec![cand(
            SourceKind::Winget,
            "Microsoft.EdgeWebView2Runtime",
        )]);
        assert!(refuse_protected_uninstall(&upd).is_ok());
    }

    #[test]
    fn at_most_50_removals_at_once() {
        let ops: Vec<_> = (0..51)
            .map(|i| remove(SourceKind::Winget, &format!("App.N{i}")))
            .collect();
        assert!(refuse_protected_uninstall(&ops).is_err());
        assert!(refuse_protected_uninstall(&ops[..50]).is_ok());
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
