//! The startup page names an item. Rust finds that same item again and refuses anything it
//! classifies as Windows, security software, policy, or this app's own sign-in task.

use crate::error::AppError;
use crate::startup::{StartupItem, StartupList, StartupRef};
use tauri::async_runtime::spawn_blocking;

fn other_account() -> AppError {
    AppError::new(
        "other_account",
        "Quantum Updater is running as a different administrator, so this can't be changed.",
    )
}

#[tauri::command]
pub async fn list_startup() -> Result<StartupList, AppError> {
    let per_user = crate::commands::system::shell_user_matches() != Some(false);
    spawn_blocking(move || crate::startup::collect(per_user))
        .await
        .map_err(|e| AppError::new("startup", e.to_string()))
}

#[tauri::command]
pub async fn set_startup(item: StartupRef, enabled: bool) -> Result<StartupItem, AppError> {
    if !crate::startup::ref_ok(item.place, &item.name, &item.folder) {
        return Err(AppError::new(
            "invalid_input",
            "That startup item can't be changed.",
        ));
    }
    let per_user = crate::commands::system::shell_user_matches() != Some(false);
    if !per_user && crate::startup::place_is_per_user(item.place) {
        return Err(other_account());
    }
    let machine_wide = matches!(
        item.place,
        crate::startup::StartupPlace::RunHklm
            | crate::startup::StartupPlace::RunHklm32
            | crate::startup::StartupPlace::FolderCommon
    );
    if machine_wide && !crate::commands::system::is_elevated() {
        return Err(AppError::new(
            "needs_admin",
            "Quantum Updater needs administrator access to change this.",
        ));
    }
    spawn_blocking(move || crate::startup::apply(item, enabled))
        .await
        .map_err(|e| AppError::new("startup", e.to_string()))?
}
