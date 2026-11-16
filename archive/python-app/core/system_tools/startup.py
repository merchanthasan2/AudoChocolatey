"""Startup programs: registry, folders, and sign-in scheduled tasks."""

from __future__ import annotations

import hashlib
import json
import os
import re
import struct
import subprocess
import time
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any, Callable, Dict, List, Optional, Tuple

if os.name == "nt":
    import winreg
else:
    winreg = None  # type: ignore

SOURCE_HKCU_RUN = "Starts with Windows for you"
SOURCE_HKLM_RUN = "Starts for everyone on this PC"
SOURCE_STARTUP_FOLDER = "Startup folder"
SOURCE_SIGNIN_TASK = "Sign-in task"

_APPROVED_RUN = r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
_APPROVED_RUN32 = r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32"
_APPROVED_FOLDER = r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder"

_RUN_HKCU = r"Software\Microsoft\Windows\CurrentVersion\Run"
_RUN_HKLM = r"Software\Microsoft\Windows\CurrentVersion\Run"
_RUNONCE_HKCU = r"Software\Microsoft\Windows\CurrentVersion\RunOnce"
_RUNONCE_HKLM = r"Software\Microsoft\Windows\CurrentVersion\RunOnce"
_RUN_HKLM_WOW = r"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"

_ENABLED_BYTES = frozenset({0x02, 0x06})
_DISABLED_BYTE = 0x03

_REFUSE_NAMES = frozenset(
    {
        "securityhealth",
        "windows security notification",
        "windows defender",
        "defender",
        "smartscreen",
    }
)

_KNOWN_AV_EXE = frozenset(
    {
        "msmpeng.exe",
        "msseces.exe",
        "avp.exe",
        "avgui.exe",
        "avastsvc.exe",
        "avastui.exe",
        "mbam.exe",
        "mbamservice.exe",
        "nortonsecurity.exe",
        "mcshield.exe",
        "sophosui.exe",
        "bdagent.exe",
    }
)

_entries_cache: List[Dict[str, Any]] = []
_journal_disabled_ids: set = set()

_test_hkcu_run_subkey: Optional[str] = None
_test_journal_path: Optional[str] = None
_test_skip_tasks: bool = False
_test_skip_folders: bool = False


def configure_test_hooks(
    *,
    hkcu_run_subkey: Optional[str] = None,
    journal_path: Optional[str] = None,
    skip_tasks: bool = True,
    skip_folders: bool = True,
) -> None:
    """Point startup I/O at a isolated registry fixture (unit tests)."""
    global _test_hkcu_run_subkey, _test_journal_path, _test_skip_tasks, _test_skip_folders
    _test_hkcu_run_subkey = hkcu_run_subkey
    _test_journal_path = journal_path
    _test_skip_tasks = skip_tasks
    _test_skip_folders = skip_folders
    _entries_cache.clear()


def reset_test_hooks() -> None:
    configure_test_hooks(hkcu_run_subkey=None, journal_path=None, skip_tasks=False, skip_folders=False)


def _journal_path() -> str:
    if _test_journal_path:
        os.makedirs(os.path.dirname(_test_journal_path), exist_ok=True)
        return _test_journal_path
    root = os.path.join(os.environ.get("LOCALAPPDATA", ""), "QuantumUpdater")
    os.makedirs(root, exist_ok=True)
    return os.path.join(root, "startup-journal.json")


def _load_journal() -> List[Dict[str, Any]]:
    path = _journal_path()
    if not os.path.isfile(path):
        return []
    try:
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        if isinstance(data, list):
            return data
    except (OSError, json.JSONDecodeError, TypeError):
        pass
    return []


def _save_journal(rows: List[Dict[str, Any]]) -> None:
    path = _journal_path()
    tmp = path + ".tmp"
    try:
        with open(tmp, "w", encoding="utf-8") as fh:
            json.dump(rows[-200:], fh, indent=2)
        os.replace(tmp, path)
    except OSError:
        try:
            os.remove(tmp)
        except OSError:
            pass


