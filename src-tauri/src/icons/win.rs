//! Original icons from the local machine: Uninstall registry entries, icons embedded in program
//! files, and MSIX package logos. All functions are blocking; call them via `spawn_blocking`.

use crate::icons::image::to_png;
use crate::icons::sources::{choose_exe, msix_logo, parse_display_icon};
use image::RgbaImage;
use std::path::{Path, PathBuf};

#[derive(Debug, Clone)]
pub struct ArpEntry {
    pub name: String,
    pub display_icon: Option<String>,
    pub install_location: Option<String>,
}

/// Installed programs as Windows lists them (machine 64/32-bit and per-user).
pub fn arp_entries() -> Vec<ArpEntry> {
    use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, KEY_READ};
    use winreg::RegKey;
    let roots = [
        (
            HKEY_LOCAL_MACHINE,
            r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        ),
        (
            HKEY_LOCAL_MACHINE,
            r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ),
        (
            HKEY_CURRENT_USER,
            r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        ),
    ];
    let mut out = Vec::new();
    for (root, path) in roots {
        let Ok(key) = RegKey::predef(root).open_subkey_with_flags(path, KEY_READ) else {
            continue;
        };
        for sub in key.enum_keys().flatten() {
            let Ok(k) = key.open_subkey_with_flags(&sub, KEY_READ) else {
                continue;
            };
            let Ok(name) = k.get_value::<String, _>("DisplayName") else {
                continue;
            };
            let get = |v: &str| {
                k.get_value::<String, _>(v)
                    .ok()
                    .filter(|s| !s.trim().is_empty())
            };
            out.push(ArpEntry {
                name,
                display_icon: get("DisplayIcon"),
                install_location: get("InstallLocation"),
            });
        }
    }
    out
}

/// Expand `%VAR%` for reading an icon file (never used to choose something to execute).
pub fn expand_env(s: &str) -> String {
    use windows_sys::Win32::System::Environment::ExpandEnvironmentStringsW;
    if !s.contains('%') {
        return s.to_string();
    }
    let wide: Vec<u16> = s.encode_utf16().chain(Some(0)).collect();
    let mut buf = vec![0u16; 2048];
    // SAFETY: valid NUL-terminated input; output buffer length passed.
    let n = unsafe { ExpandEnvironmentStringsW(wide.as_ptr(), buf.as_mut_ptr(), buf.len() as u32) }
        as usize;
    if n == 0 || n > buf.len() {
        return s.to_string();
    }
    String::from_utf16_lossy(&buf[..n.saturating_sub(1)])
}

const ICON_FILE_CAP: u64 = 4 * 1024 * 1024;

/// Registry values (some user-writable) must not make the elevated app open network shares,
/// devices or removable media: only `X:\...` paths on fixed local drives are read.
pub fn safe_local_path(p: &Path) -> bool {
    use windows_sys::Win32::Storage::FileSystem::GetDriveTypeW;
    const DRIVE_FIXED: u32 = 3;
    let s = p.to_string_lossy();
    let b = s.as_bytes();
    if b.len() < 3 || !b[0].is_ascii_alphabetic() || b[1] != b':' || (b[2] != b'\\' && b[2] != b'/')
    {
        return false;
    }
    let root: Vec<u16> = format!("{}:\\", b[0] as char)
        .encode_utf16()
        .chain(Some(0))
        .collect();
    // SAFETY: valid NUL-terminated root path.
    unsafe { GetDriveTypeW(root.as_ptr()) == DRIVE_FIXED }
}

/// MSIX package full names are letters, digits, dots, dashes and underscores only.
pub fn valid_package_name(name: &str) -> bool {
    !name.is_empty()
        && name.len() <= 200
        && name
            .chars()
            .all(|c| c.is_ascii_alphanumeric() || matches!(c, '.' | '_' | '-'))
}

/// PNG of the icon at `path[,index]`: image files are decoded; executables/DLLs are extracted.
pub fn icon_from_file(path: &Path, index: i32, size: u32) -> Option<Vec<u8>> {
    if !safe_local_path(path) {
        return None;
    }
    let ext = path.extension()?.to_string_lossy().to_ascii_lowercase();
    if matches!(ext.as_str(), "ico" | "png" | "bmp" | "jpg" | "jpeg") {
        if std::fs::metadata(path).ok()?.len() > ICON_FILE_CAP {
            return None;
        }
        return to_png(&std::fs::read(path).ok()?, size).ok();
    }
    if !matches!(ext.as_str(), "exe" | "dll") {
        return None;
    }
    let img = extract_hicon(path, index, size)?;
    let mut out = std::io::Cursor::new(Vec::new());
    image::DynamicImage::ImageRgba8(img)
        .write_to(&mut out, image::ImageFormat::Png)
        .ok()?;
    Some(out.into_inner())
}

