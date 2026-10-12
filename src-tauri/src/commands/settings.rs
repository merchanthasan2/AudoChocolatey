use crate::error::AppError;
use crate::settings::schema::{Settings, SettingsPatch};
use crate::state::AppState;
use tauri::State;

#[tauri::command]
pub fn get_settings(state: State<'_, AppState>) -> Settings {
    state.settings()
}

#[tauri::command]
pub fn update_settings(
    state: State<'_, AppState>,
    patch: SettingsPatch,
) -> Result<Settings, AppError> {
    state.settings.lock().expect("settings lock").apply(patch)
}
