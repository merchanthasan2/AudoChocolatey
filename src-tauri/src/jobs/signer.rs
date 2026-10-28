//! "Installer signed by …": a note about who signed the installer a job downloaded. Only a
//! note, never a gate, and never "safe" or "verified": anyone can sign as anyone, and only a
//! signature Windows trusts shows a name.

use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};
use std::time::SystemTime;
use ts_rs::TS;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase", tag = "kind")]
#[ts(export)]
pub enum Signer {
    /// Windows trusts the signature; `name` is the signer's display name.
    Signed {
        name: String,
    },
    Unsigned,
    /// Signed, but the signature or its certificate isn't trusted (expired, revoked, altered).
    Invalid,
}

/// The `WinVerifyTrust` result as a note. Anything unclear gives no note.
pub fn classify_trust(hr: i32) -> Option<Signer> {
    match hr as u32 {
        0 => Some(Signer::Signed {
            name: String::new(),
        }),
        0x800B_0100 => Some(Signer::Unsigned), // TRUST_E_NOSIGNATURE
        0x8009_6010 // TRUST_E_BAD_DIGEST
        | 0x800B_0109 // CERT_E_UNTRUSTEDROOT
        | 0x800B_0101 // CERT_E_EXPIRED
        | 0x800B_010C // CERT_E_REVOKED
        | 0x800B_0111 // TRUST_E_EXPLICIT_DISTRUST
        | 0x800B_010A // CERT_E_CHAINING
        | 0x8009_6004 // TRUST_E_CERT_SIGNATURE
        | 0x800B_0004 => Some(Signer::Invalid), // TRUST_E_SUBJECT_NOT_TRUSTED
        _ => None,
    }
}

/// A signer name fit to show: no control or direction-changing characters, at most 80.
pub fn clean_name(s: &str) -> String {
    s.chars()
        .filter(|c| {
            !c.is_control()
                && !matches!(*c as u32, 0x200E | 0x200F | 0x202A..=0x202E | 0x2066..=0x2069)
        })
        .take(80)
        .collect::<String>()
        .trim()
        .to_string()
}

const EXTS: [&str; 6] = ["exe", "msi", "msix", "msixbundle", "appx", "appxbundle"];
const MAX_DEPTH: usize = 4;
const REPARSE: u32 = 0x400;

fn is_reparse(m: &std::fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    m.file_attributes() & REPARSE != 0
}

/// The newest installer file under `roots` changed at or after `since`. Never follows links
/// or junctions, and looks at most four folders deep.
pub fn pick_installer(roots: &[PathBuf], since: SystemTime) -> Option<PathBuf> {
    let mut best: Option<(SystemTime, PathBuf)> = None;
    let mut stack: Vec<(PathBuf, usize)> = roots.iter().map(|r| (r.clone(), 0)).collect();
    while let Some((dir, depth)) = stack.pop() {
        let Ok(meta) = std::fs::symlink_metadata(&dir) else {
            continue;
        };
        if is_reparse(&meta) || !meta.is_dir() {
            continue;
        }
        let Ok(entries) = std::fs::read_dir(&dir) else {
            continue;
        };
        for e in entries.flatten() {
            let path = e.path();
            let Ok(m) = std::fs::symlink_metadata(&path) else {
                continue;
            };
            if is_reparse(&m) {
                continue;
            }
            if m.is_dir() {
                if depth + 1 < MAX_DEPTH {
                    stack.push((path, depth + 1));
                }
                continue;
            }
            let ext_ok = path
                .extension()
                .and_then(|x| x.to_str())
                .is_some_and(|x| EXTS.iter().any(|e| e.eq_ignore_ascii_case(x)));
            let Ok(modified) = m.modified() else {
                continue;
            };
            if !ext_ok || !m.is_file() || modified < since {
                continue;
            }
            if best.as_ref().is_none_or(|(t, _)| modified > *t) {
                best = Some((modified, path));
            }
        }
    }
    best.map(|(_, p)| p)
}