def _append_journal(record: Dict[str, Any]) -> None:
    rows = _load_journal()
    record = {**record, "ts": time.time()}
    rows.append(record)
    _save_journal(rows)


def stable_entry_id(source: str, name: str) -> str:
    blob = f"{source}\0{name}".encode("utf-8")
    return hashlib.sha256(blob).hexdigest()[:24]


def _filetime_now_bytes() -> bytes:
    ticks = int((time.time() + 11644473600) * 10_000_000)
    return struct.pack("<Q", ticks)


def decode_startup_approved(raw: Optional[bytes]) -> Tuple[bool, bytes]:
    """Return (enabled, full 12-byte value to write back)."""
    if not raw or len(raw) < 1:
        base = bytearray(12)
        base[0] = 0x02
        return True, bytes(base)
    buf = bytearray(raw[:12])
    while len(buf) < 12:
        buf.append(0)
    enabled = buf[0] in _ENABLED_BYTES
    return enabled, bytes(buf)


def encode_startup_approved(enabled: bool, previous: Optional[bytes] = None) -> bytes:
    _, base = decode_startup_approved(previous)
    buf = bytearray(base)
    if enabled:
        buf[0] = 0x02 if buf[0] not in _ENABLED_BYTES else buf[0]
        for i in range(4, 12):
            buf[i] = 0
    else:
        buf[0] = _DISABLED_BYTE
        ft = _filetime_now_bytes()
        for i, b in enumerate(ft):
            buf[4 + i] = b
    return bytes(buf)


def _reg_enum_values(hkey, path: str, wow: bool = False) -> Dict[str, str]:
    out: Dict[str, str] = {}
    if winreg is None:
        return out
    access = winreg.KEY_READ
    if wow:
        access |= getattr(winreg, "KEY_WOW64_64KEY", 0)
    try:
        with winreg.OpenKey(hkey, path, 0, access) as key:
            i = 0
            while True:
                try:
                    name, val, _ = winreg.EnumValue(key, i)
                    if isinstance(val, str):
                        out[name] = val
                    i += 1
                except OSError:
                    break
    except OSError:
        pass
    return out


def _reg_read_binary(hkey, subkey: str, value_name: str, wow: bool = False) -> Optional[bytes]:
    if winreg is None:
        return None
    access = winreg.KEY_READ
    if wow and hkey == winreg.HKEY_LOCAL_MACHINE:
        access |= getattr(winreg, "KEY_WOW64_64KEY", 0)
    try:
        with winreg.OpenKey(hkey, subkey, 0, access) as key:
            val, _ = winreg.QueryValueEx(key, value_name)
            if isinstance(val, bytes):
                return val
    except OSError:
        pass
    return None


def _reg_write_binary(hkey, subkey: str, value_name: str, data: bytes, wow: bool = False) -> bool:
    if winreg is None:
        return False
    access = winreg.KEY_SET_VALUE
    if wow and hkey == winreg.HKEY_LOCAL_MACHINE:
        access |= getattr(winreg, "KEY_WOW64_64KEY", 0)
    try:
        with winreg.OpenKey(hkey, subkey, 0, access) as key:
            winreg.SetValueEx(key, value_name, 0, winreg.REG_BINARY, data)
        return True
    except OSError:
        return False


def _parse_command_target(command: str) -> str:
    cmd = (command or "").strip()
    if not cmd:
        return ""
    if cmd.lower().startswith("rundll32"):
        return cmd
    m = re.match(r'^"([^"]+)"', cmd)
    if m:
        return m.group(1)
    parts = cmd.split()
    return parts[0] if parts else cmd


def _company_name_from_path(path: str) -> str:
    if not path or not os.path.isfile(path):
        return ""
    try:
        import ctypes
        from ctypes import wintypes

        size = wintypes.DWORD(0)
        ctypes.windll.version.GetFileVersionInfoSizeW(path, ctypes.byref(size))
        if not size.value:
            return ""
        buf = ctypes.create_string_buffer(size.value)
        if not ctypes.windll.version.GetFileVersionInfoW(path, 0, size.value, buf):
            return ""
        u_len = wintypes.UINT()
        u_buf = ctypes.c_void_p()
        if not ctypes.windll.version.VerQueryValueW(
            buf, "\\StringFileInfo\\040904B0\\CompanyName", ctypes.byref(u_buf), ctypes.byref(u_len)
        ):
            return ""
        return ctypes.wstring_at(u_buf.value) if u_buf.value else ""
    except Exception:
        return ""