fn extract_hicon(path: &Path, index: i32, size: u32) -> Option<RgbaImage> {
    use windows_sys::Win32::UI::Shell::SHDefExtractIconW;
    use windows_sys::Win32::UI::WindowsAndMessaging::{DestroyIcon, HICON};
    let wide: Vec<u16> = path
        .to_string_lossy()
        .encode_utf16()
        .chain(Some(0))
        .collect();
    let mut large: HICON = std::ptr::null_mut();
    let mut small: HICON = std::ptr::null_mut();
    // SAFETY: valid path; the returned icons are destroyed below.
    unsafe {
        let hr = SHDefExtractIconW(
            wide.as_ptr(),
            index,
            0,
            &mut large,
            &mut small,
            (16 << 16) | size,
        );
        if !small.is_null() {
            DestroyIcon(small);
        }
        if hr != 0 || large.is_null() {
            return None;
        }
        let img = hicon_to_rgba(large);
        DestroyIcon(large);
        img
    }
}

/// HICON -> RGBA. Icons without an alpha channel get transparency from their mask.
unsafe fn hicon_to_rgba(
    icon: windows_sys::Win32::UI::WindowsAndMessaging::HICON,
) -> Option<RgbaImage> {
    use windows_sys::Win32::Graphics::Gdi::*;
    use windows_sys::Win32::UI::WindowsAndMessaging::{GetIconInfo, ICONINFO};
    let mut info: ICONINFO = std::mem::zeroed();
    if GetIconInfo(icon, &mut info) == 0 {
        return None;
    }
    let cleanup = |info: &ICONINFO| {
        if !info.hbmColor.is_null() {
            DeleteObject(info.hbmColor as _);
        }
        if !info.hbmMask.is_null() {
            DeleteObject(info.hbmMask as _);
        }
    };
    if info.hbmColor.is_null() {
        cleanup(&info);
        return None;
    }
    let mut bm: BITMAP = std::mem::zeroed();
    if GetObjectW(
        info.hbmColor as _,
        std::mem::size_of::<BITMAP>() as i32,
        (&mut bm as *mut BITMAP).cast(),
    ) == 0
    {
        cleanup(&info);
        return None;
    }
    let (w, h) = (bm.bmWidth, bm.bmHeight);
    if w <= 0 || h <= 0 || w > 1024 || h > 1024 {
        cleanup(&info);
        return None;
    }
    let read = |bitmap: HBITMAP| -> Option<Vec<u8>> {
        let mut bi: BITMAPINFO = std::mem::zeroed();
        bi.bmiHeader.biSize = std::mem::size_of::<BITMAPINFOHEADER>() as u32;
        bi.bmiHeader.biWidth = w;
        bi.bmiHeader.biHeight = -h; // top-down
        bi.bmiHeader.biPlanes = 1;
        bi.bmiHeader.biBitCount = 32;
        bi.bmiHeader.biCompression = BI_RGB;
        let mut buf = vec![0u8; (w * h * 4) as usize];
        let dc = GetDC(std::ptr::null_mut());
        let lines = GetDIBits(
            dc,
            bitmap,
            0,
            h as u32,
            buf.as_mut_ptr().cast(),
            &mut bi,
            DIB_RGB_COLORS,
        );
        ReleaseDC(std::ptr::null_mut(), dc);
        (lines != 0).then_some(buf)
    };
    let Some(mut bgra) = read(info.hbmColor) else {
        cleanup(&info);
        return None;
    };
    if bgra.as_chunks::<4>().0.iter().all(|p| p[3] == 0) {
        if let Some(mask) = read(info.hbmMask) {
            for (px, m) in bgra
                .as_chunks_mut::<4>()
                .0
                .iter_mut()
                .zip(mask.as_chunks::<4>().0.iter())
            {
                px[3] = if m[0] == 0 { 255 } else { 0 };
            }
        }
    }
    cleanup(&info);
    for px in bgra.as_chunks_mut::<4>().0.iter_mut() {
        px.swap(0, 2);
    }
    RgbaImage::from_raw(w as u32, h as u32, bgra)
}

/// Icon for a registry entry: its DisplayIcon, else the main program in its install folder.
pub fn icon_for_entry(e: &ArpEntry, size: u32) -> Option<Vec<u8>> {
    if let Some((path, index)) = e.display_icon.as_deref().and_then(parse_display_icon) {
        let path = PathBuf::from(expand_env(&path.to_string_lossy()));
        if let Some(png) = icon_from_file(&path, index, size) {
            return Some(png);
        }
    }
    let dir = PathBuf::from(expand_env(
        e.install_location.as_deref()?.trim().trim_matches('"'),
    ));
    main_exe(&dir, &e.name).and_then(|exe| icon_from_file(&exe, 0, size))
}

/// The app's main program in `dir` or one level below (e.g. `Acrobat DC\Acrobat\Acrobat.exe`).
pub fn main_exe(dir: &Path, app_name: &str) -> Option<PathBuf> {
    if !safe_local_path(dir) {
        return None;
    }
    let list = |d: &Path| -> Vec<String> {
        std::fs::read_dir(d)
            .map(|it| {
                it.flatten()
                    .filter(|e| e.path().is_file())
                    .map(|e| e.file_name().to_string_lossy().into_owned())
                    .collect()
            })
            .unwrap_or_default()
    };
    if let Some(name) = choose_exe(&list(dir), app_name) {
        return Some(dir.join(name));
    }
    let subdirs: Vec<PathBuf> = std::fs::read_dir(dir)
        .ok()?
        .flatten()
        .map(|e| e.path())
        .filter(|p| p.is_dir())
        .take(40)
        .collect();
    subdirs
        .into_iter()
        .find_map(|sub| choose_exe(&list(&sub), app_name).map(|n| sub.join(n)))
}

