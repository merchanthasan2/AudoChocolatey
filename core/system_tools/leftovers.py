"""Leftover folders after uninstall: scan, recycle, and stale registry cleanup."""

from __future__ import annotations

import hashlib
import json
import os
import secrets
import time
import uuid
import winreg
from typing import Any, Callable, Dict, List, Optional, Set, Tuple

ProgressCallback = Callable[..., None]

from core.admin import is_admin

MIN_AGE_DAYS = 90
TOKEN_TTL_S = 300

SHARED_RUNTIME_DIR_NAMES = frozenset(
    {
        "microsoft.net",
        "microsoft visual studio",
        "microsoft sdks",
        "windows kits",
        "msbuild",
        "package cache",
        "microsoft shared",
        "common files",
        "dotnet",
        "nugetpackages",
        "reference assemblies",
    }
)

_last_scan_folders: Dict[str, Dict[str, Any]] = {}
_registry_catalog: Dict[str, Dict[str, Any]] = {}
_reg_confirm_tokens: Dict[str, Dict[str, Any]] = {}

_test_backup_root: Optional[str] = None
_test_force_admin: Optional[bool] = None


def configure_test_hooks(*, backup_root: Optional[str] = None) -> None:
    global _test_backup_root
    _test_backup_root = backup_root


def reset_test_hooks() -> None:
    configure_test_hooks(backup_root=None)


def _norm(path: str) -> str:
    return os.path.normcase(os.path.normpath(path))


def _quantum_dir() -> str:
    root = os.path.join(os.environ.get("LOCALAPPDATA", ""), "QuantumUpdater")
    os.makedirs(root, exist_ok=True)
    return root


def _registry_backups_dir() -> str:
    if _test_backup_root:
        os.makedirs(_test_backup_root, exist_ok=True)
        return _test_backup_root
    path = os.path.join(_quantum_dir(), "registry-backups")
    os.makedirs(path, exist_ok=True)
    return path


def _history_path() -> str:
    return os.path.join(_quantum_dir(), "history.jsonl")


def _log_action(record: Dict[str, Any]) -> None:
    record = {**record, "ts": time.time()}
    try:
        with open(_history_path(), "a", encoding="utf-8") as fh:
            fh.write(json.dumps(record) + "\n")
    except OSError:
        pass


def _folder_item_id(folder: str) -> str:
    return hashlib.sha256(f"folder\0{folder}".encode("utf-8")).hexdigest()[:24]


def _registry_entry_id(hive_name: str, subkey: str) -> str:
    blob = f"reg\0{hive_name}\0{subkey}".encode("utf-8")
    return hashlib.sha256(blob).hexdigest()[:24]


def _allowed_removal_roots() -> List[str]:
    home = os.path.expanduser("~")
    roots = [
        os.path.join(home, "AppData", "Local"),
        os.path.join(home, "AppData", "Roaming"),
        os.environ.get("ProgramFiles", r"C:\Program Files"),
        os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"),
        os.environ.get("ProgramData", r"C:\ProgramData"),
    ]
    return [_norm(os.path.abspath(r)) for r in roots if r and os.path.isdir(r)]


def _under_allowed_scope(path: str) -> bool:
    p = _norm(os.path.abspath(path))
    for root in _allowed_removal_roots():
        if p == root or p.startswith(root + os.sep):
            return True
    return False


