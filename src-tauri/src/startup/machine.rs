//! Live Run keys, Startup folders, and sign-in tasks. The only writes are a `StartupApproved`
//! flag or a task's Enabled bit. Nothing here deletes a value, a shortcut, or a task.

use super::{
    approved_bytes, approved_state, detail_for, filetime_now, label_for, lnk_target, lock_for,
    place_is_per_user, ref_ok, sort_items, task_path, Approved, Classified, StartupItem,
    StartupList, StartupLock, StartupPlace, StartupRef,
};
use crate::error::AppError;
use std::path::{Path, PathBuf};

const RUN: &str = r"Software\Microsoft\Windows\CurrentVersion\Run";
const RUN32: &str = r"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
const APPROVED_RUN: &str =
    r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
const APPROVED_RUN32: &str =
    r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
const APPROVED_FOLDER: &str =
    r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

#[derive(Clone, Copy)]
struct Slot {
    place: StartupPlace,
    hive: winreg::HKEY,
    run_key: &'static str,
    approved_key: &'static str,
    wow: u32,
}

fn slots() -> [Slot; 3] {
    use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, KEY_WOW64_64KEY};
    [
        Slot {
            place: StartupPlace::RunHkcu,
            hive: HKEY_CURRENT_USER,
            run_key: RUN,
            approved_key: APPROVED_RUN,
            wow: 0,
        },
        Slot {
            place: StartupPlace::RunHklm,
            hive: HKEY_LOCAL_MACHINE,
            run_key: RUN,
            approved_key: APPROVED_RUN,
            wow: KEY_WOW64_64KEY,
        },
        Slot {
            place: StartupPlace::RunHklm32,
            hive: HKEY_LOCAL_MACHINE,
            run_key: RUN32,
            approved_key: APPROVED_RUN32,
            wow: KEY_WOW64_64KEY,
        },
    ]
}

pub fn collect(per_user: bool) -> StartupList {
    let mut items = Vec::new();
    for slot in slots() {
        if !per_user && place_is_per_user(slot.place) {
            continue;
        }
        items.extend(read_run(&slot));
    }
    if per_user {
        if let Some(dir) = known_dir(windows_sys::Win32::UI::Shell::FOLDERID_Startup) {
            items.extend(read_folder(&dir, StartupPlace::FolderUser));
        }
    }
    if let Some(dir) = known_dir(windows_sys::Win32::UI::Shell::FOLDERID_CommonStartup) {
        items.extend(read_folder(&dir, StartupPlace::FolderCommon));
    }
    let warning = match read_tasks() {
        Ok(tasks) => {
            items.extend(tasks);
            None
        }
        Err(_) => Some("Some sign-in tasks couldn't be read.".to_string()),
    };
    sort_items(&mut items);
    if items.len() > 400 {
        items.truncate(400);
    }
    StartupList {
        items,
        warning,
        per_user,
    }
}

pub fn apply(item: StartupRef, enabled: bool) -> Result<StartupItem, AppError> {
    if !ref_ok(item.place, &item.name, &item.folder) {
        return Err(AppError::new(
            "invalid_input",
            "That startup item can't be changed.",
        ));
    }
    let current = load(&item)?.ok_or_else(missing)?;
    if let Some(lock) = current.lock {
        return Err(AppError::new("startup_locked", lock.message()));
    }
    match item.place {
        StartupPlace::Task => set_task(&item.folder, &item.name, enabled)?,
        StartupPlace::FolderUser | StartupPlace::FolderCommon => {
            write_approved(folder_slot(item.place), &item.name, enabled)?;
        }
        place => {
            let slot = slots()
                .into_iter()
                .find(|s| s.place == place)
                .ok_or_else(missing)?;
            write_approved(slot, &item.name, enabled)?;
        }
    }
    load(&item)?.ok_or_else(missing)
}

fn missing() -> AppError {
    AppError::new("not_found", "That startup item is no longer there.")
}

fn denied() -> AppError {
    AppError::new(
        "needs_admin",
        "Quantum Updater needs administrator access to change this.",
    )
}

fn io_err(e: std::io::Error) -> AppError {
    if e.kind() == std::io::ErrorKind::PermissionDenied {
        denied()
    } else {
        AppError::new("startup", "That startup item couldn't be changed.")
    }
}

fn load(item: &StartupRef) -> Result<Option<StartupItem>, AppError> {
    let found = match item.place {
        StartupPlace::Task => return load_task(&item.folder, &item.name),
        StartupPlace::FolderUser | StartupPlace::FolderCommon => load_shortcut(item),
        place => slots()
            .into_iter()
            .find(|s| s.place == place)
            .and_then(|s| load_run(&s, &item.name)),
    };
    Ok(found.filter(|got| got.name.eq_ignore_ascii_case(&item.name)))
}

