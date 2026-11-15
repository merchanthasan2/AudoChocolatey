"""Windows Add/Remove Programs (ARP) registry inventory."""

from __future__ import annotations

import logging
import os
import re
import shlex
import winreg
from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Tuple

logger = logging.getLogger(__name__)

_KB_ONLY_RE = re.compile(r"^KB\d+$", re.IGNORECASE)
_GUID_RE = re.compile(
    r"/([xXiI])(\{[0-9A-Fa-f-]{36}\}|[0-9A-Fa-f-]{32})"
)
_QUIET_FLAGS = {"/qn", "/q", "/quiet", "/s", "/silent"}
_BAD_ARG = re.compile(r"https?://|[|&\n\r<>`]|\$\(")

_BLOCKED_EXE_NAMES = frozenset(
    {
        "cmd",
        "cmd.exe",
        "powershell",
        "powershell.exe",
        "pwsh",
        "pwsh.exe",
        "wscript",
        "wscript.exe",
        "cscript",
        "cscript.exe",
        "mshta",
        "mshta.exe",
        "reg",
        "reg.exe",
        "regedit",
        "regedit.exe",
        "rundll32",
        "rundll32.exe",
    }
)


@dataclass
class ArpEntry:
    id: str
    name: str
    version: str
    publisher: str
    uninstall_string: str
    quiet_uninstall_string: str
    install_location: str
    scope: str  # machine | user
    registry_path: str


def _is_windows_update_display(name: str) -> bool:
    n = (name or "").strip()
    if not n:
        return True
    if n.lower().startswith("update for"):
        return True
    if _KB_ONLY_RE.match(n):
        return True
    return False


def _key_access(hkey) -> int:
    access = winreg.KEY_READ
    # A 32-bit process otherwise sees only the 32-bit uninstall list.
    if hkey == winreg.HKEY_LOCAL_MACHINE:
        access |= winreg.KEY_WOW64_64KEY
    return access


def _read_value(hkey, subkey: str, value_name: str) -> str:
    try:
        with winreg.OpenKey(hkey, subkey, 0, _key_access(hkey)) as key:
            val, _ = winreg.QueryValueEx(key, value_name)
            return str(val) if val is not None else ""
    except OSError:
        return ""


def _enum_subkeys(hkey, path: str) -> List[str]:
    names: List[str] = []
    try:
        with winreg.OpenKey(hkey, path, 0, _key_access(hkey)) as key:
            i = 0
            while True:
                try:
                    names.append(winreg.EnumKey(key, i))
                    i += 1
                except OSError:
                    break
    except OSError:
        pass
    return names


def _scan_root(hkey, path: str, scope: str) -> List[ArpEntry]:
    entries: List[ArpEntry] = []
    for subkey in _enum_subkeys(hkey, path):
        full_path = f"{path}\\{subkey}"
        sys_comp = _read_value(hkey, full_path, "SystemComponent")
        if sys_comp.strip() in ("1", "0x1"):
            continue

        display_name = _read_value(hkey, full_path, "DisplayName").strip()
        if not display_name:
            continue
        if _is_windows_update_display(display_name):
            continue

        uninstall_string = _read_value(hkey, full_path, "UninstallString")
        quiet_uninstall_string = _read_value(hkey, full_path, "QuietUninstallString")
        publisher = _read_value(hkey, full_path, "Publisher")
        version = _read_value(hkey, full_path, "DisplayVersion")
        install_location = _read_value(hkey, full_path, "InstallLocation")

        entry_id = f"arp:{scope}:{subkey}"

        if uninstall_string and _path_under_user_profile(uninstall_string):
            logger.info("ARP entry listed: %s", display_name)
        elif uninstall_string:
            logger.debug("ARP uninstall string present for: %s", display_name)

        entries.append(
            ArpEntry(
                id=entry_id,
                name=display_name,
                version=version,
                publisher=publisher,
                uninstall_string=uninstall_string,
                quiet_uninstall_string=quiet_uninstall_string,
                install_location=install_location,
                scope=scope,
                registry_path=full_path,
            )
        )
    return entries


def _path_under_user_profile(text: str) -> bool:
    profile = os.environ.get("USERPROFILE", "")
    if not profile:
        return False
    try:
        norm = os.path.normcase(os.path.normpath(text))
        prof = os.path.normcase(os.path.normpath(profile))
        return prof in norm or norm.startswith(prof + os.sep)
    except Exception:
        return False


