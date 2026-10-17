//! History and "Go back to the earlier version". The page only names a history entry; Rust
//! re-checks everything against the PC and the source, and a Windows dialog asks first.

use crate::catalog::Catalog;
use crate::commands::jobs::refuse_unelevated;
use crate::error::AppError;
use crate::ids::{ChocoId, PkgVersion, SourceKind, WingetId};
use crate::jobs::model::Job;
use crate::rollback::{display_name, pick_listed, rollback_offer};
use crate::rules::{rule_key, Rule, RuleKind};
use crate::sources::model::{OpKind, OpRequest};
use crate::state::AppState;
use crate::versions::compare;
use serde::Serialize;
use std::cmp::Ordering;
use tauri::{AppHandle, Manager, State};
use tauri_plugin_dialog::{DialogExt, MessageDialogButtons, MessageDialogKind};
use ts_rs::TS;

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct HistoryEntry {
    pub job: Job,
    /// History shows "Go back"; clicking re-checks everything first.
    pub can_go_back: bool,
}

struct RollbackPlan {
    pub name: String,
    /// The version it goes back to.
    pub to: String,
    /// The version it leaves.
    pub from: String,
}

/// Finished changes, newest first (also the ones cleared from the activity panel).
#[tauri::command]
pub fn get_history(state: State<'_, AppState>) -> Vec<HistoryEntry> {
    let cat = Catalog::embedded();
    let history = state.queue.history();
    history
        .iter()
        .map(|j| HistoryEntry {
            can_go_back: rollback_offer(j, &history, cat).is_ok(),
            job: j.clone(),
        })
        .collect()
}

fn refuse(msg: impl Into<String>) -> AppError {
    AppError::new("rollback", msg)
}

/// Everything checked again, from scratch: the offer, admin rights, the source switch, the
/// installed version, and that the source still lists the earlier version.
async fn plan(state: &AppState, job_id: &str) -> Result<(RollbackPlan, OpRequest), AppError> {
    let cat = Catalog::embedded();
    let history = state.queue.history();
    let job = history
        .iter()
        .find(|j| j.id == job_id)
        .ok_or_else(|| refuse("That change isn't in the history any more."))?;
    let (target, earlier, current) = rollback_offer(job, &history, cat).map_err(refuse)?;
    refuse_unelevated(crate::commands::system::is_elevated())?;
    let name = display_name(&target, cat);
    let source = target.source();
    if source == SourceKind::Choco && !state.settings().choco.enabled {
        return Err(refuse(
            "Community catalog apps are turned off in Settings, so this can't be undone now.",
        ));
    }
    let (installed, listed) = match source {
        SourceKind::Winget => {
            let id = WingetId::try_from(target.id().to_string())?;
            (
                state.winget.installed_version(&id).await,
                state.winget.versions(&id).await,
            )
        }
        SourceKind::Choco => {
            let id = ChocoId::try_from(target.id().to_string())?;
            (
                state.choco.installed_version(&id).await,
                state.choco.versions(&id).await,
            )
        }
        SourceKind::Msstore => return Err(refuse("Microsoft Store apps can't go back.")),
    };
    let installed = installed.map_err(|_| {
        refuse(format!(
            "Couldn't check which version of {name} is installed."
        ))
    })?;
    if !installed.is_some_and(|v| compare(&v, current) == Ordering::Equal) {
        return Err(refuse(format!(
            "{name} isn't on version {current} any more, so there's nothing to undo."
        )));
    }
    let listed = listed.map_err(|_| {
        refuse("Couldn't check which versions are available. Try again in a moment.")
    })?;
    let version = pick_listed(&listed, earlier).ok_or_else(|| {
        refuse(format!(
            "Version {earlier} of {name} isn't available any more."
        ))
    })?;
    let op = OpRequest {
        kind: OpKind::Rollback,
        target,
        version: Some(PkgVersion::try_from(version.clone())?),
    };
    Ok((
        RollbackPlan {
            name,
            to: version,
            from: current.to_string(),
        },
        op,
    ))
}

/// Goes back after a Windows dialog says yes. `None` when the person said no.
#[tauri::command]
pub async fn rollback(
    app: AppHandle,
    state: State<'_, AppState>,
    job_id: String,
) -> Result<Option<String>, AppError> {
    let (p, op) = plan(&state, &job_id).await?;
    let mut dlg = app
        .dialog()
        .message(format!(
            "{} will go from version {} back to version {}. Version {} will be skipped until a newer one comes out.",
            p.name, p.from, p.to, p.from
        ))
        .title(format!("Go back to {} {}?", p.name, p.to))
        .kind(MessageDialogKind::Warning)
        .buttons(MessageDialogButtons::OkCancelCustom(
            "Go back".into(),
            "Cancel".into(),
        ));
    if let Some(w) = app.get_webview_window("main") {
        dlg = dlg.parent(&w);
    }
    let yes = tauri::async_runtime::spawn_blocking(move || dlg.blocking_show())
        .await
        .unwrap_or(false);
    if !yes {
        return Ok(None);
    }
    let cat = Catalog::embedded();
    let key = rule_key(&op.target, cat);
    let id = state
        .queue
        .enqueue_exclusive(op.clone(), p.name.clone(), |t| rule_key(t, cat) == key)?;
    // Hide the version it left until a newer one appears; an app set to "Never" stays so.
    let mut rules = state.rules.lock().unwrap_or_else(|e| e.into_inner());
    let never = rules
        .rules()
        .iter()
        .any(|r| r.key == key && r.kind == RuleKind::Never);
    if !never {
        let _ = rules.set(Rule {
            key,
            target: op.target,
            name: p.name,
            kind: RuleKind::SkipVersion {
                version: PkgVersion::try_from(p.from)?,
                after: None,
            },
            created_at: crate::jobs::model::now_ms(),
        });
    }
    Ok(Some(id))
}
