//! Trusted executable locations for an always-elevated app.
//!
//! `%LOCALAPPDATA%\Microsoft\WindowsApps` is writable by the (non-admin) user, so a plain
//! `winget.exe` there could be planted by malware and then run elevated. We only accept the
//! alias if it is an App Execution Link whose target is the Store-installed App Installer under
//! `Program Files\WindowsApps` (writable only by TrustedInstaller). Chocolatey's location is read
//! from the machine-wide environment (HKLM) and the ProgramData known folder, never from the
//! user-overridable process environment.

use regex::Regex;
use std::path::{Path, PathBuf};
use std::sync::LazyLock;

const IO_REPARSE_TAG_APPEXECLINK: u32 = 0x8000_001B;

static TRUSTED_WINGET: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(
        r"(?i)^[A-Z]:\\Program Files\\WindowsApps\\Microsoft\.DesktopAppInstaller_[A-Za-z0-9._~]+_8wekyb3d8bbwe\\winget\.exe$",
    )
    .unwrap()
});

/// Target path from App Execution Link reparse data (version 3: package family, app id, target).
pub fn app_link_target(data: &[u8]) -> Option<String> {
    if data.len() < 6 || u32::from_le_bytes(data[..4].try_into().ok()?) != 3 {
        return None;
    }
    let wide: Vec<u16> = data[4..]
        .as_chunks::<2>()
        .0
        .iter()
        .map(|c| u16::from_le_bytes(*c))
        .collect();
    let mut parts = wide.split(|&c| c == 0).map(String::from_utf16_lossy);
    let (_family, _app_id) = (parts.next()?, parts.next()?);
    parts.next().filter(|t| !t.is_empty())
}

pub fn is_trusted_winget_target(path: &str) -> bool {
    !path.contains("..") && TRUSTED_WINGET.is_match(path)
}

/// Only absolute literal paths are accepted from registry values (no `%VAR%` expansion,
/// which would pull in user-controlled variables).
pub fn literal_path(value: Option<String>) -> Option<PathBuf> {
    let v = value?;
    let p = PathBuf::from(&v);
    (!v.contains('%') && p.is_absolute()).then_some(p)
}

fn wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

/// Reads the reparse point of `path` and returns its app-link target, if it is one.
pub fn read_app_link(path: &Path) -> Option<String> {
    use windows_sys::Win32::Foundation::{CloseHandle, INVALID_HANDLE_VALUE};
    use windows_sys::Win32::Storage::FileSystem::{
        CreateFileW, FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OPEN_REPARSE_POINT,
        FILE_READ_ATTRIBUTES, FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE, OPEN_EXISTING,
    };
    use windows_sys::Win32::System::Ioctl::FSCTL_GET_REPARSE_POINT;
    use windows_sys::Win32::System::IO::DeviceIoControl;

    let name = wide(&path.to_string_lossy());
    let mut buf = vec![0u8; 16 * 1024];
    let mut returned = 0u32;
    // SAFETY: valid NUL-terminated path; buffer and length match; handle closed below.
    let ok = unsafe {
        let h = CreateFileW(
            name.as_ptr(),
            FILE_READ_ATTRIBUTES,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            std::ptr::null(),
            OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS,
            std::ptr::null_mut(),
        );
        if h == INVALID_HANDLE_VALUE {
            return None;
        }
        let ok = DeviceIoControl(
            h,
            FSCTL_GET_REPARSE_POINT,
            std::ptr::null(),
            0,
            buf.as_mut_ptr().cast(),
            buf.len() as u32,
            &mut returned,
            std::ptr::null_mut(),
        );
        CloseHandle(h);
        ok
    };
    let n = returned as usize;
    if ok == 0 || n < 8 {
        return None;
    }
    // REPARSE_DATA_BUFFER: tag (u32), data length (u16), reserved (u16), data.
    let tag = u32::from_le_bytes(buf[..4].try_into().ok()?);
    let len = u16::from_le_bytes(buf[4..6].try_into().ok()?) as usize;
    if tag != IO_REPARSE_TAG_APPEXECLINK || 8 + len > n {
        return None;
    }
    app_link_target(&buf[8..8 + len])
}