/// Where a job's installer can be: winget's `%TEMP%\WinGet\<id>.*` folders, or Chocolatey's
/// `%TEMP%\chocolatey\<id>` folder. Only this app's folders, so another job's file isn't named.
pub fn roots_for(temp: &Path, source: crate::ids::SourceKind, id: &str) -> Vec<PathBuf> {
    use crate::ids::SourceKind;
    match source {
        SourceKind::Winget => {
            let prefix = format!("{}.", id.to_ascii_lowercase());
            std::fs::read_dir(temp.join("WinGet"))
                .map(|rd| {
                    rd.flatten()
                        .filter(|e| {
                            e.file_name()
                                .to_str()
                                .is_some_and(|n| n.to_ascii_lowercase().starts_with(&prefix))
                        })
                        .map(|e| e.path())
                        .collect()
                })
                .unwrap_or_default()
        }
        SourceKind::Choco => vec![temp.join("chocolatey").join(id)],
        SourceKind::Msstore => Vec::new(),
    }
}

/// Checks one file's signature through a handle opened once (so the file checked is the file
/// found), after making sure it still lives under `root`. Blocking.
pub fn check(path: &Path, root: &Path) -> Option<Signer> {
    use windows_sys::Win32::Foundation::{CloseHandle, GENERIC_READ, INVALID_HANDLE_VALUE};
    use windows_sys::Win32::Storage::FileSystem::{
        CreateFileW, GetFinalPathNameByHandleW, FILE_FLAG_OPEN_REPARSE_POINT, FILE_SHARE_DELETE,
        FILE_SHARE_READ, OPEN_EXISTING,
    };
    let wide: Vec<u16> = path.as_os_str().encode_wide_nul();
    // SAFETY: NUL-terminated path; the handle is closed below.
    let h = unsafe {
        CreateFileW(
            wide.as_ptr(),
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_DELETE,
            std::ptr::null(),
            OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT,
            std::ptr::null_mut(),
        )
    };
    if h == INVALID_HANDLE_VALUE {
        return None;
    }
    let mut buf = vec![0u16; 1024];
    // SAFETY: valid handle and a writable buffer of the stated length.
    let n = unsafe { GetFinalPathNameByHandleW(h, buf.as_mut_ptr(), buf.len() as u32, 0) } as usize;
    let inside = n > 0 && n < buf.len() && {
        let real = String::from_utf16_lossy(&buf[..n]);
        let real = real.trim_start_matches(r"\\?\").to_ascii_lowercase();
        std::fs::canonicalize(root)
            .ok()
            .and_then(|r| {
                r.to_str()
                    .map(|s| s.trim_start_matches(r"\\?\").to_ascii_lowercase())
            })
            .is_some_and(|r| real.starts_with(&format!("{}\\", r.trim_end_matches('\\'))))
    };
    let out = if inside { verify(&wide, h) } else { None };
    // SAFETY: opened above.
    unsafe { CloseHandle(h) };
    out
}

/// Who signed this program, when Windows trusts the signature. Unlike `check` there is no
/// folder the file must live under. Blocking.
pub fn signer_name(path: &Path) -> Option<String> {
    use windows_sys::Win32::Foundation::{CloseHandle, GENERIC_READ, INVALID_HANDLE_VALUE};
    use windows_sys::Win32::Storage::FileSystem::{
        CreateFileW, FILE_FLAG_OPEN_REPARSE_POINT, FILE_SHARE_DELETE, FILE_SHARE_READ,
        OPEN_EXISTING,
    };
    let wide: Vec<u16> = path.as_os_str().encode_wide_nul();
    // SAFETY: NUL-terminated path; the handle is closed below.
    let h = unsafe {
        CreateFileW(
            wide.as_ptr(),
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_DELETE,
            std::ptr::null(),
            OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT,
            std::ptr::null_mut(),
        )
    };
    if h == INVALID_HANDLE_VALUE {
        return None;
    }
    let out = match verify(&wide, h) {
        Some(Signer::Signed { name }) => Some(name),
        _ => None,
    };
    // SAFETY: opened above.
    unsafe { CloseHandle(h) };
    out
}

trait WideNul {
    fn encode_wide_nul(&self) -> Vec<u16>;
}
impl WideNul for std::ffi::OsStr {
    fn encode_wide_nul(&self) -> Vec<u16> {
        use std::os::windows::ffi::OsStrExt;
        self.encode_wide().chain(Some(0)).collect()
    }
}

fn verify(path: &[u16], h: windows_sys::Win32::Foundation::HANDLE) -> Option<Signer> {
    use windows_sys::Win32::Security::Cryptography::{
        CertGetNameStringW, CERT_NAME_SIMPLE_DISPLAY_TYPE,
    };
    use windows_sys::Win32::Security::WinTrust::{
        WTHelperGetProvSignerFromChain, WTHelperProvDataFromStateData, WinVerifyTrust,
        WINTRUST_ACTION_GENERIC_VERIFY_V2, WINTRUST_DATA, WINTRUST_FILE_INFO,
        WTD_CACHE_ONLY_URL_RETRIEVAL, WTD_CHOICE_FILE, WTD_REVOKE_NONE, WTD_STATEACTION_CLOSE,
        WTD_STATEACTION_VERIFY, WTD_UI_NONE,
    };
    let mut file = WINTRUST_FILE_INFO {
        cbStruct: std::mem::size_of::<WINTRUST_FILE_INFO>() as u32,
        pcwszFilePath: path.as_ptr(),
        hFile: h,
        pgKnownSubject: std::ptr::null_mut(),
    };
    // SAFETY: a zeroed WINTRUST_DATA is valid; the fields WinVerifyTrust reads are set below.
    let mut data: WINTRUST_DATA = unsafe { std::mem::zeroed() };
    data.cbStruct = std::mem::size_of::<WINTRUST_DATA>() as u32;
    data.dwUIChoice = WTD_UI_NONE;
    data.fdwRevocationChecks = WTD_REVOKE_NONE;
    data.dwUnionChoice = WTD_CHOICE_FILE;
    data.Anonymous.pFile = &mut file;
    data.dwStateAction = WTD_STATEACTION_VERIFY;
    data.dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL;
    let mut action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
    let hwnd = -1isize as windows_sys::Win32::Foundation::HWND; // INVALID_HANDLE_VALUE: no UI
                                                                // SAFETY: `data` and `file` outlive both calls; state is closed with STATEACTION_CLOSE.
    let hr = unsafe { WinVerifyTrust(hwnd, &mut action, (&mut data as *mut WINTRUST_DATA).cast()) };
    let mut out = classify_trust(hr);
    if let Some(Signer::Signed { name }) = &mut out {
        // SAFETY: the state data is valid until CLOSE; every pointer is checked for null.
        unsafe {
            let prov = WTHelperProvDataFromStateData(data.hWVTStateData);
            let sgnr = if prov.is_null() {
                std::ptr::null_mut()
            } else {
                WTHelperGetProvSignerFromChain(prov, 0, 0, 0)
            };
            if !sgnr.is_null() && (*sgnr).csCertChain > 0 && !(*sgnr).pasCertChain.is_null() {
                let cert = (*(*sgnr).pasCertChain).pCert;
                if !cert.is_null() {
                    let mut buf = [0u16; 256];
                    let n = CertGetNameStringW(
                        cert,
                        CERT_NAME_SIMPLE_DISPLAY_TYPE,
                        0,
                        std::ptr::null(),
                        buf.as_mut_ptr(),
                        buf.len() as u32,
                    ) as usize;
                    if n > 1 {
                        *name = clean_name(&String::from_utf16_lossy(&buf[..n - 1]));
                    }
                }
            }
        }
        if name.is_empty() {
            out = None;
        }
    }
    data.dwStateAction = WTD_STATEACTION_CLOSE;
    // SAFETY: closes the state opened above.
    unsafe { WinVerifyTrust(hwnd, &mut action, (&mut data as *mut WINTRUST_DATA).cast()) };
    out
}

/// Looks up who signed the installer a job downloaded (a seam for tests).
pub trait SignerCheck: Send + Sync {
    /// Blocking.
    fn check(&self, target: &crate::ids::SourceRef, since: SystemTime) -> Option<Signer>;
}

pub struct SystemSigner {
    pub temp: PathBuf,
}

impl SignerCheck for SystemSigner {
    fn check(&self, target: &crate::ids::SourceRef, since: SystemTime) -> Option<Signer> {
        let roots = roots_for(&self.temp, target.source(), target.id());
        let file = pick_installer(&roots, since)?;
        let root = roots.iter().find(|r| file.starts_with(r))?;
        check(&file, root)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::Duration;

    #[test]
    fn trust_results_map_to_plain_notes() {
        assert!(matches!(classify_trust(0), Some(Signer::Signed { .. })));
        assert_eq!(classify_trust(0x800B0100u32 as i32), Some(Signer::Unsigned));
        for hr in [
            0x80096010u32,
            0x800B0109,
            0x800B0101,
            0x800B010C,
            0x800B0111,
        ] {
            assert_eq!(classify_trust(hr as i32), Some(Signer::Invalid), "{hr:x}");
        }
        for hr in [0x800B0003u32, 0x800B0001, 0x80070002, 1] {
            assert_eq!(classify_trust(hr as i32), None, "{hr:x}");
        }
    }

    #[test]
    fn names_are_cleaned() {
        assert_eq!(clean_name("Mozilla Corporation"), "Mozilla Corporation");
        assert_eq!(clean_name("Evil\u{202E}gnp.exe\n"), "Evilgnp.exe");
        assert_eq!(clean_name(&"x".repeat(200)).len(), 80);
    }

    #[test]
    fn picks_the_newest_installer_and_skips_the_rest() {
        let dir = tempfile::tempdir().unwrap();
        let root = dir.path().to_path_buf();
        let since = SystemTime::now() - Duration::from_secs(5);
        std::fs::create_dir_all(root.join("a/b/c/d/e")).unwrap();
        std::fs::write(root.join("notes.zip"), "x").unwrap();
        std::fs::write(root.join("setup.tmp"), "x").unwrap();
        std::fs::write(root.join("a/b/c/d/e/deep.exe"), "x").unwrap();
        std::fs::write(root.join("a/first.EXE"), "x").unwrap();
        std::thread::sleep(Duration::from_millis(30));
        std::fs::write(root.join("a/b/second.msi"), "x").unwrap();
        assert_eq!(
            pick_installer(std::slice::from_ref(&root), since),
            Some(root.join("a/b/second.msi"))
        );
        let later = SystemTime::now() + Duration::from_secs(60);
        assert_eq!(pick_installer(&[root], later), None, "older than the job");
        assert_eq!(pick_installer(&[dir.path().join("missing")], since), None);
    }

    #[test]
    fn junctions_are_never_followed() {
        let dir = tempfile::tempdir().unwrap();
        let outside = tempfile::tempdir().unwrap();
        std::fs::write(outside.path().join("other.exe"), "x").unwrap();
        let link = dir.path().join("link");
        let made = std::process::Command::new("cmd")
            .args(["/C", "mklink", "/J"])
            .arg(&link)
            .arg(outside.path())
            .output()
            .is_ok_and(|o| o.status.success());
        if !made {
            return; // no junctions on this file system
        }
        let since = SystemTime::now() - Duration::from_secs(60);
        assert_eq!(pick_installer(&[dir.path().to_path_buf()], since), None);
        assert_eq!(pick_installer(&[link], since), None);
    }

    #[test]
    fn an_unsigned_file_reads_as_unsigned_and_outside_files_are_refused() {
        let dir = tempfile::tempdir().unwrap();
        let f = dir.path().join("plain.exe");
        std::fs::write(&f, b"MZ not really a program").unwrap();
        let got = check(&f, dir.path());
        assert!(matches!(got, Some(Signer::Unsigned) | None), "{got:?}");
        let other = tempfile::tempdir().unwrap();
        assert_eq!(check(&f, other.path()), None, "not under the root");
    }

    #[test]
    fn a_signed_windows_program_shows_its_signer() {
        let edge = Path::new(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe");
        if !edge.exists() {
            return;
        }
        let root = edge.parent().unwrap();
        match check(edge, root) {
            Some(Signer::Signed { name }) => assert!(name.contains("Microsoft"), "{name}"),
            other => panic!("{other:?}"),
        }
    }
}
