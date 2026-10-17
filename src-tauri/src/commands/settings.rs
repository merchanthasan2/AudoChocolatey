use crate::background::auto::{may_auto_install, read_probe, Hold};
use crate::error::AppError;
use crate::settings::schema::{Settings, SettingsPatch};
use crate::settings::SettingsStore;
use crate::state::AppState;
use serde::Serialize;
use tauri::State;
use ts_rs::TS;

#[tauri::command]
pub fn get_settings(state: State<'_, AppState>) -> Settings {
    state.settings()
}

#[tauri::command]
pub fn update_settings(
    state: State<'_, AppState>,
    patch: SettingsPatch,
) -> Result<Settings, AppError> {
    let mut store = state.settings.lock().expect("settings lock");
    update_with(&mut store, patch, crate::background::autostart::apply)
}

/// Whether automatic updates could start right now, and if not, why (shown in Settings).
#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct AutoStatus {
    pub hold: Option<Hold>,
    pub message: Option<String>,
}

#[tauri::command]
pub async fn get_auto_status(state: State<'_, AppState>) -> Result<AutoStatus, AppError> {
    let rules = state.settings().auto_rules;
    let probe = tauri::async_runtime::spawn_blocking(read_probe)
        .await
        .unwrap_or_default();
    let hold = may_auto_install(&rules, &probe).err();
    Ok(AutoStatus {
        hold,
        message: hold.map(|h| h.message().to_string()),
    })
}

/// Start at sign-in changes Windows first; if that's refused, nothing is saved.
fn update_with(
    store: &mut SettingsStore,
    patch: SettingsPatch,
    autostart: impl FnOnce(bool) -> Result<(), String>,
) -> Result<Settings, AppError> {
    if let Some(want) = patch.start_at_login {
        // Turning on always re-registers (repairs a stale task); off only if it was on.
        if want || store.get().start_at_login {
            autostart(want).map_err(|m| AppError::new("autostart", m))?;
        }
    }
    store.apply(patch)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn patch(on: bool) -> SettingsPatch {
        SettingsPatch {
            start_at_login: Some(on),
            background_prompted: Some(true),
            ..Default::default()
        }
    }

    #[test]
    fn a_refused_autostart_saves_nothing() {
        let dir = tempfile::tempdir().unwrap();
        let mut store = SettingsStore::open(dir.path().join("settings.json"));
        let err =
            update_with(&mut store, patch(true), |_| Err("Access is denied.".into())).unwrap_err();
        assert_eq!(err.message, "Access is denied.");
        assert!(!store.get().start_at_login);
        assert!(
            !store.get().background_prompted,
            "the whole patch is dropped"
        );
    }

    #[test]
    fn windows_changes_before_the_setting_is_saved() {
        let dir = tempfile::tempdir().unwrap();
        let mut store = SettingsStore::open(dir.path().join("settings.json"));
        let mut calls = Vec::new();
        let saved = update_with(&mut store, patch(true), |w| {
            calls.push(w);
            Ok(())
        })
        .unwrap();
        assert!(saved.start_at_login && saved.background_prompted);
        // Off when it was on removes the task; off when already off leaves Windows alone.
        update_with(&mut store, patch(false), |w| {
            calls.push(w);
            Ok(())
        })
        .unwrap();
        update_with(&mut store, patch(false), |w| {
            calls.push(w);
            Ok(())
        })
        .unwrap();
        assert_eq!(calls, vec![true, false]);
    }
}