/// A machine-wide (HKLM) environment variable, never the user's.
pub fn machine_env(name: &str) -> Option<String> {
    use windows_sys::Win32::System::Registry::{
        RegGetValueW, HKEY_LOCAL_MACHINE, RRF_NOEXPAND, RRF_RT_REG_EXPAND_SZ, RRF_RT_REG_SZ,
    };
    let key = wide(r"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
    let value = wide(name);
    let mut buf = vec![0u16; 1024];
    let mut bytes = (buf.len() * 2) as u32;
    // SAFETY: buffers are valid for the sizes passed; RegGetValueW NUL-terminates strings.
    let status = unsafe {
        RegGetValueW(
            HKEY_LOCAL_MACHINE,
            key.as_ptr(),
            value.as_ptr(),
            RRF_RT_REG_SZ | RRF_RT_REG_EXPAND_SZ | RRF_NOEXPAND,
            std::ptr::null_mut(),
            buf.as_mut_ptr().cast(),
            &mut bytes,
        )
    };
    if status != 0 {
        return None;
    }
    let chars = (bytes as usize / 2).saturating_sub(1).min(buf.len());
    Some(String::from_utf16_lossy(&buf[..chars])).filter(|s| !s.is_empty())
}

/// `C:\ProgramData` from the shell, not from the overridable %ProgramData% variable.
pub fn program_data_dir() -> Option<PathBuf> {
    use windows_sys::Win32::System::Com::CoTaskMemFree;
    use windows_sys::Win32::UI::Shell::{FOLDERID_ProgramData, SHGetKnownFolderPath};
    let mut out: windows_sys::core::PWSTR = std::ptr::null_mut();
    // SAFETY: SHGetKnownFolderPath allocates `out`, which we free with CoTaskMemFree.
    unsafe {
        let hr = SHGetKnownFolderPath(&FOLDERID_ProgramData, 0, std::ptr::null_mut(), &mut out);
        if hr != 0 || out.is_null() {
            return None;
        }
        let len = (0..).take_while(|&i| *out.add(i) != 0).count();
        let s = String::from_utf16_lossy(std::slice::from_raw_parts(out, len));
        CoTaskMemFree(out.cast());
        Some(PathBuf::from(s))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const LINK: &[u8] = include_bytes!("../../tests/fixtures/winget/appexeclink-winget.bin");

    #[test]
    fn parses_real_app_execution_link() {
        assert_eq!(
            app_link_target(LINK).as_deref(),
            Some(
                r"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.29.380.0_x64__8wekyb3d8bbwe\winget.exe"
            )
        );
    }

    #[test]
    fn rejects_garbage_and_wrong_version() {
        assert_eq!(app_link_target(&[]), None);
        assert_eq!(app_link_target(&[1, 0, 0, 0, b'a', 0]), None);
        let mut bad = LINK.to_vec();
        bad[0] = 9;
        assert_eq!(app_link_target(&bad), None);
    }

    #[test]
    fn only_the_store_installed_app_installer_is_trusted() {
        assert!(is_trusted_winget_target(
            r"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.29.380.0_x64__8wekyb3d8bbwe\winget.exe"
        ));
        assert!(is_trusted_winget_target(
            r"D:\program files\windowsapps\Microsoft.DesktopAppInstaller_1.30.1.0_arm64__8wekyb3d8bbwe\WINGET.EXE"
        ));
        for bad in [
            r"C:\Users\x\AppData\Local\Microsoft\WindowsApps\winget.exe",
            r"C:\Program Files\WindowsApps\Evil.Package_1.0_x64__8wekyb3d8bbwe\winget.exe",
            r"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.0_x64__deadbeef\winget.exe",
            r"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.0_x64__8wekyb3d8bbwe\..\..\evil\winget.exe",
            r"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.0_x64__8wekyb3d8bbwe\sub\winget.exe",
        ] {
            assert!(!is_trusted_winget_target(bad), "{bad}");
        }
    }

    #[test]
    fn machine_env_values_with_variables_are_refused() {
        assert_eq!(
            literal_path(Some(r"C:\ProgramData\chocolatey".into())),
            Some(r"C:\ProgramData\chocolatey".into())
        );
        assert_eq!(literal_path(Some(r"%USERPROFILE%\choco".into())), None);
        assert_eq!(literal_path(Some("relative".into())), None);
        assert_eq!(literal_path(None), None);
    }

    /// Live check on this machine: the real alias resolves to a trusted target.
    #[test]
    fn real_winget_alias_is_trusted_when_present() {
        let Some(local) = std::env::var_os("LOCALAPPDATA") else {
            return;
        };
        let alias = std::path::PathBuf::from(local).join(r"Microsoft\WindowsApps\winget.exe");
        if !alias.exists() {
            return;
        }
        let target = read_app_link(&alias).expect("alias should be an app execution link");
        assert!(is_trusted_winget_target(&target), "{target}");
    }
}
