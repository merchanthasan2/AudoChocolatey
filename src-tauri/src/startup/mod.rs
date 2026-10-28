//! Apps that start with Windows. Turning one off only marks it disabled (the same switch Task
//! Manager uses). The Run value, the shortcut, and the scheduled task are never deleted.
//! Windows, security software, policy-held items, and this app's own sign-in task are read-only,
//! and a write is refused again after a fresh read so the page cannot talk us into one of those.

mod machine;

use serde::{Deserialize, Serialize};
use ts_rs::TS;

pub use machine::{apply, collect};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum StartupPlace {
    /// The signed-in person's Run key.
    RunHkcu,
    /// The machine-wide Run key.
    RunHklm,
    /// The 32-bit machine-wide Run key.
    RunHklm32,
    /// The person's Startup folder.
    FolderUser,
    /// The Startup folder shared by everyone on this PC.
    FolderCommon,
    /// A scheduled task whose only trigger is sign-in.
    Task,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum StartupLock {
    Windows,
    Security,
    /// This app's own "start at sign-in", which Settings owns.
    Settings,
    Policy,
    /// The command could not be read. Machine-wide items stay put rather than be guessed.
    Unchecked,
}

impl StartupLock {
    pub fn message(self) -> &'static str {
        match self {
            Self::Windows => "Windows manages this, so it can't be changed here.",
            Self::Security => "Security software stays on.",
            Self::Settings => "Change this in Settings.",
            Self::Policy => "Managed by policy, so it can't be changed here.",
            Self::Unchecked => "This couldn't be checked, so it stays as it is.",
        }
    }
}

