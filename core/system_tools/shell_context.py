"""33.1 — Explorer context-menu handlers (third-party, reversible)."""
from __future__ import annotations

import json
import os
import re
from typing import Any, Dict, List

from core.system_tools.network_tools import _ps

_SAFE_HANDLER = re.compile(r"^[A-Za-z0-9._ -]{1,120}$")

_MS_CLSID_MARKERS = (
    "50717930-aBB4-4e16-8dae-",
    "00021401-0000-0000-c000-",
    "f86fa3ab-7353-4420-",
)

_STORE = os.path.join(os.environ.get("LOCALAPPDATA", ""), "QuantumUpdater", "context_menu_backups.json")

_LIST_SCRIPT = (
    "$ErrorActionPreference='SilentlyContinue';"
    "$roots = @("
    "'Registry::HKEY_CLASSES_ROOT\\Directory\\shellex\\ContextMenuHandlers',"
    "'Registry::HKEY_CLASSES_ROOT\\Directory\\Background\\shellex\\ContextMenuHandlers',"
    "'Registry::HKEY_CLASSES_ROOT\\*\\shellex\\ContextMenuHandlers'"
    ");"
    "$out = @();"
    "foreach ($root in $roots) {"
    "  Get-ChildItem -Path $root -ErrorAction SilentlyContinue | ForEach-Object {"
    "    $def = $_.GetValue('');"
    "    $out += [pscustomobject]@{"
    "      id = ($root + '\\\\' + $_.PSChildName);"
    "      name = $_.PSChildName;"
    "      clsid = [string]$def;"
    "      scope = $root"
    "    }"
    "  }"
    "};"
    "if (-not $out) { '[]' } else { $out | ConvertTo-Json -Compress }"
)


def _load_store() -> List[Dict[str, str]]:
    if not os.path.isfile(_STORE):
        return []
    try:
        with open(_STORE, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data if isinstance(data, list) else []
    except (OSError, json.JSONDecodeError):
        return []


def _save_store(rows: List[Dict[str, str]]) -> None:
    os.makedirs(os.path.dirname(_STORE), exist_ok=True)
    with open(_STORE, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(rows, handle, indent=2)


def _is_system(clsid: str, name: str) -> bool:
    low = (clsid or "").lower()
    if any(marker in low for marker in _MS_CLSID_MARKERS):
        return True
    if name in ("New", "Sharing", "Library Location", "Offline Files", "WorkFolders"):
        return True
    return False


def list_context_handlers() -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only.", "handlers": []}
    try:
        proc = _ps(_LIST_SCRIPT, timeout=90)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "handlers": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    disabled = {item.get("id") for item in _load_store()}
    handlers: List[Dict[str, Any]] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        hid = str(row.get("id") or "")
        name = str(row.get("name") or "")
        clsid = str(row.get("clsid") or "")
        if not hid or not _SAFE_HANDLER.match(name):
            continue
        third_party = not _is_system(clsid, name)
        handlers.append(
            {
                "id": hid,
                "name": name,
                "clsid": clsid,
                "scope": str(row.get("scope") or ""),
                "third_party": third_party,
                "disabled": hid in disabled,
            }
        )
    return {"ok": True, "handlers": handlers, "count": len(handlers)}


def disable_context_handler(handler_id: str, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    hid = str(handler_id or "").strip()
    if not hid.startswith("Registry::") or ".." in hid:
        return {"ok": False, "error": "That handler id is not allowed."}
    listed = list_context_handlers()
    match = next((h for h in listed.get("handlers") or [] if h.get("id") == hid), None)
    if not match:
        return {"ok": False, "error": "Handler not found in the current list."}
    if not match.get("third_party"):
        return {"ok": False, "error": "System handlers cannot be disabled."}
    escaped = hid.replace("'", "''")
    script = f"Remove-Item -LiteralPath '{escaped}' -Force -ErrorAction Stop; 'OK'"
    try:
        proc = _ps(script, timeout=60)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    if proc.returncode != 0:
        return {"ok": False, "error": (proc.stderr or proc.stdout or "Disable failed.").strip()[:500]}
    backup = _load_store()
    backup.append(
        {
            "id": hid,
            "name": str(match.get("name") or ""),
            "clsid": str(match.get("clsid") or ""),
        }
    )
    _save_store(backup)
    return {"ok": True, "message": f"Disabled handler {match.get('name')}."}


def restore_context_handlers(confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    backup = _load_store()
    if not backup:
        return {"ok": False, "error": "No saved handler backups."}
    restored = 0
    for row in backup:
        hid = str(row.get("id") or "")
        clsid = str(row.get("clsid") or "")
        if not hid.startswith("Registry::"):
            continue
        parts = hid.replace("Registry::", "").rsplit("\\", 1)
        if len(parts) != 2:
            continue
        reg_path, child_name = parts
        esc_path = reg_path.replace("'", "''")
        esc_name = child_name.replace("'", "''")
        esc_clsid = clsid.replace("'", "''")
        script = (
            f"New-Item -Path 'Registry::{esc_path}' -Force | Out-Null;"
            f"New-ItemProperty -Path 'Registry::{esc_path}\\{esc_name}' -Name '(default)' "
            f"-Value '{esc_clsid}' -PropertyType String -Force | Out-Null;"
            "'OK'"
        )
        try:
            proc = _ps(script, timeout=60)
            if proc.returncode == 0:
                restored += 1
        except Exception:
            pass
    _save_store([])
    return {"ok": True, "message": f"Restored {restored} handler(s).", "restored": restored}