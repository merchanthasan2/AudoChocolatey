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

/// Is this app running as the same Windows user as the desktop (explorer)? `None` when there is
/// no desktop yet. False means "over-the-shoulder" admin: per-user installs (Store apps) would
/// land in the admin's account, not the person's at the keyboard.
pub fn shell_user_matches() -> Option<bool> {
    use windows_sys::Win32::Foundation::{CloseHandle, HANDLE};
    use windows_sys::Win32::Security::{
        EqualSid, GetTokenInformation, TokenUser, TOKEN_QUERY, TOKEN_USER,
    };
    use windows_sys::Win32::System::Threading::{
        GetCurrentProcess, OpenProcess, OpenProcessToken, PROCESS_QUERY_LIMITED_INFORMATION,
    };
    use windows_sys::Win32::UI::WindowsAndMessaging::{GetShellWindow, GetWindowThreadProcessId};

    // Reads a process token's user SID into an owned buffer.
    fn user_sid(process: HANDLE) -> Option<Vec<u8>> {
        let mut token: HANDLE = std::ptr::null_mut();
        // SAFETY: valid process handle and out-pointer; the token is closed below.
        if unsafe { OpenProcessToken(process, TOKEN_QUERY, &mut token) } == 0 {
            return None;
        }
        let mut buf = vec![0u8; 256];
        let mut len = 0u32;
        // SAFETY: buffer and length describe writable memory; token is open.
        let ok = unsafe {
            GetTokenInformation(
                token,
                TokenUser,
                buf.as_mut_ptr().cast(),
                buf.len() as u32,
                &mut len,
            )
        };
        // SAFETY: token was opened above.
        unsafe { CloseHandle(token) };
        (ok != 0).then_some(buf)
    }

    // SAFETY: no preconditions; returns null without a desktop shell.
    let shell = unsafe { GetShellWindow() };
    if shell.is_null() {
        return None;
    }
    let mut pid = 0u32;
    // SAFETY: shell is a window handle; pid is a valid out-pointer.
    unsafe { GetWindowThreadProcessId(shell, &mut pid) };
    if pid == 0 {
        return None;
    }
    // SAFETY: plain OpenProcess with query-only rights; closed below.
    let proc = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid) };
    if proc.is_null() {
        return None;
    }
    let theirs = user_sid(proc);
    // SAFETY: proc was opened above.
    unsafe { CloseHandle(proc) };
    // SAFETY: pseudo-handle for this process, always valid.
    let ours = user_sid(unsafe { GetCurrentProcess() });
    let (theirs, ours) = (theirs?, ours?);
    let sid = |b: &Vec<u8>| {
        // SAFETY: GetTokenInformation(TokenUser) filled the buffer with a TOKEN_USER.
        unsafe { (*(b.as_ptr() as *const TOKEN_USER)).User.Sid }
    };
    // SAFETY: both SIDs point into live buffers filled by the system.
    Some(unsafe { EqualSid(sid(&theirs), sid(&ours)) } != 0)
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

    #[test]
    fn this_test_runs_as_the_desktop_user() {
        // Tests run unelevated as the signed-in user (or with no desktop at all in CI).
        assert_ne!(shell_user_matches(), Some(false));
    }
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
