//! "VLC is open. Close it and update?" Finds an app's running programs (only those started from
//! its own install folder) and asks them to close the polite way, as clicking X would. Nothing
//! is ever closed without the person saying so, and nothing is ever force-killed.

use std::path::{Path, PathBuf};
use std::time::{Duration, Instant};

/// A Programs-list entry, as far as finding an app's folder goes.
#[derive(Debug, Clone)]
pub struct ArpInfo {
    pub name: String,
    pub install_location: Option<String>,
    pub display_icon: Option<String>,
}

fn alnum(s: &str) -> String {
    s.chars()
        .filter(|c| c.is_alphanumeric())
        .flat_map(char::to_lowercase)
        .collect()
}

/// The name without a trailing version ("VLC media player 3.0.21" -> "VLC media player").
fn without_version(s: &str) -> &str {
    let t = s.trim_end();
    match t.rfind(' ') {
        Some(i)
            if t[i + 1..]
                .trim_start_matches('v')
                .starts_with(|c: char| c.is_ascii_digit()) =>
        {
            t[..i].trim_end()
        }
        _ => t,
    }
}

/// A folder that can belong to one app: absolute, at least two levels below the drive, and
/// not a shared Windows or profile folder.
pub fn app_folder_ok(p: &Path, shared: &[PathBuf]) -> bool {
    if !p.is_absolute() || p.components().count() < 3 {
        return false;
    }
    let low = p.to_string_lossy().to_lowercase();
    let low = low.trim_end_matches('\\');
    !shared.iter().any(|s| {
        let s = s.to_string_lossy().to_lowercase();
        let s = s.trim_end_matches('\\');
        low == s || s.starts_with(&format!("{low}\\"))
    }) && !low.starts_with(
        &std::env::var("SystemRoot")
            .unwrap_or_else(|_| r"C:\Windows".into())
            .to_lowercase(),
    )
}

/// The app's install folder: from exactly one Programs entry whose name is the app's name.
pub fn install_folder(app_name: &str, arp: &[ArpInfo], shared: &[PathBuf]) -> Option<PathBuf> {
    let want = alnum(without_version(app_name));
    if want.len() < 3 {
        return None;
    }
    let hits: Vec<&ArpInfo> = arp
        .iter()
        .filter(|e| alnum(without_version(&e.name)) == want)
        .collect();
    let [e] = hits.as_slice() else {
        return None;
    };
    let folder = e
        .install_location
        .as_deref()
        .map(|l| PathBuf::from(l.trim().trim_matches('"')))
        .or_else(|| {
            let (icon, _) = crate::icons::sources::parse_display_icon(e.display_icon.as_deref()?)?;
            icon.parent().map(Path::to_path_buf)
        })?;
    app_folder_ok(&folder, shared).then_some(folder)
}

/// Folders that hold many apps, never treated as one app's folder.
pub fn shared_folders() -> Vec<PathBuf> {
    [
        "ProgramFiles",
        "ProgramFiles(x86)",
        "ProgramData",
        "USERPROFILE",
        "APPDATA",
        "LOCALAPPDATA",
        "PUBLIC",
    ]
    .iter()
    .filter_map(|v| std::env::var_os(v).map(PathBuf::from))
    .chain(std::env::var_os("LOCALAPPDATA").map(|l| PathBuf::from(l).join("Programs")))
    .collect()
}

/// Running programs whose file is inside `folder` (process id and file name).
pub fn processes_in(folder: &Path) -> Vec<(u32, String)> {
    use windows_sys::Win32::Foundation::{CloseHandle, INVALID_HANDLE_VALUE};
    use windows_sys::Win32::System::Diagnostics::ToolHelp::{
        CreateToolhelp32Snapshot, Process32FirstW, Process32NextW, PROCESSENTRY32W,
        TH32CS_SNAPPROCESS,
    };
    use windows_sys::Win32::System::Threading::{
        OpenProcess, QueryFullProcessImageNameW, PROCESS_QUERY_LIMITED_INFORMATION,
    };
    let me = std::process::id();
    let prefix = format!(
        "{}\\",
        folder
            .to_string_lossy()
            .to_lowercase()
            .trim_end_matches('\\')
    );
    let mut out = Vec::new();
    // SAFETY: snapshot handle closed below; the entry struct is sized before use.
    unsafe {
        let snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if snap == INVALID_HANDLE_VALUE {
            return out;
        }
        let mut e: PROCESSENTRY32W = std::mem::zeroed();
        e.dwSize = std::mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut ok = Process32FirstW(snap, &mut e) != 0;
        while ok {
            let pid = e.th32ProcessID;
            if pid != me && pid != 0 {
                let h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid);
                if !h.is_null() {
                    let mut buf = [0u16; 1024];
                    let mut len = buf.len() as u32;
                    if QueryFullProcessImageNameW(h, 0, buf.as_mut_ptr(), &mut len) != 0 {
                        let path = String::from_utf16_lossy(&buf[..len as usize]);
                        if path.to_lowercase().starts_with(&prefix) {
                            let name = Path::new(&path)
                                .file_name()
                                .map(|n| n.to_string_lossy().into_owned())
                                .unwrap_or_default();
                            out.push((pid, name));
                        }
                    }
                    CloseHandle(h);
                }
            }
            ok = Process32NextW(snap, &mut e) != 0;
        }
        CloseHandle(snap);
    }
    out
}