def _read_lnk_target(lnk_path: str) -> str:
    try:
        with open(lnk_path, "rb") as fh:
            data = fh.read()
    except OSError:
        return ""
    if len(data) < 0x4C or data[0:4] != b"L\x00\x00\x00":
        return ""
    flags = struct.unpack_from("<I", data, 0x14)[0]
    pos = 0x4C
    if flags & 0x1:
        id_size = struct.unpack_from("<H", data, pos + 0x10)[0] if len(data) > pos + 0x12 else 0
        pos += 0x12 + id_size
    link_info_size = 0
    if flags & 0x2 and len(data) > pos + 4:
        link_info_size = struct.unpack_from("<I", data, pos)[0]
    if flags & 0x2 and link_info_size >= 0x1C and len(data) >= pos + link_info_size:
        offs_local = struct.unpack_from("<I", data, pos + 0x10)[0]
        if offs_local:
            base = pos + offs_local
            if len(data) > base + 2:
                local_off = struct.unpack_from("<H", data, base)[0]
                start = base + local_off
                end = data.find(b"\x00", start)
                if end > start:
                    try:
                        return data[start:end].decode("utf-16-le", errors="ignore")
                    except Exception:
                        pass
    if flags & 0x20:
        name_off = 0x4C
        if flags & 0x1:
            pass
        for off in range(0x4C, min(len(data) - 2, 0x400), 2):
            if data[off : off + 2] == b"\x00\x00" and off > 0x4C:
                try:
                    chunk = data[0x4C:off]
                    if len(chunk) % 2 == 0:
                        text = chunk.decode("utf-16-le", errors="ignore").strip("\x00")
                        if text and len(text) > 1:
                            return text
                except Exception:
                    break
    return ""


def _startup_folder(user: bool) -> str:
    if user:
        return os.path.join(
            os.environ.get("APPDATA", ""),
            r"Microsoft\Windows\Start Menu\Programs\Startup",
        )
    return os.path.join(
        os.environ.get("ProgramData", r"C:\ProgramData"),
        r"Microsoft\Windows\Start Menu\Programs\Startup",
    )


def _wmi_antivirus_display_names() -> List[str]:
    names: List[str] = []
    if os.name != "nt":
        return names
    try:
        proc = subprocess.run(
            [
                "powershell.exe",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                (
                    "Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct "
                    "| Select-Object -ExpandProperty displayName"
                ),
            ],
            capture_output=True,
            text=True,
            timeout=15,
            creationflags=0x08000000,
        )
        if proc.returncode == 0 and proc.stdout:
            for line in proc.stdout.splitlines():
                t = line.strip()
                if t:
                    names.append(t)
    except Exception:
        pass
    return names


_av_products_cache: Tuple[float, List[str]] = (0.0, [])


def _antivirus_tokens() -> List[str]:
    global _av_products_cache
    now = time.time()
    if now - _av_products_cache[0] < 300:
        return _av_products_cache[1]
    tokens = [n.casefold() for n in _wmi_antivirus_display_names()]
    _av_products_cache = (now, tokens)
    return tokens