def _needs_admin_for_path(path: str) -> bool:
    p = _norm(os.path.abspath(path))
    elevated_roots = [
        _norm(os.path.abspath(os.environ.get("ProgramFiles", r"C:\Program Files"))),
        _norm(os.path.abspath(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"))),
        _norm(os.path.abspath(os.environ.get("ProgramData", r"C:\ProgramData"))),
    ]
    for root in elevated_roots:
        if root and (p == root or p.startswith(root + os.sep)):
            return True
    return False


def _is_shared_runtime(path: str) -> bool:
    parts = [seg.casefold() for seg in os.path.normpath(path).split(os.sep) if seg]
    return any(seg in SHARED_RUNTIME_DIR_NAMES for seg in parts)


def _read_uninstall_install_locations() -> Dict[str, Dict[str, str]]:
    roots: List[Tuple[Any, str, bool]] = [
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", True),
        (
            winreg.HKEY_LOCAL_MACHINE,
            r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            True,
        ),
        (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", False),
    ]
    out: Dict[str, Dict[str, str]] = {}
    for hive, subkey, wow in roots:
        access = winreg.KEY_READ
        if wow:
            access |= winreg.KEY_WOW64_64KEY
        try:
            with winreg.OpenKey(hive, subkey, 0, access) as key:
                i = 0
                while True:
                    try:
                        name = winreg.EnumKey(key, i)
                        i += 1
                    except OSError:
                        break
                    full = f"{subkey}\\{name}"
                    loc = _reg_str(hive, full, "InstallLocation", wow)
                    if not loc:
                        continue
                    loc = loc.strip('" ')
                    if not loc:
                        continue
                    abs_loc = _norm(os.path.abspath(loc))
                    if not os.path.isdir(abs_loc):
                        continue
                    display = _reg_str(hive, full, "DisplayName", wow)
                    publisher = _reg_str(hive, full, "Publisher", wow)
                    out[abs_loc] = {
                        "program": display or name,
                        "publisher": publisher,
                    }
        except OSError:
            continue
    return out


def _hive_name(hive) -> str:
    if hive == winreg.HKEY_LOCAL_MACHINE:
        return "HKLM"
    if hive == winreg.HKEY_CURRENT_USER:
        return "HKCU"
    return "HKU"


def _hive_from_name(name: str):
    if name == "HKLM":
        return winreg.HKEY_LOCAL_MACHINE
    if name == "HKCU":
        return winreg.HKEY_CURRENT_USER
    raise ValueError(f"Unknown hive: {name}")


def _reg_str(hive, subkey: str, value: str, wow: bool) -> str:
    access = winreg.KEY_READ
    if wow:
        access |= winreg.KEY_WOW64_64KEY
    try:
        with winreg.OpenKey(hive, subkey, 0, access) as key:
            raw, _ = winreg.QueryValueEx(key, value)
            return str(raw).strip() if raw is not None else ""
    except OSError:
        return ""


def _quantum_history_removed() -> List[Dict[str, Any]]:
    path = _history_path()
    if not os.path.isfile(path):
        return []
    rows: List[Dict[str, Any]] = []
    try:
        with open(path, "r", encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    row = json.loads(line)
                except json.JSONDecodeError:
                    continue
                action = (row.get("action") or "").lower()
                if "uninstall" in action or row.get("type") == "uninstall":
                    rows.append(row)
    except OSError:
        pass
    return rows


def _folder_size_and_mtime(path: str) -> Tuple[int, float]:
    total = 0
    latest = 0.0
    try:
        for root, _dirs, files in os.walk(path):
            for name in files:
                fp = os.path.join(root, name)
                try:
                    st = os.stat(fp, follow_symlinks=False)
                    total += st.st_size
                    latest = max(latest, st.st_mtime)
                except OSError:
                    continue
    except OSError:
        pass
    if latest == 0.0:
        try:
            latest = os.path.getmtime(path)
        except OSError:
            latest = 0.0
    return total, latest


def _candidate_appdata_roots() -> List[str]:
    home = os.path.expanduser("~")
    roots = [
        os.environ.get("LOCALAPPDATA", os.path.join(home, "AppData", "Local")),
        os.environ.get("APPDATA", os.path.join(home, "AppData", "Roaming")),
        os.environ.get("ProgramData", r"C:\ProgramData"),
    ]
    return [r for r in roots if r and os.path.isdir(r)]


def _exact_folder_match(roots: List[str], program: str, publisher: str) -> List[str]:
    matches: List[str] = []
    names: Set[str] = set()
    prog = (program or "").strip()
    pub = (publisher or "").strip()
    if prog:
        names.add(prog)
    if pub and prog:
        names.add(f"{pub}\\{prog}".replace("/", "\\"))
        names.add(os.path.join(pub, prog))
    for root in roots:
        try:
            for ent in os.scandir(root):
                if not ent.is_dir(follow_symlinks=False):
                    continue
                if ent.name in names:
                    matches.append(ent.path)
                if pub:
                    nested = os.path.join(ent.path, prog) if prog else ""
                    if nested and os.path.isdir(nested) and os.path.basename(nested) == prog:
                        parent_name = os.path.join(ent.name, prog)
                        if parent_name in names or ent.name == pub:
                            matches.append(nested)
        except OSError:
            continue
    return matches


def _resolve_registry_ref(row: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    """Map a history row to a still-present uninstall registry key (no guessing)."""
    hive_name = row.get("registry_hive") or row.get("hive") or ""
    subkey = row.get("registry_subkey") or row.get("registry_path") or ""
    wow = bool(row.get("registry_wow64", row.get("wow64", False)))
    if not subkey:
        return None
    if hive_name:
        try:
            hive = _hive_from_name(hive_name)
        except ValueError:
            return None
    else:
        hive = winreg.HKEY_CURRENT_USER
    try:
        access = winreg.KEY_READ
        if wow and hive == winreg.HKEY_LOCAL_MACHINE:
            access |= winreg.KEY_WOW64_64KEY
        with winreg.OpenKey(hive, subkey, 0, access):
            pass
    except OSError:
        return None
    return {
        "hive": hive,
        "hive_name": hive_name or _hive_name(hive),
        "subkey": subkey,
        "wow64": wow,
        "program": row.get("program") or row.get("name") or "",
    }


def _orphan_install_locations(
    active: Dict[str, Dict[str, str]],
    progress_cb: Optional[ProgressCallback] = None,
) -> List[Dict[str, Any]]:
    active_keys = set(active.keys())
    suspects: Dict[str, Dict[str, Any]] = {}

    for row in _quantum_history_removed():
        loc = (row.get("install_location") or row.get("folder") or "").strip()
        if loc and os.path.isdir(loc):
            key = _norm(os.path.abspath(loc))
            if key not in active_keys:
                meta: Dict[str, Any] = {
                    "program": row.get("program") or row.get("name") or "Removed program",
                    "publisher": row.get("publisher") or "",
                }
                reg = _resolve_registry_ref(row)
                if reg:
                    rid = _registry_entry_id(reg["hive_name"], reg["subkey"])
                    meta["registry_entry_id"] = rid
                    meta["_registry"] = reg
                suspects[key] = meta

    seen_folder: Set[str] = set()
    for row in _quantum_history_removed():
        program = row.get("program") or row.get("name") or ""
        publisher = row.get("publisher") or ""
        if not program:
            continue
        for folder in _exact_folder_match(_candidate_appdata_roots(), program, publisher):
            key = _norm(folder)
            if key in active_keys or key in seen_folder:
                continue
            if not os.path.isdir(folder):
                continue
            seen_folder.add(key)
            meta = {
                "program": program,
                "publisher": publisher,
            }
            reg = _resolve_registry_ref(row)
            if reg:
                rid = _registry_entry_id(reg["hive_name"], reg["subkey"])
                meta["registry_entry_id"] = rid
                meta["_registry"] = reg
            suspects.setdefault(key, meta)

    suspect_items = list(suspects.items())
    results: List[Dict[str, Any]] = []
    for folder, meta in suspect_items:
        if progress_cb:
            progress_cb(percent=None, current_item=folder)
        if folder in active_keys:
            continue
        if not os.path.isdir(folder):
            continue
        size, mtime = _folder_size_and_mtime(folder)
        from core.system_tools.large_files import human_size

        item: Dict[str, Any] = {
            "id": _folder_item_id(folder),
            "program": meta.get("program") or "Removed program",
            "folder": folder,
            "size_bytes": size,
            "size_str": human_size(size),
            "last_modified": mtime,
        }
        if meta.get("registry_entry_id"):
            item["registry_entry_id"] = meta["registry_entry_id"]
            item["_registry"] = meta.get("_registry")
        results.append(item)
    return results


def _folder_has_open_handle(path: str) -> bool:
    if os.name != "nt":
        return False
    try:
        import ctypes

        GENERIC_READ = 0x80000000
        OPEN_EXISTING = 3
        FILE_SHARE_NONE = 0
        INVALID = ctypes.c_void_p(-1).value

        checked = 0
        for root, _dirs, files in os.walk(path):
            for name in files:
                if checked > 40:
                    return False
                fp = os.path.join(root, name)
                handle = ctypes.windll.kernel32.CreateFileW(
                    fp,
                    GENERIC_READ,
                    FILE_SHARE_NONE,
                    None,
                    OPEN_EXISTING,
                    0,
                    None,
                )
                checked += 1
                if handle == INVALID:
                    err = ctypes.get_last_error()
                    if err in (32, 33):
                        return True
                else:
                    ctypes.windll.kernel32.CloseHandle(handle)
        probe = os.path.join(path, ".quantum_lock_probe")
        try:
            with open(probe, "xb"):
                pass
            os.remove(probe)
        except FileExistsError:
            try:
                os.remove(probe)
            except OSError:
                pass
        except PermissionError:
            return True
        except OSError:
            pass
    except Exception:
        return False
    return False


def _removal_refusal(folder: str, last_modified: float) -> Tuple[bool, str, bool]:
    """Return (can_recycle, reason, needs_admin)."""
    if not _under_allowed_scope(folder):
        return False, "This folder is outside the allowed removal scope.", False
    if _is_shared_runtime(folder):
        return False, "Shared runtime folders are kept.", False
    age_s = time.time() - last_modified
    if age_s < MIN_AGE_DAYS * 86400:
        return False, "This folder was modified within the last 90 days.", False
    if _folder_has_open_handle(folder):
        return False, "A program still has this folder open.", False
    needs_admin = _needs_admin_for_path(folder)
    if needs_admin and not is_admin() and _test_force_admin is not False:
        if _test_force_admin is not True:
            return False, "Administrator is required for this folder.", True
    return True, "", needs_admin


def _move_folder_to_recycle(folder: str) -> Tuple[bool, str]:
    if os.name != "nt":
        return False, "Not supported on this platform."
    try:
        import ctypes
        from ctypes import wintypes

        FO_DELETE = 0x0003
        FOF_ALLOWUNDO = 0x0040
        FOF_NOCONFIRMATION = 0x0010
        FOF_SILENT = 0x0004
        FOF_NOERRORUI = 0x0400

        class SHFILEOPSTRUCTW(ctypes.Structure):
            _fields_ = [
                ("hwnd", wintypes.HWND),
                ("wFunc", wintypes.UINT),
                ("pFrom", wintypes.LPCWSTR),
                ("pTo", wintypes.LPCWSTR),
                ("fFlags", wintypes.WORD),
                ("fAnyOperationsAborted", wintypes.BOOL),
                ("hNameMappings", wintypes.LPVOID),
                ("lpszProgressTitle", wintypes.LPCWSTR),
            ]

        target = os.path.abspath(folder)
        if not os.path.isdir(target):
            return False, "Folder not found."
        payload = target + "\0\0"
        op = SHFILEOPSTRUCTW()
        op.wFunc = FO_DELETE
        op.pFrom = payload
        op.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
        code = ctypes.windll.shell32.SHFileOperationW(ctypes.byref(op))
        if code != 0 or op.fAnyOperationsAborted:
            return False, "Could not move the folder to the Recycle Bin."
        return True, "ok"
    except Exception as exc:
        return False, str(exc)


def _serialize_reg(val: Any, reg_type: int) -> Any:
    if reg_type in (winreg.REG_SZ, winreg.REG_EXPAND_SZ):
        return str(val)
    if reg_type == winreg.REG_DWORD:
        return int(val)
    if reg_type == winreg.REG_QWORD:
        return int(val)
    if reg_type == winreg.REG_BINARY:
        return bytes(val).hex()
    if reg_type == winreg.REG_MULTI_SZ:
        return list(val)
    return str(val)


def _deserialize_reg(data: Any, reg_type: int) -> Any:
    if reg_type in (winreg.REG_SZ, winreg.REG_EXPAND_SZ):
        return str(data)
    if reg_type == winreg.REG_DWORD:
        return int(data)
    if reg_type == winreg.REG_QWORD:
        return int(data)
    if reg_type == winreg.REG_BINARY:
        if isinstance(data, str):
            return bytes.fromhex(data)
        return bytes(data)
    if reg_type == winreg.REG_MULTI_SZ:
        return list(data)
    return data


def _export_registry_key(hive, subkey: str, wow64: bool) -> Dict[str, Any]:
    access = winreg.KEY_READ
    if wow64 and hive == winreg.HKEY_LOCAL_MACHINE:
        access |= winreg.KEY_WOW64_64KEY
    values: Dict[str, Any] = {}
    with winreg.OpenKey(hive, subkey, 0, access) as key:
        i = 0
        while True:
            try:
                name, val, typ = winreg.EnumValue(key, i)
                values[name] = {"type": int(typ), "data": _serialize_reg(val, typ)}
                i += 1
            except OSError:
                break
    return values


def _write_registry_backup(entry_id: str, hive_name: str, subkey: str, wow64: bool, values: Dict[str, Any]) -> str:
    backup_id = uuid.uuid4().hex
    payload = {
        "backup_id": backup_id,
        "entry_id": entry_id,
        "hive": hive_name,
        "subkey": subkey,
        "wow64": wow64,
        "values": values,
        "exported_at": time.time(),
    }
    path = os.path.join(_registry_backups_dir(), f"{backup_id}.json")
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, indent=2)
    os.replace(tmp, path)
    return backup_id


def _backup_path(backup_id: str) -> str:
    return os.path.join(_registry_backups_dir(), f"{backup_id}.json")


def _load_backup(backup_id: str) -> Optional[Dict[str, Any]]:
    path = _backup_path(backup_id)
    if not os.path.isfile(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, json.JSONDecodeError, TypeError):
        return None


def _delete_registry_key(hive, subkey: str, wow64: bool) -> bool:
    access = winreg.KEY_SET_VALUE | winreg.KEY_ENUMERATE_SUB_KEYS
    if wow64 and hive == winreg.HKEY_LOCAL_MACHINE:
        access |= winreg.KEY_WOW64_64KEY
    try:
        with winreg.OpenKey(hive, subkey, 0, access) as key:
            while True:
                try:
                    name, _, _ = winreg.EnumValue(key, 0)
                    winreg.DeleteValue(key, name)
                except OSError:
                    break
        parent, leaf = subkey.rsplit("\\", 1)
        parent_access = winreg.KEY_WRITE
        if wow64 and hive == winreg.HKEY_LOCAL_MACHINE:
            parent_access |= winreg.KEY_WOW64_64KEY
        with winreg.OpenKey(hive, parent, 0, parent_access) as parent_key:
            winreg.DeleteKey(parent_key, leaf)
        return True
    except OSError:
        return False


def _restore_registry_key(payload: Dict[str, Any]) -> Tuple[bool, str]:
    hive_name = payload.get("hive", "")
    subkey = payload.get("subkey", "")
    wow64 = bool(payload.get("wow64"))
    values = payload.get("values") or {}
    try:
        hive = _hive_from_name(hive_name)
    except ValueError:
        return False, "Backup hive is not valid."
    access = winreg.KEY_WRITE | winreg.KEY_SET_VALUE
    if wow64 and hive == winreg.HKEY_LOCAL_MACHINE:
        access |= winreg.KEY_WOW64_64KEY
    try:
        with winreg.CreateKeyEx(hive, subkey, 0, access) as key:
            for name, spec in values.items():
                typ = int(spec.get("type", winreg.REG_SZ))
                data = _deserialize_reg(spec.get("data"), typ)
                winreg.SetValueEx(key, name, 0, typ, data)
    except OSError as exc:
        return False, str(exc)
    return True, "ok"


def leftovers_scan(progress_cb: Optional[ProgressCallback] = None) -> Dict[str, Any]:
    started = time.perf_counter()
    global _last_scan_folders, _registry_catalog
    if os.name != "nt":
        return {"items": [], "count": 0, "elapsed_s": 0.0}
    if progress_cb:
        progress_cb(percent=None, current_item="Reading installed programs")
    active = _read_uninstall_install_locations()
    if progress_cb:
        progress_cb(percent=None, current_item="Finding leftover folders")
    raw_items = _orphan_install_locations(active, progress_cb=progress_cb)
    items: List[Dict[str, Any]] = []
    folder_map: Dict[str, Dict[str, Any]] = {}
    reg_map: Dict[str, Dict[str, Any]] = {}

    for row in raw_items:
        folder = row["folder"]
        can, reason, needs_admin = _removal_refusal(folder, float(row.get("last_modified") or 0))
        public = {
            "id": row["id"],
            "program": row["program"],
            "folder": folder,
            "size_bytes": row["size_bytes"],
            "size_str": row["size_str"],
            "last_modified": row["last_modified"],
            "can_recycle": can,
            "refuse_reason": reason,
            "needs_admin": needs_admin,
            "folder_present": os.path.isdir(folder),
        }
        rid = row.get("registry_entry_id")
        if rid:
            public["registry_entry_id"] = rid
            reg_map[rid] = {
                "entry_id": rid,
                "folder": folder,
                "folder_id": row["id"],
                **(row.get("_registry") or {}),
            }
        folder_map[_norm(folder)] = {**row, **public}
        items.append(public)

    _last_scan_folders = folder_map
    _registry_catalog = reg_map
    if progress_cb:
        progress_cb(percent=None, current_item="")
    elapsed = time.perf_counter() - started
    return {"items": items, "count": len(items), "elapsed_s": round(elapsed, 3)}


def leftovers_reveal(path: str) -> Dict[str, Any]:
    from core.system_tools.large_files import reveal_in_explorer

    return reveal_in_explorer(path)


def leftovers_recycle(path: str, *, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"success": False, "message": "Per-folder confirmation is required."}
    folder = os.path.abspath(path)
    key = _norm(folder)
    item = _last_scan_folders.get(key)
    if not item:
        leftovers_scan()
        item = _last_scan_folders.get(key)
    if not item:
        return {"success": False, "message": "Folder was not part of the latest leftovers scan."}
    can, reason, needs_admin = _removal_refusal(folder, float(item.get("last_modified") or 0))
    if not can:
        return {"success": False, "message": reason or "This folder cannot be removed.", "needs_admin": needs_admin}
    ok, msg = _move_folder_to_recycle(folder)
    if not ok:
        return {"success": False, "message": msg}
    _log_action(
        {
            "action": "leftovers_recycle",
            "folder": folder,
            "program": item.get("program", ""),
            "registry_entry_id": item.get("registry_entry_id"),
        }
    )
    item["folder_present"] = os.path.isdir(folder)
    return {"success": True, "folder": folder, "folder_present": item["folder_present"]}


def leftovers_confirm_registry(entry_id: str) -> Dict[str, Any]:
    reg = _registry_catalog.get(entry_id)
    if not reg:
        leftovers_scan()
        reg = _registry_catalog.get(entry_id)
    if not reg:
        return {"success": False, "message": "Registry entry was not part of the latest leftovers scan."}
    folder = reg.get("folder", "")
    if folder and os.path.isdir(folder):
        return {"success": False, "message": "Remove the leftover folder before removing the uninstall entry."}
    hive = reg["hive"]
    subkey = reg["subkey"]
    wow64 = bool(reg.get("wow64"))
    try:
        values = _export_registry_key(hive, subkey, wow64)
    except OSError:
        return {"success": False, "message": "Could not read the uninstall registry entry."}
    backup_id = _write_registry_backup(entry_id, reg["hive_name"], subkey, wow64, values)
    token = secrets.token_urlsafe(24)
    _reg_confirm_tokens[token] = {
        "entry_id": entry_id,
        "backup_id": backup_id,
        "expires_at": time.time() + TOKEN_TTL_S,
    }
    return {
        "success": True,
        "token": token,
        "backup_id": backup_id,
        "entry_id": entry_id,
        "expires_at": _reg_confirm_tokens[token]["expires_at"],
    }


def leftovers_delete_registry(entry_id: str, token: str) -> Dict[str, Any]:
    slot = _reg_confirm_tokens.get(token)
    if not slot or slot.get("entry_id") != entry_id:
        return {"success": False, "message": "Confirmation expired or invalid."}
    if time.time() > float(slot.get("expires_at", 0)):
        _reg_confirm_tokens.pop(token, None)
        return {"success": False, "message": "Confirmation expired or invalid."}
    backup_id = slot.get("backup_id", "")
    backup = _load_backup(backup_id)
    if not backup or backup.get("entry_id") != entry_id:
        return {"success": False, "message": "Registry backup is missing. Export a backup before deleting."}
    reg = _registry_catalog.get(entry_id)
    if not reg:
        reg = {
            "hive": _hive_from_name(backup["hive"]),
            "hive_name": backup["hive"],
            "subkey": backup["subkey"],
            "wow64": bool(backup.get("wow64")),
        }
    folder = reg.get("folder", "")
    if folder and os.path.isdir(folder):
        return {"success": False, "message": "Remove the leftover folder before removing the uninstall entry."}
    if not _delete_registry_key(reg["hive"], reg["subkey"], bool(reg.get("wow64"))):
        return {"success": False, "message": "Could not delete the uninstall registry entry."}
    _reg_confirm_tokens.pop(token, None)
    _log_action(
        {
            "action": "leftovers_registry_delete",
            "entry_id": entry_id,
            "backup_id": backup_id,
        }
    )
    return {"success": True, "entry_id": entry_id, "backup_id": backup_id}


def leftovers_restore_registry(backup_id: str) -> Dict[str, Any]:
    payload = _load_backup(backup_id)
    if not payload:
        return {"success": False, "message": "Backup not found."}
    ok, msg = _restore_registry_key(payload)
    if not ok:
        return {"success": False, "message": msg or "Restore failed."}
    _log_action({"action": "leftovers_registry_restore", "backup_id": backup_id})
    return {"success": True, "backup_id": backup_id, "entry_id": payload.get("entry_id")}