fn load_run(slot: &Slot, name: &str) -> Option<StartupItem> {
    let key = open_read(slot.hive, slot.run_key, slot.wow)?;
    let command = key.get_value::<String, _>(name).ok()?;
    let approved =
        open_read(slot.hive, slot.approved_key, slot.wow).and_then(|k| raw_value(&k, name));
    finish(
        slot.place,
        name,
        "",
        &expand_env(&command),
        approved.as_deref(),
    )
}

fn read_run(slot: &Slot) -> Vec<StartupItem> {
    let Some(key) = open_read(slot.hive, slot.run_key, slot.wow) else {
        return Vec::new();
    };
    let approved = open_read(slot.hive, slot.approved_key, slot.wow);
    let mut out = Vec::new();
    for item in key.enum_values().flatten() {
        let name = item.0;
        if name.is_empty() {
            continue;
        }
        let Some(command) = reg_string(&item.1) else {
            continue;
        };
        let bytes = approved.as_ref().and_then(|k| raw_value(k, &name));
        if let Some(row) = finish(
            slot.place,
            &name,
            "",
            &expand_env(&command),
            bytes.as_deref(),
        ) {
            out.push(row);
        }
    }
    out
}

fn load_shortcut(item: &StartupRef) -> Option<StartupItem> {
    let id = match item.place {
        StartupPlace::FolderUser => windows_sys::Win32::UI::Shell::FOLDERID_Startup,
        _ => windows_sys::Win32::UI::Shell::FOLDERID_CommonStartup,
    };
    let dir = known_dir(id)?;
    let path = dir.join(&item.name);
    if path.parent() != Some(dir.as_path()) || !path.is_file() {
        return None;
    }
    shortcut_item(item.place, &dir, &item.name)
}

fn read_folder(dir: &Path, place: StartupPlace) -> Vec<StartupItem> {
    let Ok(rd) = std::fs::read_dir(dir) else {
        return Vec::new();
    };
    rd.flatten()
        .filter_map(|ent| {
            let name = ent.file_name().to_string_lossy().to_string();
            shortcut_item(place, dir, &name)
        })
        .collect()
}

fn shortcut_item(place: StartupPlace, dir: &Path, name: &str) -> Option<StartupItem> {
    if !ref_ok(place, name, "") {
        return None;
    }
    let path = dir.join(name);
    if path.parent() != Some(dir) || !path.is_file() {
        return None;
    }
    let data = std::fs::read(&path).unwrap_or_default();
    let target = if data.len() <= 256 * 1024 {
        lnk_target(&data).unwrap_or_default()
    } else {
        String::new()
    };
    let slot = folder_slot(place);
    let approved =
        open_read(slot.hive, slot.approved_key, slot.wow).and_then(|k| raw_value(&k, name));
    finish(place, name, "", &expand_env(&target), approved.as_deref())
}

fn folder_slot(place: StartupPlace) -> Slot {
    use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, KEY_WOW64_64KEY};
    let common = place == StartupPlace::FolderCommon;
    Slot {
        place,
        hive: if common {
            HKEY_LOCAL_MACHINE
        } else {
            HKEY_CURRENT_USER
        },
        run_key: "",
        approved_key: APPROVED_FOLDER,
        wow: if common { KEY_WOW64_64KEY } else { 0 },
    }
}

fn write_approved(slot: Slot, name: &str, enabled: bool) -> Result<(), AppError> {
    use winreg::enums::{RegType, KEY_WRITE};
    use winreg::RegKey;
    let flags = KEY_WRITE | slot.wow;
    let (key, _) = RegKey::predef(slot.hive)
        .create_subkey_with_flags(slot.approved_key, flags)
        .map_err(io_err)?;
    let bytes = approved_bytes(enabled, filetime_now()).to_vec();
    key.set_raw_value(
        name,
        &winreg::RegValue {
            bytes: bytes.into(),
            vtype: RegType::REG_BINARY,
        },
    )
    .map_err(io_err)
}

fn finish(
    place: StartupPlace,
    name: &str,
    folder: &str,
    command: &str,
    approved: Option<&[u8]>,
) -> Option<StartupItem> {
    if !ref_ok(place, name, folder) {
        return None;
    }
    let label = label_for(place, name);
    let state: Approved = approved_state(approved);
    let mut lock = lock_for(&Classified {
        label: &label,
        command,
        place,
        task_folder: folder,
        windows_dir: &windows_dir(),
    });
    if lock.is_none() && state.policy {
        lock = Some(StartupLock::Policy);
    }
    if lock.is_none() && command.is_empty() && !place_is_per_user(place) {
        lock = Some(StartupLock::Unchecked);
    }
    Some(StartupItem {
        place,
        name: name.to_string(),
        folder: folder.to_string(),
        label,
        detail: detail_for(place, command),
        enabled: state.enabled,
        lock,
    })
}