def _is_refused(name: str, command: str, target_path: str, task_path: str = "") -> Tuple[bool, str]:
    nm = (name or "").casefold()
    cmd = (command or "").casefold()
    tgt = (target_path or "").casefold()
    if any(token in nm or token in cmd for token in _REFUSE_NAMES):
        return True, "Windows Security cannot be changed here."
    if "securityhealth" in tgt or "smartscreen" in tgt:
        return True, "Windows Security cannot be changed here."
    exe = os.path.basename(tgt).casefold()
    if exe in _KNOWN_AV_EXE:
        return True, "Registered antivirus software cannot be changed here."
    for av in _antivirus_tokens():
        if av and (av in nm or av in cmd or av in tgt):
            return True, "Registered antivirus software cannot be changed here."
    system32 = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32").casefold()
    try:
        real = os.path.realpath(target_path) if target_path and os.path.exists(target_path) else target_path
        if real and os.path.normcase(real).startswith(os.path.normcase(system32)):
            return True, "Programs in the Windows system folder cannot be changed here."
    except OSError:
        if tgt and system32 in tgt:
            return True, "Programs in the Windows system folder cannot be changed here."
    norm_task = (task_path or "").replace("/", "\\")
    if norm_task.lower().startswith("\\microsoft\\windows\\"):
        return True, "Built-in Windows sign-in tasks cannot be changed here."
    return False, ""


@dataclass
class _PendingDisable:
    entry_id: str
    approval_hive: int
    approval_path: str
    approval_name: str
    approval_wow: bool
    previous_bytes: bytes
    task_name: str = ""

    task_enable_state: Optional[bool] = None


def _approval_location(
    source: str,
    hkcu: bool,
    wow: bool,
) -> Tuple[int, str, bool]:
    if source == SOURCE_STARTUP_FOLDER:
        return winreg.HKEY_CURRENT_USER, _APPROVED_FOLDER, False
    if wow:
        return (
            winreg.HKEY_LOCAL_MACHINE if not hkcu else winreg.HKEY_CURRENT_USER,
            _APPROVED_RUN32,
            wow and hkcu is False,
        )
    if hkcu:
        return winreg.HKEY_CURRENT_USER, _APPROVED_RUN, False
    return winreg.HKEY_LOCAL_MACHINE, _APPROVED_RUN, False


def _read_enabled(
    source: str,
    name: str,
    hkcu: bool,
    wow: bool,
    task_enabled: Optional[bool] = None,
) -> bool:
    if source == SOURCE_SIGNIN_TASK:
        return bool(task_enabled)
    hive, subkey, reg_wow = _approval_location(source, hkcu, wow)
    raw = _reg_read_binary(hive, subkey, name, wow=reg_wow)
    enabled, _ = decode_startup_approved(raw)
    return enabled