def list_arp_entries() -> List[ArpEntry]:
    if os.name != "nt":
        return []

    roots: List[Tuple[Any, str, str]] = [
      (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "machine"),
      (
          winreg.HKEY_LOCAL_MACHINE,
          r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
          "machine",
      ),
      (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "user"),
  ]

    all_entries: List[ArpEntry] = []
    seen_ids: set = set()
    for hkey, path, scope in roots:
        for entry in _scan_root(hkey, path, scope):
            if entry.id in seen_ids:
                continue
            seen_ids.add(entry.id)
            all_entries.append(entry)
    return all_entries


def find_arp_entry(arp_id: str) -> Optional[ArpEntry]:
    for entry in list_arp_entries():
        if entry.id == arp_id:
            return entry
    return None


def _normalize_guid(raw: str) -> str:
    if not raw.startswith("{"):
        raw = "{" + raw + "}"
    return raw.upper()


def _msi_plan(uninstall_string: str) -> Optional[Dict[str, Any]]:
    lowered = uninstall_string.strip()
    if "msiexec" not in lowered.lower():
        return None
    if "%" in uninstall_string or ".." in uninstall_string:
        return None

    m = _GUID_RE.search(uninstall_string)
    if not m:
        return None

    switch = f"/{m.group(1).upper()}"
    guid = _normalize_guid(m.group(2))

    tokens = shlex.split(uninstall_string, posix=False)
    had_quiet = any(t.lower() in _QUIET_FLAGS for t in tokens)

    system_root = os.environ.get("SystemRoot", r"C:\Windows")
    exe = os.path.join(system_root, "System32", "msiexec.exe")
    if not os.path.isfile(exe):
        return None

    args = [switch + guid]
    if had_quiet and not any(a.lower() == "/qn" for a in args):
        args.append("/qn")

    return {"exe": exe, "args": args, "cwd": None}


def _allowed_program_roots() -> List[str]:
    roots = []
    for env_name in ("ProgramFiles", "ProgramFiles(x86)"):
        val = os.environ.get(env_name, "")
        if val:
            roots.append(os.path.normcase(os.path.abspath(val)))
    local_programs = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs")
    if local_programs:
        roots.append(os.path.normcase(os.path.abspath(local_programs)))
    return roots


def _under_allowed_root(resolved: str) -> bool:
    res = os.path.normcase(os.path.abspath(resolved))
    for root in _allowed_program_roots():
        if res == root or res.startswith(root + os.sep):
            return True
    return False


def _exe_plan(uninstall_string: str) -> Optional[Dict[str, Any]]:
    if "%" in uninstall_string or ".." in uninstall_string:
        return None
    if "msiexec" in uninstall_string.lower():
        return None

    try:
        tokens = shlex.split(uninstall_string, posix=False)
    except ValueError:
        return None
    if not tokens:
        return None

    exe_token = tokens[0]
    if exe_token.startswith('"') and exe_token.endswith('"'):
        exe_token = exe_token[1:-1]

    if " " in exe_token and not (uninstall_string.strip().startswith('"') and '"' in uninstall_string):
        return None

    if not os.path.isabs(exe_token):
        return None

    resolved = os.path.realpath(exe_token)
    exe_name = os.path.basename(resolved).lower()
    if exe_name in _BLOCKED_EXE_NAMES:
        return None
    if not os.path.isfile(resolved):
        return None
    if not _under_allowed_root(resolved):
        return None

    args = tokens[1:]
    if any(_BAD_ARG.search(arg) for arg in args):
        return None
    return {"exe": resolved, "args": args, "cwd": None}


def plan_uninstall(entry: ArpEntry, prefer_quiet: bool = True) -> Optional[Dict[str, Any]]:
    """Return a safe argv-based uninstall plan, or None if unsafe."""
    primary = entry.quiet_uninstall_string if prefer_quiet and entry.quiet_uninstall_string else entry.uninstall_string
    if not primary or not primary.strip():
        alternate = entry.uninstall_string if primary != entry.uninstall_string else entry.quiet_uninstall_string
        primary = alternate or ""

    if not primary.strip():
        return None

    msi = _msi_plan(primary)
    if msi:
        return msi
    return _exe_plan(primary)