fn open_read(hive: winreg::HKEY, subkey: &str, wow: u32) -> Option<winreg::RegKey> {
    use winreg::enums::KEY_READ;
    use winreg::RegKey;
    RegKey::predef(hive)
        .open_subkey_with_flags(subkey, KEY_READ | wow)
        .ok()
}

fn raw_value(key: &winreg::RegKey, name: &str) -> Option<Vec<u8>> {
    use winreg::enums::RegType;
    let value = key.get_raw_value(name).ok()?;
    (value.vtype == RegType::REG_BINARY).then_some(value.bytes.into_owned())
}

fn reg_string(value: &winreg::RegValue) -> Option<String> {
    use winreg::enums::RegType;
    if !matches!(value.vtype, RegType::REG_SZ | RegType::REG_EXPAND_SZ) {
        return None;
    }
    let (chunks, _) = value.bytes.as_ref().as_chunks::<2>();
    let units: Vec<u16> = chunks.iter().copied().map(u16::from_le_bytes).collect();
    let end = units.iter().position(|c| *c == 0).unwrap_or(units.len());
    let s = String::from_utf16_lossy(&units[..end]);
    (!s.is_empty()).then_some(s)
}

fn windows_dir() -> String {
    std::env::var("SystemRoot").unwrap_or_else(|_| r"C:\Windows".into())
}

fn expand_env(s: &str) -> String {
    use windows_sys::Win32::System::Environment::ExpandEnvironmentStringsW;
    if !s.contains('%') {
        return s.to_string();
    }
    let wide: Vec<u16> = s.encode_utf16().chain(std::iter::once(0)).collect();
    let mut buf = vec![0u16; 32_768];
    // SAFETY: `wide` is NUL-terminated; `buf` is writable and its length is passed.
    let n = unsafe { ExpandEnvironmentStringsW(wide.as_ptr(), buf.as_mut_ptr(), buf.len() as u32) };
    if n == 0 || n as usize > buf.len() {
        return s.to_string();
    }
    String::from_utf16_lossy(&buf[..n as usize - 1])
}

fn known_dir(id: windows_sys::core::GUID) -> Option<PathBuf> {
    use windows_sys::Win32::System::Com::CoTaskMemFree;
    use windows_sys::Win32::UI::Shell::SHGetKnownFolderPath;
    let mut raw = std::ptr::null_mut();
    // SAFETY: out-pointer is valid; the string is freed with CoTaskMemFree below.
    let hr = unsafe { SHGetKnownFolderPath(&id, 0, std::ptr::null_mut(), &mut raw) };
    if hr != 0 || raw.is_null() {
        return None;
    }
    let mut len = 0usize;
    // SAFETY: the system returned a NUL-terminated wide string.
    let text = unsafe {
        while len < 32_768 && *raw.add(len) != 0 {
            len += 1;
        }
        let s = String::from_utf16_lossy(std::slice::from_raw_parts(raw, len));
        CoTaskMemFree(raw.cast());
        s
    };
    (!text.is_empty()).then(|| PathBuf::from(text))
}

fn read_tasks() -> Result<Vec<StartupItem>, String> {
    crate::background::taskstore::on_com_thread(|| {
        let service = crate::background::taskstore::service()?;
        let root = unsafe { service.GetFolder(&windows::core::BSTR::from("\\"))? };
        let mut raw = Vec::new();
        walk(&root, 0, &mut raw)?;
        Ok(raw)
    })
}

fn walk(
    folder: &windows::Win32::System::TaskScheduler::ITaskFolder,
    depth: i32,
    out: &mut Vec<StartupItem>,
) -> windows::core::Result<()> {
    if depth > 6 {
        return Ok(());
    }
    unsafe {
        let path = tidy_folder(folder.Path()?.to_string());
        let low = path.to_lowercase();
        if low == "\\microsoft" || low.starts_with("\\microsoft\\") {
            return Ok(());
        }
        let tasks = folder.GetTasks(1)?;
        for i in 1..=tasks.Count()? {
            let task = tasks.get_Item(&crate::background::taskstore::variant_i4(i))?;
            if let Some(row) = task_item(&task, &path) {
                out.push(row);
            }
        }
        let subs = folder.GetFolders(0)?;
        for i in 1..=subs.Count()? {
            let sub = subs.get_Item(&crate::background::taskstore::variant_i4(i))?;
            let _ = walk(&sub, depth + 1, out);
        }
    }
    Ok(())
}

