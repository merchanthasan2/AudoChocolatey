use crate::sources::model::SourceStatus;
use crate::state::AppState;
use serde::Serialize;
use tauri::State;
use ts_rs::TS;

#[derive(Debug, Clone, Serialize, PartialEq, Eq, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct Overview {
    pub app_version: String,
    pub build_sha: String,
    pub is_elevated: bool,
    pub sources: Vec<SourceStatus>,
}

#[derive(Debug, Clone, Serialize, PartialEq, Eq, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct Diagnostics {
    pub app_version: String,
    pub build_sha: String,
    pub is_elevated: bool,
    pub winget_path: Option<String>,
    pub choco_path: Option<String>,
    pub config_dir: String,
    pub settings_read_only: bool,
    pub sources: Vec<SourceStatus>,
}

pub fn build_overview(is_elevated: bool, sources: Vec<SourceStatus>) -> Overview {
    Overview {
        app_version: env!("CARGO_PKG_VERSION").to_string(),
        build_sha: env!("BUILD_SHA").to_string(),
        is_elevated,
        sources,
    }
}

pub fn is_elevated() -> bool {
    // SAFETY: IsUserAnAdmin takes no arguments and has no preconditions.
    unsafe { windows_sys::Win32::UI::Shell::IsUserAnAdmin() != 0 }
}

async fn statuses(state: &AppState) -> Vec<SourceStatus> {
    let (w, c) = tokio::join!(state.winget.status(), state.choco.status());
    vec![w, c]
}

#[tauri::command]
pub async fn get_overview(state: State<'_, AppState>) -> Result<Overview, crate::error::AppError> {
    Ok(build_overview(is_elevated(), statuses(&state).await))
}

#[tauri::command]
pub async fn get_diagnostics(
    state: State<'_, AppState>,
) -> Result<Diagnostics, crate::error::AppError> {
    let path = |p: Option<&std::path::PathBuf>| p.map(|p| p.display().to_string());
    let settings_read_only = state.settings.lock().expect("settings lock").read_only();
    let sources = statuses(&state).await;
    Ok(Diagnostics {
        app_version: env!("CARGO_PKG_VERSION").to_string(),
        build_sha: env!("BUILD_SHA").to_string(),
        is_elevated: is_elevated(),
        winget_path: path(state.winget.exe()),
        choco_path: path(state.choco.exe()),
        config_dir: state.config_dir.display().to_string(),
        settings_read_only,
        sources,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind;
    use crate::sources::model::SourceState;

    fn sources() -> Vec<SourceStatus> {
        vec![
            SourceStatus {
                kind: SourceKind::Winget,
                state: SourceState::Ready {
                    version: "1.29.380".into(),
                },
            },
            SourceStatus {
                kind: SourceKind::Choco,
                state: SourceState::Missing,
            },
        ]
    }

    #[test]
    fn overview_reports_package_version_and_sha() {
        let o = build_overview(true, sources());
        assert_eq!(o.app_version, env!("CARGO_PKG_VERSION"));
        assert!(!o.build_sha.is_empty());
        assert!(o.is_elevated);
    }

    #[test]
    fn overview_serializes_camel_case_with_sources() {
        let json = serde_json::to_value(build_overview(false, sources())).unwrap();
        assert!(json.get("appVersion").is_some());
        assert!(json.get("buildSha").is_some());
        assert_eq!(json["isElevated"], false);
        assert_eq!(json["sources"][0]["kind"], "winget");
        assert_eq!(json["sources"][0]["state"], "ready");
        assert_eq!(json["sources"][1]["state"], "missing");
    }
}