/// Asks the windows of these processes to close (as the X button does), then waits up to
/// `wait` for them to exit. Returns the ones still running.
pub fn close_politely(pids: &[u32], wait: Duration) -> Vec<u32> {
    use windows_sys::Win32::Foundation::{BOOL, HWND, LPARAM};
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        EnumWindows, GetWindowThreadProcessId, IsWindowVisible, PostMessageW, WM_CLOSE,
    };
    unsafe extern "system" fn each(hwnd: HWND, lp: LPARAM) -> BOOL {
        // SAFETY: lp is the &Vec<u32> passed below, alive for the EnumWindows call.
        let pids = unsafe { &*(lp as *const Vec<u32>) };
        let mut pid = 0u32;
        // SAFETY: hwnd comes from EnumWindows.
        unsafe {
            GetWindowThreadProcessId(hwnd, &mut pid);
            if pids.contains(&pid) && IsWindowVisible(hwnd) != 0 {
                PostMessageW(hwnd, WM_CLOSE, 0, 0);
            }
        }
        1
    }
    let list: Vec<u32> = pids.to_vec();
    // SAFETY: the callback only reads `list`, which outlives the call.
    unsafe { EnumWindows(Some(each), &list as *const Vec<u32> as LPARAM) };
    let start = Instant::now();
    loop {
        let alive: Vec<u32> = list.iter().copied().filter(|p| alive(*p)).collect();
        if alive.is_empty() || start.elapsed() >= wait {
            return alive;
        }
        std::thread::sleep(Duration::from_millis(250));
    }
}

fn alive(pid: u32) -> bool {
    use windows_sys::Win32::Foundation::{CloseHandle, WAIT_TIMEOUT};
    use windows_sys::Win32::System::Threading::{
        OpenProcess, WaitForSingleObject, PROCESS_SYNCHRONIZE,
    };
    // SAFETY: handle closed below.
    unsafe {
        let h = OpenProcess(PROCESS_SYNCHRONIZE, 0, pid);
        if h.is_null() {
            return false;
        }
        let r = WaitForSingleObject(h, 0);
        CloseHandle(h);
        r == WAIT_TIMEOUT
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn shared() -> Vec<PathBuf> {
        vec![
            PathBuf::from(r"C:\Program Files"),
            PathBuf::from(r"C:\Program Files (x86)"),
            PathBuf::from(r"C:\Users\me"),
            PathBuf::from(r"C:\Users\me\AppData\Local"),
            PathBuf::from(r"C:\Users\me\AppData\Local\Programs"),
        ]
    }

    fn arp(name: &str, loc: Option<&str>, icon: Option<&str>) -> ArpInfo {
        ArpInfo {
            name: name.into(),
            install_location: loc.map(String::from),
            display_icon: icon.map(String::from),
        }
    }

    #[test]
    fn finds_the_folder_of_exactly_one_matching_entry() {
        let list = vec![
            arp(
                "VLC media player",
                Some(r"C:\Program Files\VideoLAN\VLC"),
                None,
            ),
            arp(
                "Notepad++ (64-bit x64)",
                None,
                Some(r"C:\Program Files\Notepad++\notepad++.exe,0"),
            ),
            arp("Twin", Some(r"C:\Program Files\A"), None),
            arp("Twin", Some(r"C:\Program Files\B"), None),
            arp("Broad", Some(r"C:\Program Files"), None),
            arp("Profile", Some(r"C:\Users\me"), None),
            arp(
                "Spotify",
                Some(r"C:\Users\me\AppData\Roaming\Spotify"),
                None,
            ),
        ];
        let s = shared();
        assert_eq!(
            install_folder("VLC media player 3.0.21", &list, &s),
            Some(PathBuf::from(r"C:\Program Files\VideoLAN\VLC"))
        );
        assert_eq!(
            install_folder("Notepad++ (64-bit x64)", &list, &s),
            Some(PathBuf::from(r"C:\Program Files\Notepad++"))
        );
        assert_eq!(install_folder("Twin", &list, &s), None, "two candidates");
        assert_eq!(install_folder("Broad", &list, &s), None, "a shared folder");
        assert_eq!(install_folder("Profile", &list, &s), None);
        assert_eq!(install_folder("Unknown App", &list, &s), None);
        assert!(install_folder("Spotify", &list, &s).is_some());
    }

    #[test]
    fn shared_and_windows_folders_are_refused() {
        let s = shared();
        assert!(!app_folder_ok(Path::new(r"C:\Program Files"), &s));
        assert!(!app_folder_ok(Path::new(r"C:\"), &s));
        assert!(!app_folder_ok(Path::new(r"C:\Windows\System32"), &s));
        assert!(
            !app_folder_ok(Path::new(r"C:\Users"), &s),
            "contains a shared folder"
        );
        assert!(!app_folder_ok(Path::new(r"relative\path"), &s));
        assert!(app_folder_ok(
            Path::new(r"C:\Program Files\VideoLAN\VLC"),
            &s
        ));
    }

    #[test]
    fn this_test_process_is_found_in_its_own_folder_but_never_itself() {
        let exe = std::env::current_exe().unwrap();
        let found = processes_in(exe.parent().unwrap());
        assert!(found.iter().all(|(pid, _)| *pid != std::process::id()));
    }
}