/// Logo of an installed MSIX package (`MSIX\<PackageFullName>` in winget's list).
pub fn msix_icon(package_full_name: &str, program_files: &Path, size: u32) -> Option<Vec<u8>> {
    if !valid_package_name(package_full_name) {
        return None;
    }
    let root = program_files.join("WindowsApps").join(package_full_name);
    let manifest = std::fs::read_to_string(root.join("AppxManifest.xml")).ok()?;
    let logo = PathBuf::from(msix_logo(&manifest)?.replace('/', "\\"));
    let dir = root.join(logo.parent().unwrap_or(Path::new("")));
    let stem = logo.file_stem()?.to_string_lossy().to_ascii_lowercase();
    // Packages ship scale variants (StoreLogo.scale-200.png); pick the largest file.
    let best = std::fs::read_dir(&dir)
        .ok()?
        .flatten()
        .filter(|e| {
            e.file_name()
                .to_string_lossy()
                .to_ascii_lowercase()
                .starts_with(&stem)
        })
        .max_by_key(|e| e.metadata().map(|m| m.len()).unwrap_or(0))?;
    icon_from_file(&best.path(), 0, size)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn png_side(png: &[u8]) -> u32 {
        image::load_from_memory(png).unwrap().width()
    }

    #[test]
    fn extracts_an_icon_from_a_system_exe() {
        let notepad = Path::new(r"C:\Windows\System32\notepad.exe");
        if !notepad.exists() {
            return;
        }
        let png = icon_from_file(notepad, 0, 128).expect("notepad has an icon");
        assert!(png_side(&png) >= 32);
        let img = image::load_from_memory(&png).unwrap().to_rgba8();
        assert!(
            img.pixels().any(|p| p[3] > 0),
            "icon must not be fully transparent"
        );
    }

    #[test]
    fn registry_lists_installed_programs() {
        assert!(!arp_entries().is_empty());
    }

    #[test]
    fn finds_real_icons_for_installed_apps_on_this_pc() {
        let entries = arp_entries();
        let mut found = 0;
        for e in entries.iter().filter(|e| e.display_icon.is_some()).take(15) {
            if icon_for_entry(e, 64).is_some() {
                found += 1;
            }
        }
        assert!(
            found >= 5,
            "only {found} of 15 entries with DisplayIcon produced an icon"
        );
    }

    #[test]
    fn unsafe_package_names_are_rejected() {
        assert!(msix_icon(r"..\..\Windows", Path::new(r"C:\Program Files"), 64).is_none());
    }
}

#[cfg(test)]
mod visual {
    use super::*;
    /// Manual check: `cargo test dump_icons -- --ignored` writes real icons to .playwright-mcp/icons.
    #[test]
    #[ignore]
    fn dump_icons() {
        let out = std::path::Path::new("../.playwright-mcp/icons");
        std::fs::create_dir_all(out).unwrap();
        let mut sheet =
            image::RgbaImage::from_pixel(8 * 72, 3 * 72, image::Rgba([246, 243, 238, 255]));
        let mut i = 0;
        for e in arp_entries() {
            if i >= 24 {
                break;
            }
            if let Some(png) = icon_for_entry(&e, 64) {
                let img = image::load_from_memory(&png).unwrap().to_rgba8();
                let img =
                    image::imageops::resize(&img, 64, 64, image::imageops::FilterType::Lanczos3);
                image::imageops::overlay(
                    &mut sheet,
                    &img,
                    ((i % 8) * 72 + 4) as i64,
                    ((i / 8) * 72 + 4) as i64,
                );
                println!("{i}: {}", e.name);
                i += 1;
            }
        }
        sheet.save(out.join("sheet.png")).unwrap();
    }
}

#[cfg(test)]
mod path_policy {
    use super::*;

    #[test]
    fn only_local_fixed_drive_paths_are_read() {
        assert!(safe_local_path(Path::new(
            r"C:\Windows\System32\notepad.exe"
        )));
        for bad in [
            r"\host\share\x.ico",
            r"\?\C:\x.ico",
            r"\.\pipe\x",
            r"relative\x.ico",
            r"Z:\nope\x.ico",
            r"C:x.ico",
        ] {
            assert!(!safe_local_path(Path::new(bad)), "{bad}");
        }
    }

    #[test]
    fn package_names_cannot_escape() {
        assert!(valid_package_name(
            "Microsoft.XboxIdentityProvider_12.130.16001.0_x64__8wekyb3d8bbwe"
        ));
        for bad in [r"..\x", "a/b", r"a\b", "C:evil", ""] {
            assert!(!valid_package_name(bad), "{bad}");
        }
    }
}
