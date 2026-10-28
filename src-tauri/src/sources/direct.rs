//! Opening the uninstaller an app installed directly already registered.
//!
//! The page sends only the row id. The command is read again from that PC and checked here
//! before anything runs. It is never passed to a shell. A per-user entry is started as the
//! signed-in person, because that registry value can be written without administrator rights.

use std::path::{Component, Path, PathBuf};

use crate::error::{AppError, AppResult};
use crate::sources::label::parse_arp_id;
use crate::sources::model::InstalledPackage;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Plan {
    pub exe: PathBuf,
    pub args: Vec<String>,
    /// The original text after the program, for the unelevated launch.
    pub params: String,
    pub elevated: bool,
    pub name: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Removal {
    Program(Plan),
    /// A Store-style package. Windows removes it; there is no separate program to start.
    Package {
        full_name: String,
        name: String,
    },
}

#[derive(Debug, Clone)]
pub struct Roots {
    pub program_files: PathBuf,
    pub program_files_x86: PathBuf,
    pub program_data: PathBuf,
    pub system32: PathBuf,
    pub syswow64: PathBuf,
    pub installer: PathBuf,
    pub local_appdata: PathBuf,
    pub appdata: PathBuf,
    pub cargo: PathBuf,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Scope {
    Machine,
    User,
}

const BLOCKED_EXE: &[&str] = &[
    "cmd.exe",
    "powershell.exe",
    "pwsh.exe",
    "wscript.exe",
    "cscript.exe",
    "mshta.exe",
    "bash.exe",
    "sh.exe",
    "wt.exe",
    "conhost.exe",
    "forfiles.exe",
    "certutil.exe",
    "reg.exe",
    "regedit.exe",
    "schtasks.exe",
    "bitsadmin.exe",
    "wmic.exe",
    "ftp.exe",
    "curl.exe",
    "ssh.exe",
    "python.exe",
    "node.exe",
];

/// Which directly installed rows can offer Uninstall. Catalog rows stay on their own path.
pub fn mark_removable(items: &mut [InstalledPackage], same_user: Option<bool>) {
    let roots = machine_roots();
    for item in items {
        item.removable = item.source.is_none()
            && !item.component
            && roots
                .as_ref()
                .is_some_and(|roots| removable(item, roots, same_user));
    }
}

fn removable(item: &InstalledPackage, roots: &Roots, same_user: Option<bool>) -> bool {
    if let Some(full) = msix_name(&item.id) {
        return same_user != Some(false)
            && !crate::sources::components::is_component(&item.name, &item.id)
            && !full.is_empty();
    }
    prepare_arp(&item.id, same_user, roots, &|p| p.is_file()).is_ok()
}

/// Read this PC again and decide what to run. Refuses anything the checks don't accept.
pub fn prepare(id: &str, same_user: Option<bool>) -> AppResult<Removal> {
    if let Some(full) = msix_name(id) {
        if same_user == Some(false) {
            return Err(other_user());
        }
        if crate::sources::components::is_component("", id) {
            return Err(refused());
        }
        return Ok(Removal::Package {
            name: full.to_string(),
            full_name: full.to_string(),
        });
    }
    let roots = machine_roots().ok_or_else(refused)?;
    prepare_arp(id, same_user, &roots, &|p| p.is_file())
}

fn prepare_arp(
    id: &str,
    same_user: Option<bool>,
    roots: &Roots,
    exists: &dyn Fn(&Path) -> bool,
) -> AppResult<Removal> {
    let arp = parse_arp_id(id).filter(|a| !a.key.contains('\\') && a.key.len() <= 200);
    let Some(arp) = arp else {
        return Err(refused());
    };
    if !arp.machine && same_user == Some(false) {
        return Err(other_user());
    }
    let Some(entry) = read_arp(arp.machine, arp.x64, arp.key) else {
        return Err(refused());
    };
    if entry.system_component || entry.no_remove {
        return Err(refused());
    }
    if crate::sources::components::is_component(&entry.name, id) {
        return Err(refused());
    }
    let scope = if arp.machine {
        Scope::Machine
    } else {
        Scope::User
    };
    let Some(mut plan) = plan_command(&entry.command, scope, roots, exists) else {
        return Err(refused());
    };
    plan.name = entry.name;
    Ok(Removal::Program(plan))
}

fn other_user() -> AppError {
    AppError::new(
        "other_user",
        "You're running as a different administrator, so apps installed just for you can't be removed here.",
    )
}

fn refused() -> AppError {
    AppError::new("direct_refused", "This app can't be removed here.")
}

struct ArpEntry {
    name: String,
    command: String,
    system_component: bool,
    no_remove: bool,
}

fn read_arp(machine: bool, x64: bool, key: &str) -> Option<ArpEntry> {
    use winreg::enums::{
        HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, KEY_READ, KEY_WOW64_32KEY, KEY_WOW64_64KEY,
    };
    use winreg::RegKey;
    let (hive, view) = if machine {
        (
            HKEY_LOCAL_MACHINE,
            if x64 {
                KEY_WOW64_64KEY
            } else {
                KEY_WOW64_32KEY
            },
        )
    } else {
        (HKEY_CURRENT_USER, KEY_READ)
    };
    let flags = if machine { KEY_READ | view } else { view };
    let k = RegKey::predef(hive)
        .open_subkey_with_flags(
            format!(r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{key}"),
            flags,
        )
        .ok()?;
    let command = k
        .get_value::<String, _>("UninstallString")
        .ok()
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())?;
    let name = k
        .get_value::<String, _>("DisplayName")
        .ok()
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())
        .unwrap_or_else(|| "App".into());
    let dword = |v: &str| k.get_value::<u32, _>(v).unwrap_or(0) == 1;
    Some(ArpEntry {
        name,
        command,
        system_component: dword("SystemComponent"),
        no_remove: dword("NoRemove"),
    })
}

/// `MSIX\<package full name>` as winget prints it. Nothing else is accepted.
fn msix_name(id: &str) -> Option<&str> {
    let rest = id
        .get(..5)
        .filter(|p| p.eq_ignore_ascii_case("MSIX\\"))
        .map(|_| &id[5..])?;
    if rest.len() > 400
        || !rest.contains("__")
        || !rest
            .chars()
            .all(|c| c.is_ascii_alphanumeric() || matches!(c, '.' | '_' | '-'))
    {
        return None;
    }
    Some(rest)
}

fn plan_command(
    raw: &str,
    scope: Scope,
    roots: &Roots,
    exists: &dyn Fn(&Path) -> bool,
) -> Option<Plan> {
    let raw = raw.trim();
    if raw.is_empty()
        || raw.len() > 2000
        || raw.contains('%')
        || raw.chars().any(|c| c.is_control())
    {
        return None;
    }
    let (exe_text, params) = exe_and_tail(raw)?;
    let argv = split_command(raw)?;
    if argv.is_empty() || argv.len() > 40 || argv.iter().any(|a| a.len() > 2000) {
        return None;
    }
    let file = PathBuf::from(exe_text);
    let name = file.file_name()?.to_string_lossy().to_ascii_lowercase();
    if BLOCKED_EXE.contains(&name.as_str()) {
        return None;
    }
    let (exe, args) = if name == "msiexec.exe" {
        if !msiexec_args_ok(&argv[1..]) {
            return None;
        }
        (roots.system32.join("msiexec.exe"), argv[1..].to_vec())
    } else if name == "rundll32.exe" {
        rundll32(roots, scope, &file, &argv[1..], exists)?
    } else {
        if !safe_path(&file) || !allowed_exe(&file, scope, roots) || !exists(&file) {
            return None;
        }
        (file, argv[1..].to_vec())
    };
    if !exists(&exe) {
        return None;
    }
    Some(Plan {
        exe,
        args,
        params: params.to_string(),
        elevated: scope == Scope::Machine,
        name: String::new(),
    })
}

fn rundll32(
    roots: &Roots,
    scope: Scope,
    named: &Path,
    args: &[String],
    exists: &dyn Fn(&Path) -> bool,
) -> Option<(PathBuf, Vec<String>)> {
    let dll_text = args.first()?.split(',').next()?.trim().trim_matches('"');
    let dll = PathBuf::from(dll_text);
    if !safe_path(&dll)
        || !dll
            .extension()
            .is_some_and(|e| e.eq_ignore_ascii_case("dll"))
        || !allowed_exe(&dll, scope, roots)
        || !exists(&dll)
    {
        return None;
    }
    // Use the system copy of the same bitness the entry named. Never the path it gave us.
    let wow = named
        .components()
        .any(|c| matches!(c, Component::Normal(s) if s.eq_ignore_ascii_case("SysWOW64")));
    let pair = if wow {
        [
            roots.syswow64.join("rundll32.exe"),
            roots.system32.join("rundll32.exe"),
        ]
    } else {
        [
            roots.system32.join("rundll32.exe"),
            roots.syswow64.join("rundll32.exe"),
        ]
    };
    let exe = pair.into_iter().find(|p| exists(p))?;
    Some((exe, args.to_vec()))
}

fn msiexec_args_ok(args: &[String]) -> bool {
    let guid = |s: &str| {
        let s = s.trim();
        let inner = s
            .strip_prefix('{')
            .and_then(|s| s.strip_suffix('}'))
            .unwrap_or("");
        let mut parts = inner.split('-');
        matches!(
            (
                parts.next().map(str::len),
                parts.next().map(str::len),
                parts.next().map(str::len),
                parts.next().map(str::len),
                parts.next().map(str::len),
                parts.next()
            ),
            (Some(8), Some(4), Some(4), Some(4), Some(12), None)
        ) && inner.chars().all(|c| c.is_ascii_hexdigit() || c == '-')
    };
    match args {
        [one] => one
            .strip_prefix('/')
            .or_else(|| one.strip_prefix('-'))
            .and_then(|rest| rest.get(..1).map(|flag| (flag, &rest[1..])))
            .is_some_and(|(flag, id)| {
                (flag.eq_ignore_ascii_case("x") || flag.eq_ignore_ascii_case("i")) && guid(id)
            }),
        [flag, id] => {
            let flag = flag.trim_start_matches(['/', '-']);
            (flag.eq_ignore_ascii_case("x") || flag.eq_ignore_ascii_case("i")) && guid(id)
        }
        _ => false,
    }
}

fn allowed_exe(path: &Path, scope: Scope, roots: &Roots) -> bool {
    let machine = [
        &roots.program_files,
        &roots.program_files_x86,
        &roots.program_data,
        &roots.installer,
    ];
    if machine.iter().any(|root| within(path, root)) {
        return true;
    }
    scope == Scope::User
        && [&roots.local_appdata, &roots.appdata, &roots.cargo]
            .iter()
            .any(|root| within(path, root))
}

fn safe_path(path: &Path) -> bool {
    path.is_absolute()
        && path
            .components()
            .all(|c| !matches!(c, Component::ParentDir))
}

fn within(path: &Path, root: &Path) -> bool {
    let mut parts = path.components();
    for rc in root.components() {
        match (rc, parts.next()) {
            (Component::Prefix(a), Some(Component::Prefix(b)))
                if a.as_os_str().eq_ignore_ascii_case(b.as_os_str()) => {}
            (Component::RootDir, Some(Component::RootDir)) => {}
            (Component::Normal(a), Some(Component::Normal(b))) if a.eq_ignore_ascii_case(b) => {}
            _ => return false,
        }
    }
    parts.next().is_some()
}

fn exe_and_tail(raw: &str) -> Option<(&str, &str)> {
    let s = raw.trim();
    if let Some(rest) = s.strip_prefix('"') {
        let end = rest.find('"')?;
        return Some((&rest[..end], rest[end + 1..].trim()));
    }
    let end = s.find(char::is_whitespace).unwrap_or(s.len());
    (!s[..end].is_empty()).then_some((&s[..end], s[end..].trim()))
}

fn split_command(raw: &str) -> Option<Vec<String>> {
    use windows_sys::Win32::Foundation::LocalFree;
    use windows_sys::Win32::UI::Shell::CommandLineToArgvW;
    let wide: Vec<u16> = raw.encode_utf16().chain(std::iter::once(0)).collect();
    let mut n = 0i32;
    // SAFETY: `wide` is NUL-terminated. The returned block is freed with LocalFree.
    let ptr = unsafe { CommandLineToArgvW(wide.as_ptr(), &mut n) };
    if ptr.is_null() || n <= 0 {
        return None;
    }
    let mut out = Vec::with_capacity(n as usize);
    for i in 0..n as usize {
        // SAFETY: CommandLineToArgvW wrote `n` NUL-terminated strings.
        let s = unsafe { *ptr.add(i) };
        let len = (0..).take_while(|&k| unsafe { *s.add(k) } != 0).count();
        let text = String::from_utf16_lossy(unsafe { std::slice::from_raw_parts(s, len) });
        out.push(text);
    }
    // SAFETY: the block came from CommandLineToArgvW.
    unsafe { LocalFree(ptr.cast()) };
    Some(out)
}

fn machine_roots() -> Option<Roots> {
    use crate::sources::trust::{known_folder, program_data_dir, program_files_dir};
    use windows_sys::Win32::UI::Shell::{
        FOLDERID_LocalAppData, FOLDERID_Profile, FOLDERID_ProgramFilesX86, FOLDERID_RoamingAppData,
        FOLDERID_System, FOLDERID_SystemX86, FOLDERID_Windows,
    };
    let windows = known_folder(&FOLDERID_Windows)?;
    Some(Roots {
        program_files: program_files_dir()?,
        program_files_x86: known_folder(&FOLDERID_ProgramFilesX86)?,
        program_data: program_data_dir()?,
        system32: known_folder(&FOLDERID_System)?,
        syswow64: known_folder(&FOLDERID_SystemX86)?,
        installer: windows.join("Installer"),
        local_appdata: known_folder(&FOLDERID_LocalAppData)?,
        appdata: known_folder(&FOLDERID_RoamingAppData)?,
        cargo: known_folder(&FOLDERID_Profile)?.join(".cargo"),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn roots() -> Roots {
        Roots {
            program_files: PathBuf::from(r"C:\Program Files"),
            program_files_x86: PathBuf::from(r"C:\Program Files (x86)"),
            program_data: PathBuf::from(r"C:\ProgramData"),
            system32: PathBuf::from(r"C:\Windows\System32"),
            syswow64: PathBuf::from(r"C:\Windows\SysWOW64"),
            installer: PathBuf::from(r"C:\Windows\Installer"),
            local_appdata: PathBuf::from(r"C:\Users\merch\AppData\Local"),
            appdata: PathBuf::from(r"C:\Users\merch\AppData\Roaming"),
            cargo: PathBuf::from(r"C:\Users\merch\.cargo"),
        }
    }

    fn plan(raw: &str, scope: Scope) -> Option<Plan> {
        plan_command(raw, scope, &roots(), &|_| true)
    }

    #[test]
    fn a_program_files_uninstaller_is_kept_as_written() {
        let p = plan(
            r#""C:\Program Files\FileZilla FTP Client\uninstall.exe""#,
            Scope::Machine,
        )
        .unwrap();
        assert!(p.elevated);
        assert!(p.exe.ends_with(r"FileZilla FTP Client\uninstall.exe"));
        assert!(p.args.is_empty());
    }

    #[test]
    fn msiexec_is_the_system_copy_and_only_the_product() {
        let p = plan(
            r"MsiExec.exe /X{3B0346D3-155D-43E5-9561-B9BE51CF2EB6}",
            Scope::Machine,
        )
        .unwrap();
        assert_eq!(p.exe, roots().system32.join("msiexec.exe"));
        assert_eq!(
            p.args,
            vec!["/X{3B0346D3-155D-43E5-9561-B9BE51CF2EB6}".to_string()]
        );
        assert!(plan(
            r"MsiExec.exe /X{3B0346D3-155D-43E5-9561-B9BE51CF2EB6} /qn",
            Scope::Machine
        )
        .is_none());
        let swapped = plan(
            r"C:\Temp\msiexec.exe /X{3B0346D3-155D-43E5-9561-B9BE51CF2EB6}",
            Scope::Machine,
        )
        .unwrap();
        assert_eq!(swapped.exe, roots().system32.join("msiexec.exe"));
    }

    #[test]
    fn shells_variables_and_user_files_are_refused_for_everyone() {
        assert!(plan(r"C:\Windows\System32\cmd.exe /c calc", Scope::Machine).is_none());
        assert!(plan(r#"%ProgramFiles%\App\uninstall.exe"#, Scope::Machine).is_none());
        assert!(plan(
            r#"C:\Users\merch\AppData\Local\Programs\Canva\Uninstall Canva.exe"#,
            Scope::Machine
        )
        .is_none());
        assert!(plan(
            r"C:\Program Files\App\..\Windows\System32\cmd.exe",
            Scope::Machine
        )
        .is_none());
        assert!(plan("winget uninstall --product-code Something", Scope::User).is_none());
    }

    #[test]
    fn a_per_user_uninstaller_stays_in_that_account() {
        let p = plan(
            r#""C:\Users\merch\AppData\Local\Programs\Canva\Uninstall Canva.exe" /currentuser"#,
            Scope::User,
        )
        .unwrap();
        assert!(!p.elevated);
        assert_eq!(p.args, ["/currentuser"]);
        assert_eq!(p.params, "/currentuser");
        let rustup = plan(
            r#""C:\Users\merch\.cargo\bin\rustup.exe" self uninstall"#,
            Scope::User,
        )
        .unwrap();
        assert!(!rustup.elevated);
    }

    #[test]
    fn rundll32_only_loads_a_program_dll() {
        let p = plan(
            r#""C:\Windows\SysWOW64\RunDll32.EXE" "C:\Program Files\NVIDIA Corporation\Installer2\InstallerCore\NVI2.DLL",UninstallPackage Display.Driver"#,
            Scope::Machine,
        )
        .unwrap();
        assert_eq!(p.exe, roots().syswow64.join("rundll32.exe"));
        assert!(plan(
            r#""C:\Windows\System32\rundll32.exe" "C:\Windows\System32\shell32.dll",Control_RunDLL"#,
            Scope::Machine
        )
        .is_none());
    }

    #[test]
    fn a_missing_file_is_refused() {
        assert!(plan_command(
            r#""C:\Program Files\FileZilla FTP Client\uninstall.exe""#,
            Scope::Machine,
            &roots(),
            &|_| false
        )
        .is_none());
    }

    #[test]
    fn msix_ids_are_the_package_name_only() {
        assert!(msix_name(r"MSIX\SpotifyAB.SpotifyMusic_1.2.0.0_x64__zpdnekdrzrea0").is_some());
        assert!(msix_name(r"MSIX\..\Windows\System32\cmd").is_none());
        assert!(msix_name(r"ARP\Machine\X64\Steam").is_none());
    }

    /// Read-only. Skips when this PC doesn't have the app.
    #[test]
    fn this_pc_filezilla_uses_its_own_uninstaller() {
        let Ok(Removal::Program(p)) = prepare(r"ARP\Machine\X86\FileZilla Client", Some(true))
        else {
            return;
        };
        assert!(p.elevated);
        assert!(p
            .exe
            .file_name()
            .unwrap()
            .eq_ignore_ascii_case("uninstall.exe"));
        assert!(within(&p.exe, &machine_roots().unwrap().program_files));
    }
}
