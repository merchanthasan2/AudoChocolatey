"""Broken or orphaned ARP uninstall entries."""

from __future__ import annotations

import os
import shlex
import winreg
from typing import Any, Dict, List, Optional, Tuple

from core.arp_inventory import ArpEntry, list_arp_entries

# Reuse leftovers registry backup/delete helpers.
from core.system_tools.leftovers import (
    _delete_registry_key,
    _export_registry_key,
    _hive_from_name,
    _write_registry_backup,
)


def _resolve_entry(entry_id: str) -> Optional[ArpEntry]:
    for entry in list_arp_entries():
        if entry.id == entry_id:
            return entry
    return None


def _entry_registry_ref(entry: ArpEntry) -> Tuple[Any, str, bool]:
    if entry.scope == "user":
        hive = winreg.HKEY_CURRENT_USER
        wow = False
    else:
        hive = winreg.HKEY_LOCAL_MACHINE
        wow = "wow6432node" in entry.registry_path.casefold()
    return hive, entry.registry_path, wow


def _hive_name_for_entry(entry: ArpEntry) -> str:
    return "HKCU" if entry.scope == "user" else "HKLM"


def _primary_uninstall_string(entry: ArpEntry) -> str:
    if entry.uninstall_string.strip():
        return entry.uninstall_string.strip()
    return entry.quiet_uninstall_string.strip()


def _uninstall_target_missing(uninstall_string: str) -> bool:
    if not uninstall_string.strip():
        return True
    lowered = uninstall_string.lower()
    if "msiexec" in lowered:
        system_root = os.environ.get("SystemRoot", r"C:\Windows")
        exe = os.path.join(system_root, "System32", "msiexec.exe")
        return not os.path.isfile(exe)
    try:
        tokens = shlex.split(uninstall_string, posix=False)
    except ValueError:
        return True
    if not tokens:
        return True
    exe_token = tokens[0].strip('"')
    if not os.path.isabs(exe_token):
        return True
    try:
        resolved = os.path.realpath(exe_token)
    except OSError:
        return True
    return not os.path.isfile(resolved)


def _is_broken_entry(entry: ArpEntry) -> bool:
    primary = _primary_uninstall_string(entry)
    if not primary:
        return True
    return _uninstall_target_missing(primary)


def list_broken_uninstallers() -> Dict[str, Any]:
    if os.name != "nt":
        return {"items": [], "count": 0}
    items: List[Dict[str, Any]] = []
    for entry in list_arp_entries():
        if not _is_broken_entry(entry):
            continue
        primary = _primary_uninstall_string(entry)
        items.append(
            {
                "entry_id": entry.id,
                "name": entry.name,
                "publisher": entry.publisher,
                "version": entry.version,
                "uninstall_string": primary,
                "install_location": entry.install_location,
                "scope": entry.scope,
                "reason": "Missing uninstall command." if not primary else "Uninstall target not found.",
            }
        )
    items.sort(key=lambda row: (row.get("name") or "").casefold())
    return {"items": items, "count": len(items)}


def remove_orphan_entry(entry_id: str, *, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"success": False, "message": "Confirmation is required.", "needs_confirm": True}
    if os.name != "nt":
        return {"success": False, "message": "Not supported on this platform."}
    entry = _resolve_entry(entry_id)
    if not entry:
        return {"success": False, "message": "Add/Remove Programs entry was not found."}
    if not _is_broken_entry(entry):
        return {"success": False, "message": "This entry still has a working uninstall command."}

    hive, subkey, wow = _entry_registry_ref(entry)
    hive_name = _hive_name_for_entry(entry)
    try:
        values = _export_registry_key(hive, subkey, wow)
    except OSError:
        return {"success": False, "message": "Could not read the uninstall registry entry."}
    backup_id = _write_registry_backup(entry_id, hive_name, subkey, wow, values)
    if not _delete_registry_key(hive, subkey, wow):
        return {"success": False, "message": "Could not delete the uninstall registry entry.", "backup_id": backup_id}
    return {
        "success": True,
        "entry_id": entry_id,
        "backup_id": backup_id,
        "suggest_leftovers_scan": True,
        "message": "Uninstall registry entry removed. Run a leftover scan next.",
    }