fn tidy_folder(path: String) -> String {
    if path.len() > 1 {
        path.trim_end_matches('\\').to_string()
    } else if path.is_empty() {
        "\\".to_string()
    } else {
        path
    }
}

fn task_item(
    task: &windows::Win32::System::TaskScheduler::IRegisteredTask,
    folder: &str,
) -> Option<StartupItem> {
    use windows::core::Interface;
    use windows::Win32::Foundation::VARIANT_TRUE;
    use windows::Win32::System::TaskScheduler::{
        IExecAction, TASK_ACTION_EXEC, TASK_TRIGGER_LOGON,
    };
    unsafe {
        let name = task.Name().ok()?.to_string();
        let actual = task.Path().ok()?.to_string();
        if !actual.eq_ignore_ascii_case(&task_path(folder, &name)) {
            return None;
        }
        let def = task.Definition().ok()?;
        let triggers = def.Triggers().ok()?;
        let mut count = 0i32;
        triggers.Count(&mut count).ok()?;
        if count <= 0 {
            return None;
        }
        let mut logon = 0i32;
        for i in 1..=count {
            let trigger = triggers.get_Item(i).ok()?;
            let mut kind = TASK_TRIGGER_LOGON;
            trigger.Type(&mut kind).ok()?;
            if kind == TASK_TRIGGER_LOGON {
                logon += 1;
            }
        }
        // A task that also runs on a timer is not a sign-in item. Disabling it would stop that too.
        if logon != count {
            return None;
        }
        let actions = def.Actions().ok()?;
        let mut action_count = 0i32;
        actions.Count(&mut action_count).ok()?;
        let mut command = String::new();
        for i in 1..=action_count {
            let action = actions.get_Item(i).ok()?;
            let mut kind = TASK_ACTION_EXEC;
            if action.Type(&mut kind).is_err() || kind != TASK_ACTION_EXEC {
                continue;
            }
            let exec: IExecAction = action.cast().ok()?;
            let mut path = windows::core::BSTR::new();
            if exec.Path(&mut path).is_ok() {
                command = path.to_string();
                break;
            }
        }
        let enabled = task.Enabled().ok()? == VARIANT_TRUE;
        finish(
            StartupPlace::Task,
            &name,
            folder,
            &expand_env(&command),
            if enabled {
                Some(&[0x02])
            } else {
                Some(&[0x03])
            },
        )
    }
}

fn load_task(folder: &str, name: &str) -> Result<Option<StartupItem>, AppError> {
    let folder = folder.to_string();
    let name = name.to_string();
    crate::background::taskstore::on_com_thread(move || {
        use windows::core::BSTR;
        let service = crate::background::taskstore::service()?;
        let f = unsafe { service.GetFolder(&BSTR::from(folder.as_str()))? };
        let task = unsafe { f.GetTask(&BSTR::from(name.as_str()))? };
        Ok(task_item(&task, &folder))
    })
    .map_err(|e| {
        let low = e.to_lowercase();
        if low.contains("not found") || low.contains("0x80070002") {
            missing()
        } else if low.contains("access is denied") || low.contains("0x80070005") {
            denied()
        } else {
            missing()
        }
    })
}

fn set_task(folder: &str, name: &str, enabled: bool) -> Result<(), AppError> {
    let folder = folder.to_string();
    let name = name.to_string();
    crate::background::taskstore::on_com_thread(move || {
        use windows::core::BSTR;
        use windows::Win32::Foundation::{VARIANT_FALSE, VARIANT_TRUE};
        let service = crate::background::taskstore::service()?;
        let f = unsafe { service.GetFolder(&BSTR::from(folder.as_str()))? };
        let task = unsafe { f.GetTask(&BSTR::from(name.as_str()))? };
        // Read again on this thread, and refuse a Windows or security task even if the page asked.
        match task_item(&task, &folder) {
            Some(row) if row.lock.is_none() && row.name.eq_ignore_ascii_case(&name) => {}
            _ => {
                return Err(windows::core::Error::from_hresult(windows::core::HRESULT(
                    0x8000_4005_u32 as i32,
                )))
            }
        }
        let flag = if enabled { VARIANT_TRUE } else { VARIANT_FALSE };
        unsafe { task.SetEnabled(flag)? };
        Ok(())
    })
    .map_err(|e| {
        let low = e.to_lowercase();
        if low.contains("access is denied") || low.contains("0x80070005") {
            denied()
        } else {
            AppError::new("startup", "That startup item couldn't be changed.")
        }
    })
}