/// What the page sends back. The label and the lock are never taken from here; both are read again.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct StartupRef {
    pub place: StartupPlace,
    pub name: String,
    pub folder: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct StartupItem {
    pub place: StartupPlace,
    pub name: String,
    pub folder: String,
    pub label: String,
    pub detail: String,
    pub enabled: bool,
    pub lock: Option<StartupLock>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct StartupList {
    pub items: Vec<StartupItem>,
    /// Set when sign-in tasks could not be read. The rest of the list is still shown.
    pub warning: Option<String>,
    /// False when a different administrator is signed in, so per-user items were left out.
    pub per_user: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Approved {
    pub enabled: bool,
    pub policy: bool,
}

/// `StartupApproved` is 12 bytes. A missing value means Windows runs it. An odd first byte is
/// off (3 is the person, 1 and 7 are policy). 6 is on, but held by policy, so it is not ours to flip.
pub fn approved_state(bytes: Option<&[u8]>) -> Approved {
    let Some(b) = bytes.filter(|b| !b.is_empty()) else {
        return Approved {
            enabled: true,
            policy: false,
        };
    };
    Approved {
        enabled: b[0] % 2 == 0,
        policy: matches!(b[0], 0x01 | 0x06 | 0x07),
    }
}

/// The bytes Task Manager writes. Off keeps a timestamp; on is the plain "enabled" marker.
pub fn approved_bytes(enabled: bool, filetime: [u8; 8]) -> [u8; 12] {
    let mut out = [0u8; 12];
    if enabled {
        out[0] = 0x02;
    } else {
        out[0] = 0x03;
        out[4..].copy_from_slice(&filetime);
    }
    out
}

pub fn filetime_now() -> [u8; 8] {
    const UNIX_EPOCH_AS_FILETIME: u64 = 116_444_736_000_000_000;
    let ticks = std::time::SystemTime::now()
        .duration_since(std::time::SystemTime::UNIX_EPOCH)
        .map(|d| d.as_nanos() / 10)
        .unwrap_or(0);
    ((ticks as u64).saturating_add(UNIX_EPOCH_AS_FILETIME)).to_le_bytes()
}

/// The program a Run command or a shortcut target launches. Quoted paths keep their spaces.
pub fn exe_from_command(command: &str) -> Option<String> {
    let s = command.trim();
    if s.is_empty() {
        return None;
    }
    if let Some(rest) = s.strip_prefix('"') {
        let end = rest.find('"')?;
        let path = &rest[..end];
        return (!path.is_empty()).then(|| path.to_string());
    }
    let lower = s.to_ascii_lowercase();
    if let Some(i) = lower.find(".exe") {
        let path = s[..i + 4].trim();
        return (!path.is_empty()).then(|| path.to_string());
    }
    s.split_whitespace().next().map(str::to_string)
}

pub fn place_is_per_user(place: StartupPlace) -> bool {
    matches!(place, StartupPlace::RunHkcu | StartupPlace::FolderUser)
}

/// A name the page may ask us to change. Rejects anything that is not one real entry: a path
/// outside the Startup folder, a Windows task folder, or a `..` segment.
pub fn ref_ok(place: StartupPlace, name: &str, folder: &str) -> bool {
    if name.is_empty()
        || name.len() > 255
        || folder.len() > 400
        || has_control(name)
        || has_control(folder)
    {
        return false;
    }
    match place {
        StartupPlace::RunHkcu | StartupPlace::RunHklm | StartupPlace::RunHklm32 => {
            folder.is_empty() && !name.contains(['\\', '/'])
        }
        StartupPlace::FolderUser | StartupPlace::FolderCommon => {
            folder.is_empty()
                && !name.contains(['\\', '/', ':'])
                && name.to_ascii_lowercase().ends_with(".lnk")
                && name.len() > 4
        }
        StartupPlace::Task => task_folder_ok(folder) && !name.contains(['\\', '/']),
    }
}

fn has_control(s: &str) -> bool {
    s.chars().any(|c| c.is_control())
}

fn task_folder_ok(folder: &str) -> bool {
    if folder == "\\" {
        return true;
    }
    if !folder.starts_with('\\') || folder.ends_with('\\') || folder.contains('/') {
        return false;
    }
    let low = folder.to_lowercase();
    if low == "\\microsoft" || low.starts_with("\\microsoft\\") {
        return false;
    }
    folder
        .trim_start_matches('\\')
        .split('\\')
        .all(|p| !p.is_empty() && p != "." && p != "..")
}

pub fn task_path(folder: &str, name: &str) -> String {
    if folder == "\\" {
        format!("\\{name}")
    } else {
        format!("{folder}\\{name}")
    }
}

pub struct Classified<'a> {
    pub label: &'a str,
    pub command: &'a str,
    pub place: StartupPlace,
    pub task_folder: &'a str,
    pub windows_dir: &'a str,
}

pub fn lock_for(c: &Classified<'_>) -> Option<StartupLock> {
    if c.place == StartupPlace::Task {
        let folder = c.task_folder.to_lowercase();
        if folder == "\\quantumstep" {
            return Some(StartupLock::Settings);
        }
        if folder == "\\microsoft" || folder.starts_with("\\microsoft\\") {
            return Some(StartupLock::Windows);
        }
    }
    let hay = format!("{} {}", c.label, c.command).to_lowercase();
    if is_security(&hay) {
        return Some(StartupLock::Security);
    }
    let exe = exe_from_command(c.command).unwrap_or_default();
    if under_windows(&exe, c.windows_dir) {
        return Some(StartupLock::Windows);
    }
    None
}

pub fn label_for(place: StartupPlace, name: &str) -> String {
    match place {
        StartupPlace::FolderUser | StartupPlace::FolderCommon => {
            let n = name.len().saturating_sub(4);
            if name.to_ascii_lowercase().ends_with(".lnk") {
                name[..n].to_string()
            } else {
                name.to_string()
            }
        }
        _ => name.to_string(),
    }
}

pub fn detail_for(place: StartupPlace, command: &str) -> String {
    let kind = match place {
        StartupPlace::RunHkcu | StartupPlace::RunHklm | StartupPlace::RunHklm32 => {
            "Sign-in setting"
        }
        StartupPlace::FolderUser | StartupPlace::FolderCommon => "Startup folder",
        StartupPlace::Task => "Sign-in task",
    };
    let file = exe_from_command(command)
        .unwrap_or_default()
        .rsplit(['\\', '/'])
        .next()
        .unwrap_or("")
        .trim_matches('"')
        .to_string();
    if file.is_empty() {
        kind.to_string()
    } else {
        format!("{kind} · {file}")
    }
}

pub fn sort_items(items: &mut [StartupItem]) {
    items.sort_by(|a, b| {
        a.lock
            .is_some()
            .cmp(&b.lock.is_some())
            .then_with(|| a.label.to_lowercase().cmp(&b.label.to_lowercase()))
            .then_with(|| a.name.to_lowercase().cmp(&b.name.to_lowercase()))
            .then_with(|| a.place_ord().cmp(&b.place_ord()))
    });
}

impl StartupItem {
    fn place_ord(&self) -> u8 {
        match self.place {
            StartupPlace::RunHkcu => 0,
            StartupPlace::RunHklm => 1,
            StartupPlace::RunHklm32 => 2,
            StartupPlace::FolderUser => 3,
            StartupPlace::FolderCommon => 4,
            StartupPlace::Task => 5,
        }
    }
}

fn under_windows(exe: &str, windows_dir: &str) -> bool {
    let win = windows_dir.trim_end_matches(['\\', '/']).to_lowercase();
    if win.is_empty() {
        return false;
    }
    let mut path = exe.trim().trim_matches('"').to_lowercase();
    for token in ["%systemroot%", "%windir%"] {
        path = path.replace(token, &win);
    }
    path.starts_with(&format!("{win}\\")) || path.starts_with(&format!("{win}/"))
}

fn is_security(hay: &str) -> bool {
    const PHRASES: &[&str] = &[
        "kaspersky",
        "bitdefender",
        "symantec",
        "mcafee",
        "malwarebytes",
        "sophos",
        "trend micro",
        "trendmicro",
        "crowdstrike",
        "sentinelone",
        "sentinel agent",
        "carbon black",
        "carbonblack",
        "cylance",
        "webroot",
        "avira",
        "panda security",
        "f-secure",
        "withsecure",
        "comodo",
        "zonealarm",
        "emsisoft",
        "vipre",
        "hitmanpro",
        "windows defender",
        "microsoft defender",
        "securityhealth",
        "msmpeng",
        "smartscreen",
        "antimalware",
        "anti-malware",
        "antivirus",
        "anti-virus",
        "mpcmdrun",
        "windefend",
        "norton",
    ];
    // Short names that are also ordinary English fragments ("average", "preset") match only as
    // their own word, or as a folder.
    const TOKENS: &[&str] = &["avg", "eset", "avast", "gdata"];
    if PHRASES.iter().any(|p| hay.contains(p)) {
        return true;
    }
    let tokens: Vec<&str> = hay
        .split(|c: char| !c.is_ascii_alphanumeric())
        .filter(|w| !w.is_empty())
        .collect();
    tokens.iter().any(|w| TOKENS.contains(w))
        || hay.split(['\\', '/']).any(|seg| {
            let seg = seg.trim();
            TOKENS.contains(&seg)
        })
}

/// Local path stored in a `.lnk`. Unicode wins over the older ANSI path. Anything we do not
/// fully understand returns nothing, and the caller decides from the file name alone.
pub fn lnk_target(data: &[u8]) -> Option<String> {
    if data.len() < 0x4C || data.len() > 256 * 1024 {
        return None;
    }
    let header = u32::from_le_bytes(data.get(0..4)?.try_into().ok()?) as usize;
    if !(0x4C..=data.len()).contains(&header) {
        return None;
    }
    let flags = u32::from_le_bytes(data.get(0x14..0x18)?.try_into().ok()?);
    let mut off = header;
    if flags & 0x1 != 0 {
        let size = u16::from_le_bytes(data.get(off..off + 2)?.try_into().ok()?) as usize;
        off = off.checked_add(2)?.checked_add(size)?;
    }
    if flags & 0x2 == 0 || off.saturating_add(0x1C) > data.len() {
        return None;
    }
    let info_size = u32::from_le_bytes(data.get(off..off + 4)?.try_into().ok()?) as usize;
    let info_header = u32::from_le_bytes(data.get(off + 4..off + 8)?.try_into().ok()?) as usize;
    let info_flags = u32::from_le_bytes(data.get(off + 8..off + 12)?.try_into().ok()?);
    if info_size < 0x1C || info_header < 0x1C || off.checked_add(info_size)? > data.len() {
        return None;
    }
    if info_flags & 0x1 == 0 {
        return None;
    }
    let info = &data[off..off + info_size];
    if info_header >= 0x24 {
        let uoff = u32::from_le_bytes(info.get(0x1C..0x20)?.try_into().ok()?) as usize;
        if let Some(s) = utf16z(info, uoff) {
            return Some(s);
        }
    }
    let aoff = u32::from_le_bytes(info.get(0x10..0x14)?.try_into().ok()?) as usize;
    cstr(info, aoff)
}

fn utf16z(buf: &[u8], offset: usize) -> Option<String> {
    if offset == 0 || offset >= buf.len() || !offset.is_multiple_of(2) {
        return None;
    }
    let mut units = Vec::new();
    let mut i = offset;
    while i + 1 < buf.len() {
        let u = u16::from_le_bytes([buf[i], buf[i + 1]]);
        if u == 0 {
            let s = String::from_utf16(&units).ok()?;
            return (!s.is_empty() && s.len() <= 1024).then_some(s);
        }
        units.push(u);
        if units.len() > 1024 {
            return None;
        }
        i += 2;
    }
    None
}

fn cstr(buf: &[u8], offset: usize) -> Option<String> {
    if offset == 0 || offset >= buf.len() {
        return None;
    }
    let end = buf[offset..].iter().position(|b| *b == 0)?;
    let bytes = &buf[offset..offset + end];
    if bytes.is_empty() || bytes.len() > 1024 || !bytes.is_ascii() {
        return None;
    }
    String::from_utf8(bytes.to_vec()).ok()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn class<'a>(
        label: &'a str,
        command: &'a str,
        place: StartupPlace,
        folder: &'a str,
    ) -> Classified<'a> {
        Classified {
            label,
            command,
            place,
            task_folder: folder,
            windows_dir: r"C:\Windows",
        }
    }

    #[test]
    fn approved_bytes_match_task_manager() {
        assert_eq!(
            approved_state(None),
            Approved {
                enabled: true,
                policy: false
            }
        );
        assert!(approved_state(Some(&[0x02, 0, 0, 0])).enabled);
        let off = approved_bytes(false, [1, 2, 3, 4, 5, 6, 7, 8]);
        assert_eq!(off[0], 0x03);
        assert_eq!(&off[4..], &[1, 2, 3, 4, 5, 6, 7, 8]);
        assert!(!approved_state(Some(&off)).enabled);
        assert!(!approved_state(Some(&off)).policy);
        assert!(approved_state(Some(&[0x01])).policy);
        assert!(approved_state(Some(&[0x06])).policy && approved_state(Some(&[0x06])).enabled);
        assert!(approved_state(Some(&[0x07])).policy && !approved_state(Some(&[0x07])).enabled);
        assert_eq!(approved_bytes(true, [9; 8])[0], 0x02);
        assert!(approved_bytes(true, [9; 8])[4..].iter().all(|b| *b == 0));
    }

    #[test]
    fn command_keeps_a_quoted_path() {
        assert_eq!(
            exe_from_command(r#""C:\Program Files\VideoLAN\VLC\vlc.exe" --started-from-file"#),
            Some(r"C:\Program Files\VideoLAN\VLC\vlc.exe".into())
        );
        assert_eq!(
            exe_from_command(r"C:\Program Files\App\app.exe --flag"),
            Some(r"C:\Program Files\App\app.exe".into())
        );
        assert_eq!(exe_from_command("   "), None);
        assert_eq!(exe_from_command(r#""not closed"#), None);
    }

    #[test]
    fn ordinary_app_can_be_switched_security_and_windows_cannot() {
        assert_eq!(
            lock_for(&class(
                "VLC media player",
                r"C:\Program Files\VideoLAN\VLC\vlc.exe",
                StartupPlace::RunHkcu,
                ""
            )),
            None
        );
        assert_eq!(
            lock_for(&class(
                "Kaspersky",
                r"C:\Program Files\Kaspersky Lab\avp.exe",
                StartupPlace::RunHklm,
                ""
            )),
            Some(StartupLock::Security)
        );
        assert_eq!(
            lock_for(&class(
                "SecurityHealth",
                r"C:\Windows\System32\SecurityHealthSystray.exe",
                StartupPlace::RunHklm,
                ""
            )),
            Some(StartupLock::Security)
        );
        assert_eq!(
            lock_for(&class(
                "Helper",
                r"%SystemRoot%\System32\helper.exe",
                StartupPlace::RunHklm,
                ""
            )),
            Some(StartupLock::Windows)
        );
        assert_eq!(
            lock_for(&class(
                "Average CPU",
                r"C:\Apps\average.exe",
                StartupPlace::RunHkcu,
                ""
            )),
            None,
            "a short security token must not match inside another word"
        );
        assert_eq!(
            lock_for(&class(
                "AVG",
                r"C:\Program Files\AVG\avgui.exe",
                StartupPlace::RunHkcu,
                ""
            )),
            Some(StartupLock::Security)
        );
        assert_eq!(
            lock_for(&class(
                "preset",
                r"C:\Apps\preset.exe",
                StartupPlace::FolderUser,
                ""
            )),
            None
        );
        assert_eq!(
            lock_for(&class(
                "Quantum Updater",
                r"C:\Program Files\Quantum Updater\Quantum Updater.exe",
                StartupPlace::Task,
                r"\QuantumStep"
            )),
            Some(StartupLock::Settings)
        );
        assert_eq!(
            lock_for(&class(
                "Scan",
                r"C:\Windows\System32\task.exe",
                StartupPlace::Task,
                r"\Microsoft\Windows"
            )),
            Some(StartupLock::Windows)
        );
    }

    #[test]
    fn refs_cannot_point_outside_one_entry() {
        assert!(ref_ok(StartupPlace::RunHkcu, "Spotify", ""));
        assert!(!ref_ok(StartupPlace::RunHkcu, r"Foo\Bar", ""));
        assert!(!ref_ok(StartupPlace::RunHklm, "Spotify", r"\extra"));
        assert!(ref_ok(StartupPlace::FolderUser, "VLC.lnk", ""));
        assert!(!ref_ok(StartupPlace::FolderUser, r"..\VLC.lnk", ""));
        assert!(!ref_ok(StartupPlace::FolderCommon, "notes.txt", ""));
        assert!(ref_ok(StartupPlace::Task, "Nightly", r"\MyApps"));
        assert!(!ref_ok(StartupPlace::Task, "Scan", r"\Microsoft\Windows"));
        assert!(!ref_ok(StartupPlace::Task, "Scan", r"\Foo\..\Windows"));
        assert!(ref_ok(
            StartupPlace::Task,
            "Quantum Updater",
            r"\QuantumStep"
        ));
        assert_eq!(task_path(r"\", "Nightly"), r"\Nightly");
        assert_eq!(task_path(r"\MyApps", "Nightly"), r"\MyApps\Nightly");
    }

    #[test]
    fn lnk_prefers_the_unicode_target() {
        let ansi = b"C:\\nope.exe\0";
        let wide: Vec<u8> = "D:\\Apps\\real.exe"
            .encode_utf16()
            .chain(std::iter::once(0))
            .flat_map(|u| u.to_le_bytes())
            .collect();
        let mut info = vec![0u8; 0x24];
        info.extend_from_slice(ansi);
        let uoff = info.len() as u32;
        info.extend_from_slice(&wide);
        let size = info.len() as u32;
        info[0..4].copy_from_slice(&size.to_le_bytes());
        info[4..8].copy_from_slice(&0x24u32.to_le_bytes());
        info[8..12].copy_from_slice(&1u32.to_le_bytes());
        info[16..20].copy_from_slice(&0x24u32.to_le_bytes());
        info[0x1C..0x20].copy_from_slice(&uoff.to_le_bytes());

        let mut file = vec![0u8; 0x4C];
        file[0..4].copy_from_slice(&0x4Cu32.to_le_bytes());
        file[0x14..0x18].copy_from_slice(&0x2u32.to_le_bytes());
        file.extend_from_slice(&info);
        assert_eq!(lnk_target(&file).as_deref(), Some(r"D:\Apps\real.exe"));

        // ANSI only, when the file has no unicode path.
        let mut ansi_info = vec![0u8; 0x1C];
        ansi_info.extend_from_slice(b"C:\\Apps\\vlc.exe\0");
        let size = ansi_info.len() as u32;
        ansi_info[0..4].copy_from_slice(&size.to_le_bytes());
        ansi_info[4..8].copy_from_slice(&0x1Cu32.to_le_bytes());
        ansi_info[8..12].copy_from_slice(&1u32.to_le_bytes());
        ansi_info[16..20].copy_from_slice(&0x1Cu32.to_le_bytes());
        let mut ansi_file = vec![0u8; 0x4C];
        ansi_file[0..4].copy_from_slice(&0x4Cu32.to_le_bytes());
        ansi_file[0x14..0x18].copy_from_slice(&0x2u32.to_le_bytes());
        ansi_file.extend_from_slice(&ansi_info);
        assert_eq!(lnk_target(&ansi_file).as_deref(), Some(r"C:\Apps\vlc.exe"));
        assert_eq!(lnk_target(&[0; 10]), None);
    }

    #[test]
    fn this_pc_list_obeys_the_rules() {
        let list = collect(true);
        assert!(list.items.len() <= 400);
        for it in &list.items {
            assert!(
                ref_ok(it.place, &it.name, &it.folder),
                "bad ref {} {}",
                it.label,
                it.folder
            );
            let folder = it.folder.to_lowercase();
            assert!(
                folder != "\\microsoft" && !folder.starts_with("\\microsoft\\"),
                "listed a Windows task"
            );
            if folder == "\\quantumstep" {
                assert_eq!(it.lock, Some(StartupLock::Settings));
            }
        }
    }

    #[test]
    fn detail_names_the_program_not_the_whole_command() {
        assert_eq!(
            detail_for(StartupPlace::RunHkcu, r#""C:\Apps\vlc.exe" --go"#),
            "Sign-in setting · vlc.exe"
        );
        assert_eq!(
            label_for(StartupPlace::FolderUser, "VLC media player.lnk"),
            "VLC media player"
        );
        assert_eq!(detail_for(StartupPlace::Task, ""), "Sign-in task");
    }

    /// A disposable sign-in entry on this PC. Turning it off and on must not remove the entry,
    /// and a security entry must refuse the write.
    #[test]
    fn this_pc_toggle_disables_without_deleting() {
        use winreg::enums::{HKEY_CURRENT_USER, KEY_READ, KEY_SET_VALUE};
        use winreg::RegKey;
        const RUN: &str = r"Software\Microsoft\Windows\CurrentVersion\Run";
        const APPROVED: &str =
            r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        const PLAIN: &str = "QU-UAT-Probe";
        const LOCKED: &str = "Kaspersky UAT Probe";
        let cmd = r"C:\Program Files\QUUAT\probe.exe";

        struct Cleanup;
        impl Drop for Cleanup {
            fn drop(&mut self) {
                let hkcu = RegKey::predef(HKEY_CURRENT_USER);
                for key in [RUN, APPROVED] {
                    if let Ok(k) = hkcu.open_subkey_with_flags(key, KEY_SET_VALUE) {
                        let _ = k.delete_value(PLAIN);
                        let _ = k.delete_value(LOCKED);
                    }
                }
            }
        }
        let _cleanup = Cleanup;

        let hkcu = RegKey::predef(HKEY_CURRENT_USER);
        let (run, _) = hkcu.create_subkey(RUN).unwrap();
        run.set_value(PLAIN, &cmd).unwrap();
        run.set_value(LOCKED, &cmd).unwrap();

        let plain = StartupRef {
            place: StartupPlace::RunHkcu,
            name: PLAIN.into(),
            folder: String::new(),
        };
        let off = apply(plain.clone(), false).expect("plain item switches off");
        assert!(!off.enabled);
        assert!(off.lock.is_none());
        let still: String = run.get_value(PLAIN).unwrap();
        assert_eq!(still, cmd, "turning it off must leave the sign-in entry");
        let approved = hkcu.open_subkey_with_flags(APPROVED, KEY_READ).unwrap();
        assert_eq!(approved.get_raw_value(PLAIN).unwrap().bytes[0], 0x03);

        let on = apply(plain, true).unwrap();
        assert!(on.enabled);
        let still: String = run.get_value(PLAIN).unwrap();
        assert_eq!(
            still, cmd,
            "turning it back on must leave the sign-in entry"
        );
        assert_eq!(approved.get_raw_value(PLAIN).unwrap().bytes[0], 0x02);

        let locked = StartupRef {
            place: StartupPlace::RunHkcu,
            name: LOCKED.into(),
            folder: String::new(),
        };
        let err = apply(locked, false).expect_err("security software stays on");
        assert_eq!(err.code, "startup_locked");
        let still: String = run.get_value(LOCKED).unwrap();
        assert_eq!(still, cmd);
        assert!(approved.get_raw_value(LOCKED).is_err());
    }
}
