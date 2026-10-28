//! Remove an app that was installed directly, by opening the uninstaller it registered.

use crate::error::{AppError, AppResult};
use crate::sources::direct::{self, Removal};
use serde::Serialize;
use std::path::Path;
use std::time::Duration;
use ts_rs::TS;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct DirectRemoval {
    /// The uninstaller is open in its own window. The list changes when that window finishes.
    pub opened: bool,
}

#[tauri::command]
pub async fn remove_direct(id: String) -> Result<DirectRemoval, AppError> {
    crate::commands::jobs::refuse_unelevated(crate::commands::system::is_elevated())?;
    if id.len() > 500 || id.chars().any(|c| c.is_control()) {
        return Err(AppError::new(
            "direct_refused",
            "This app can't be removed here.",
        ));
    }
    let removal = direct::prepare(&id, crate::commands::system::shell_user_matches())?;
    match removal {
        Removal::Package { full_name, .. } => {
            tauri::async_runtime::spawn_blocking(move || remove_package(&full_name))
                .await
                .map_err(|_| AppError::new("direct_failed", "Windows couldn't remove it."))??;
            Ok(DirectRemoval { opened: false })
        }
        Removal::Program(plan) => {
            if plan.elevated {
                let mut child = tokio::process::Command::new(&plan.exe)
                    .args(&plan.args)
                    .stdin(std::process::Stdio::null())
                    .current_dir(plan.exe.parent().unwrap_or(Path::new(".")))
                    .spawn()
                    .map_err(|_| {
                        AppError::new("direct_failed", "Couldn't open its uninstaller.")
                    })?;
                // No kill_on_drop: if the window is still open when we stop waiting, it stays open.
                let waited = tokio::time::timeout(Duration::from_secs(30 * 60), child.wait()).await;
                match waited {
                    Err(_) => Ok(DirectRemoval { opened: true }),
                    Ok(Err(_)) => Err(AppError::new(
                        "direct_failed",
                        "Couldn't open its uninstaller.",
                    )),
                    Ok(Ok(status)) => match status.code() {
                        Some(0 | 3010) => Ok(DirectRemoval { opened: false }),
                        Some(1602 | 1223) => {
                            Err(AppError::new("cancelled", "Nothing was removed."))
                        }
                        _ => Err(AppError::new(
                            "direct_failed",
                            "Its uninstaller didn't finish. Nothing else was changed.",
                        )),
                    },
                }
            } else {
                crate::shell_open::launch_unelevated(&plan.exe, &plan.params)
                    .map_err(|m| AppError::new("direct_failed", m))?;
                Ok(DirectRemoval { opened: true })
            }
        }
    }
}

fn remove_package(full_name: &str) -> AppResult<()> {
    use windows::core::HSTRING;
    use windows::Management::Deployment::PackageManager;
    let manager = PackageManager::new()
        .map_err(|_| AppError::new("direct_failed", "Windows couldn't remove it."))?;
    let op = manager
        .RemovePackageAsync(&HSTRING::from(full_name))
        .map_err(|_| AppError::new("direct_failed", "Windows couldn't remove it."))?;
    op.join()
        .map_err(|_| AppError::new("direct_failed", "Windows couldn't remove it."))?;
    Ok(())
}