def _query_logon_tasks() -> List[Dict[str, Any]]:
    rows: List[Dict[str, Any]] = []
    if os.name != "nt":
        return rows
    try:
        proc = subprocess.run(
            ["schtasks.exe", "/query", "/xml", "/v"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=120,
            creationflags=0x08000000,
        )
    except (OSError, subprocess.TimeoutExpired):
        return rows
    if proc.returncode != 0 or not proc.stdout:
        return rows
    try:
        root = ET.fromstring(proc.stdout)
    except ET.ParseError:
        return rows
    ns = {"t": "http://schemas.microsoft.com/windows/2004/02/mit/task"}
    for task in root.findall(".//t:Task", ns) if root.tag.endswith("Task") else root.findall(".//{*}Task"):
        pass
    try:
        for reg in root.iter():
            if not reg.tag.endswith("RegistrationInfo"):
                continue
        tasks = []
        if root.tag.endswith("Tasks"):
            tasks = list(root)
        elif proc.stdout.strip().startswith("<"):
            tasks = [root] if root.tag.endswith("Task") else root.findall(".//{*}Task")
        for task_el in tasks:
            uri = ""
            enabled = True
            author = ""
            command = ""
            for child in task_el.iter():
                tag = child.tag.split("}")[-1] if "}" in child.tag else child.tag
                if tag == "URI":
                    uri = (child.text or "").strip()
                elif tag == "Enabled":
                    enabled = (child.text or "true").strip().lower() != "false"
                elif tag == "Author":
                    author = (child.text or "").strip()
                elif tag == "Command":
                    command = (child.text or "").strip()
            triggers_logon = False
            for child in task_el.iter():
                tag = child.tag.split("}")[-1] if "}" in child.tag else child.tag
                if tag == "LogonTrigger":
                    triggers_logon = True
                    break
            if not triggers_logon or not uri:
                continue
            name = uri.lstrip("\\")
            tgt = _parse_command_target(command)
            rows.append(
                {
                    "name": name.split("\\")[-1] or name,
                    "command": command,
                    "target_path": tgt,
                    "task_path": uri,
                    "publisher": author or _company_name_from_path(tgt),
                    "task_enabled": enabled,
                }
            )
    except Exception:
        pass
    if not rows:
        rows = _query_logon_tasks_csv_fallback()
    return rows


def _query_logon_tasks_csv_fallback() -> List[Dict[str, Any]]:
    out: List[Dict[str, Any]] = []
    try:
        proc = subprocess.run(
            ["schtasks.exe", "/query", "/fo", "CSV", "/v"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=120,
            creationflags=0x08000000,
        )
    except (OSError, subprocess.TimeoutExpired):
        return out
    if proc.returncode != 0:
        return out
    lines = proc.stdout.splitlines()
    if len(lines) < 2:
        return out
    for line in lines[1:]:
        if "Logon" not in line and "LOGIN" not in line.upper():
            continue
        parts = [p.strip('"') for p in re.findall(r'"[^"]*"|[^,]+', line)]
        if len(parts) < 9:
            continue
        task_name = parts[0]
        status = parts[3] if len(parts) > 3 else ""
        command = parts[8] if len(parts) > 8 else ""
        enabled = status.lower() != "disabled"
        tgt = _parse_command_target(command)
        out.append(
            {
                "name": task_name.split("\\")[-1],
                "command": command,
                "target_path": tgt,
                "task_path": task_name if task_name.startswith("\\") else f"\\{task_name}",
                "publisher": _company_name_from_path(tgt),
                "task_enabled": enabled,
            }
        )
    return out


def _build_registry_entry(
    name: str,
    command: str,
    source: str,
    hkcu: bool,
    wow: bool,
    readonly: bool,
) -> Dict[str, Any]:
    target = _parse_command_target(command)
    publisher = _company_name_from_path(target)
    enabled = _read_enabled(source, name, hkcu, wow)
    refused, reason = _is_refused(name, command, target)
    entry_id = stable_entry_id(source, name)
    disabled_by_us = entry_id in _journal_disabled_ids
    return {
        "id": entry_id,
        "name": name,
        "publisher": publisher,
        "command": command,
        "target_path": target,
        "source": source,
        "enabled": enabled,
        "refused": refused,
        "refused_reason": reason if refused else "",
        "disabled_by_us": disabled_by_us and not enabled,
        "readonly": readonly,
        "_hkcu": hkcu,
        "_wow": wow,
        "_approval_name": name,
        "_task_path": "",
        "_task_enabled": None,
    }


def _build_folder_entry(lnk_path: str, common: bool) -> Dict[str, Any]:
    name = os.path.basename(lnk_path)
    command = _read_lnk_target(lnk_path) or lnk_path
    target = _parse_command_target(command)
    publisher = _company_name_from_path(target)
    enabled = _read_enabled(SOURCE_STARTUP_FOLDER, name, True, False)
    refused, reason = _is_refused(name, command, target)
    entry_id = stable_entry_id(SOURCE_STARTUP_FOLDER, name)
    return {
        "id": entry_id,
        "name": name,
        "publisher": publisher,
        "command": command,
        "target_path": target,
        "source": SOURCE_STARTUP_FOLDER,
        "enabled": enabled,
        "refused": refused,
        "refused_reason": reason if refused else "",
        "disabled_by_us": entry_id in _journal_disabled_ids and not enabled,
        "readonly": False,
        "_hkcu": True,
        "_wow": False,
        "_approval_name": name,
        "_task_path": "",
        "_task_enabled": None,
        "_lnk_path": lnk_path,
        "_common_folder": common,
    }


def _build_task_entry(row: Dict[str, Any]) -> Dict[str, Any]:
    name = row["name"]
    command = row.get("command", "")
    target = row.get("target_path", "")
    task_path = row.get("task_path", "")
    publisher = row.get("publisher", "") or _company_name_from_path(target)
    enabled = bool(row.get("task_enabled", True))
    refused, reason = _is_refused(name, command, target, task_path=task_path)
    entry_id = stable_entry_id(SOURCE_SIGNIN_TASK, task_path or name)
    return {
        "id": entry_id,
        "name": name,
        "publisher": publisher,
        "command": command,
        "target_path": target,
        "source": SOURCE_SIGNIN_TASK,
        "enabled": enabled,
        "refused": refused,
        "refused_reason": reason if refused else "",
        "disabled_by_us": entry_id in _journal_disabled_ids and not enabled,
        "readonly": False,
        "_hkcu": False,
        "_wow": False,
        "_approval_name": "",
        "_task_path": task_path,
        "_task_enabled": enabled,
    }


def _refresh_journal_disabled_set() -> None:
    global _journal_disabled_ids
    ids: set = set()
    for row in _load_journal():
        if row.get("action") == "disable" and not row.get("undone"):
            ids.add(row.get("entry_id", ""))
        elif row.get("action") == "undo":
            ids.discard(row.get("entry_id", ""))
    _journal_disabled_ids = {i for i in ids if i}


def _public_entry(internal: Dict[str, Any]) -> Dict[str, Any]:
    return {
        k: internal[k]
        for k in (
            "id",
            "name",
            "publisher",
            "command",
            "target_path",
            "source",
            "enabled",
            "refused",
            "refused_reason",
            "disabled_by_us",
        )
    }


def startup_list() -> List[Dict[str, Any]]:
    """Enumerate startup entries from registry, folders, and sign-in tasks."""
    global _entries_cache
    if os.name != "nt" or winreg is None:
        return []
    _refresh_journal_disabled_set()
    entries: List[Dict[str, Any]] = []
    seen: set = set()

    hkcu_run_path = _test_hkcu_run_subkey or _RUN_HKCU
    for name, val in _reg_enum_values(winreg.HKEY_CURRENT_USER, hkcu_run_path).items():
        e = _build_registry_entry(name, val, SOURCE_HKCU_RUN, True, False, False)
        if e["id"] not in seen:
            seen.add(e["id"])
            entries.append(e)

    if _test_hkcu_run_subkey:
        _entries_cache = entries
        return [_public_entry(e) for e in entries]

    for name, val in _reg_enum_values(winreg.HKEY_LOCAL_MACHINE, _RUN_HKLM, wow=True).items():
        e = _build_registry_entry(name, val, SOURCE_HKLM_RUN, False, False, False)
        if e["id"] not in seen:
            seen.add(e["id"])
            entries.append(e)

    for name, val in _reg_enum_values(winreg.HKEY_LOCAL_MACHINE, _RUN_HKLM_WOW, wow=True).items():
        e = _build_registry_entry(name, val, SOURCE_HKLM_RUN, False, True, False)
        if e["id"] not in seen:
            seen.add(e["id"])
            entries.append(e)

    for hive, path, src, hkcu in (
        (winreg.HKEY_CURRENT_USER, _RUNONCE_HKCU, SOURCE_HKCU_RUN, True),
        (winreg.HKEY_LOCAL_MACHINE, _RUNONCE_HKLM, SOURCE_HKLM_RUN, False),
    ):
        for name, val in _reg_enum_values(hive, path, wow=(hive == winreg.HKEY_LOCAL_MACHINE)).items():
            e = _build_registry_entry(name, val, src, hkcu, False, True)
            e["refused"] = True
            e["refused_reason"] = "RunOnce entries are read-only."
            if e["id"] not in seen:
                seen.add(e["id"])
                entries.append(e)

    if not _test_skip_folders:
        for common in (False, True):
            folder = _startup_folder(user=not common)
            if not os.path.isdir(folder):
                continue
            try:
                for ent in os.scandir(folder):
                    if not ent.is_file():
                        continue
                    if ent.name.casefold().endswith(".lnk"):
                        e = _build_folder_entry(ent.path, common)
                        if e["id"] not in seen:
                            seen.add(e["id"])
                            entries.append(e)
            except OSError:
                pass

    if not _test_skip_tasks:
        for row in _query_logon_tasks():
            e = _build_task_entry(row)
            if e["id"] not in seen:
                seen.add(e["id"])
                entries.append(e)

    _entries_cache = entries
    return [_public_entry(e) for e in entries]


def _find_entry(entry_id: str) -> Optional[Dict[str, Any]]:
    if not _entries_cache:
        startup_list()
    for e in _entries_cache:
        if e["id"] == entry_id:
            return e
    return None


def _set_task_enabled(task_path: str, enabled: bool) -> Tuple[bool, str]:
    if not task_path:
        return False, "Task path missing."
    flag = "/enable" if enabled else "/disable"
    try:
        proc = subprocess.run(
            ["schtasks.exe", "/change", "/tn", task_path.lstrip("\\"), flag],
            capture_output=True,
            text=True,
            timeout=30,
            creationflags=0x08000000,
        )
        if proc.returncode == 0:
            return True, "ok"
        detail = (proc.stderr or proc.stdout or "").strip()
        return False, detail or "Could not change the sign-in task."
    except (OSError, subprocess.TimeoutExpired) as exc:
        return False, str(exc)


def startup_set_enabled(entry_id: str, enabled: bool) -> Dict[str, Any]:
    entry = _find_entry(entry_id)
    if not entry:
        return {"success": False, "message": "Startup entry not found."}
    if entry.get("readonly"):
        return {"success": False, "message": entry.get("refused_reason") or "This entry is read-only."}
    if entry.get("refused"):
        return {"success": False, "message": entry.get("refused_reason") or "This entry cannot be changed."}

    source = entry["source"]
    if source == SOURCE_SIGNIN_TASK:
        task_path = entry.get("_task_path") or entry["name"]
        prev = bool(entry.get("_task_enabled"))
        ok, msg = _set_task_enabled(task_path, enabled)
        if not ok:
            return {"success": False, "message": msg}
        _append_journal(
            {
                "action": "disable" if not enabled else "enable",
                "entry_id": entry_id,
                "name": entry["name"],
                "source": source,
                "previous_enabled": prev,
                "task_path": task_path,
            }
        )
        entry["enabled"] = enabled
        entry["_task_enabled"] = enabled
        entry["disabled_by_us"] = not enabled
        return {"success": True, "entry": _public_entry(entry)}

    name = entry["_approval_name"]
    hive, subkey, reg_wow = _approval_location(source, entry["_hkcu"], entry["_wow"])
    raw = _reg_read_binary(hive, subkey, name, wow=reg_wow)
    previous = raw
    new_bytes = encode_startup_approved(enabled, previous)
    if not _reg_write_binary(hive, subkey, name, new_bytes, wow=reg_wow):
        return {"success": False, "message": "Could not write startup approval state."}
    prev_enabled, _ = decode_startup_approved(previous)
    _append_journal(
        {
            "action": "disable" if not enabled else "enable",
            "entry_id": entry_id,
            "name": entry["name"],
            "source": source,
            "previous_enabled": prev_enabled,
            "previous_bytes": (previous or b"").hex(),
            "approval_hive": "HKCU" if hive == winreg.HKEY_CURRENT_USER else "HKLM",
            "approval_path": subkey,
            "approval_name": name,
            "approval_wow": reg_wow,
        }
    )
    entry["enabled"] = enabled
    entry["disabled_by_us"] = not enabled
    _refresh_journal_disabled_set()
    return {"success": True, "entry": _public_entry(entry)}


def startup_undo_last() -> Dict[str, Any]:
    rows = _load_journal()
    for row in reversed(rows):
        if row.get("undone"):
            continue
        entry_id = row.get("entry_id", "")
        prev = row.get("previous_enabled")
        if prev is None:
            continue
        result = startup_set_enabled(entry_id, bool(prev))
        row["undone"] = True
        _save_journal(rows)
        return {"success": result.get("success", False), "message": result.get("message", ""), "entry_id": entry_id}
    return {"success": False, "message": "Nothing to undo."}


def startup_history() -> List[Dict[str, Any]]:
    return _load_journal()
