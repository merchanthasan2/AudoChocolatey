use crate::error::AppError;
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

/// Is this app running as the Windows user signed in to its session? `None` when nobody is
/// signed in yet. False means "over-the-shoulder" admin: per-user installs (Store apps) would
/// land in the admin's account, not the person's at the keyboard. Uses the session's sign-in
/// name only; no other process is opened.
pub fn shell_user_matches() -> Option<bool> {
    use windows_sys::Win32::Security::Authentication::Identity::{
        GetUserNameExW, NameSamCompatible,
    };
    use windows_sys::Win32::System::RemoteDesktop::{
        ProcessIdToSessionId, WTSDomainName, WTSFreeMemory, WTSQuerySessionInformationW,
        WTSUserName, WTS_CURRENT_SERVER_HANDLE, WTS_INFO_CLASS,
    };

    fn session_text(session: u32, class: WTS_INFO_CLASS) -> Option<String> {
        let mut buf: *mut u16 = std::ptr::null_mut();
        let mut bytes = 0u32;
        // SAFETY: out-pointers are valid; the buffer is freed with WTSFreeMemory below.
        let ok = unsafe {
            WTSQuerySessionInformationW(
                WTS_CURRENT_SERVER_HANDLE,
                session,
                class,
                &mut buf,
                &mut bytes,
            )
        };
        if ok == 0 || buf.is_null() {
            return None;
        }
        // SAFETY: the system returned `bytes` bytes of UTF-16 (including the terminator).
        let text = unsafe { std::slice::from_raw_parts(buf, (bytes as usize) / 2) };
        let s = String::from_utf16_lossy(text)
            .trim_end_matches(char::from(0))
            .to_string();
        // SAFETY: buf came from WTSQuerySessionInformationW.
        unsafe { WTSFreeMemory(buf.cast()) };
        Some(s)
    }

    let mut session = 0u32;
    // SAFETY: valid out-pointer.
    if unsafe { ProcessIdToSessionId(std::process::id(), &mut session) } == 0 {
        return None;
    }
    let user = session_text(session, WTSUserName)?;
    if user.is_empty() {
        return None;
    }
    let domain = session_text(session, WTSDomainName).unwrap_or_default();
    let signed_in = format!("{domain}\\{user}");

    let mut buf = [0u16; 512];
    let mut len = buf.len() as u32;
    // SAFETY: buffer and length describe writable memory.
    if unsafe { GetUserNameExW(NameSamCompatible, buf.as_mut_ptr(), &mut len) } == 0 {
        return None;
    }
    let ours = String::from_utf16_lossy(&buf[..len as usize]);
    Some(ours.eq_ignore_ascii_case(&signed_in))
}

/// Restarts the app through the standard Windows admin prompt (UAC). The person decides; if
/// they say no, this app keeps running and nothing changes.
#[tauri::command]
pub fn relaunch_as_admin(app: tauri::AppHandle) -> Result<(), AppError> {
    use std::os::windows::ffi::OsStrExt;
    use windows_sys::Win32::UI::Shell::ShellExecuteW;
    use windows_sys::Win32::UI::WindowsAndMessaging::SW_SHOWNORMAL;
    let exe = std::env::current_exe()
        .map_err(|e| AppError::new("io", format!("can't find the app's own file: {e}")))?;
    let wide = |s: &std::ffi::OsStr| {
        s.encode_wide()
            .chain(std::iter::once(0))
            .collect::<Vec<u16>>()
    };
    let (verb, file) = (wide(std::ffi::OsStr::new("runas")), wide(exe.as_os_str()));
    // SAFETY: both strings are NUL-terminated UTF-16 that outlive the call.
    let r = unsafe {
        ShellExecuteW(
            std::ptr::null_mut(),
            verb.as_ptr(),
            file.as_ptr(),
            std::ptr::null(),
            std::ptr::null(),
            SW_SHOWNORMAL,
        )
    };
    // ShellExecute returns a value above 32 on success; below means declined or failed.
    if (r as isize) <= 32 {
        return Err(AppError::new(
            "declined",
            "Admin access wasn't granted, so nothing changed.",
        ));
    }
    app.exit(0);
    Ok(())
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
    fn this_test_runs_as_the_signed_in_user() {
        // Tests run as the signed-in user (or with nobody signed in, in CI).
        assert_ne!(shell_user_matches(), Some(false));
        if std::env::var("USERNAME").is_ok() && std::env::var("CI").is_err() {
            assert_eq!(shell_user_matches(), Some(true));
        }
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
