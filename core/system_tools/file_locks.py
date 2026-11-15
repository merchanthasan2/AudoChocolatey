"""34.7 — Detect whether a file is locked (best-effort)."""
from __future__ import annotations

import json
import os
from typing import Any, Dict, List

from core.system_tools.file_tools import _under_user_or_removable
from core.system_tools.network_tools import _ps


def file_lock_probe(path: str) -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    ap = os.path.abspath(path)
    if not os.path.isfile(ap):
        return {"ok": False, "error": "File not found."}
    if not _under_user_or_removable(ap):
        return {"ok": False, "error": "Path must be under your profile or a normal drive path."}
    locked = False
    try:
        with open(ap, "r+b") as handle:
            pass
    except PermissionError:
        locked = True
    except OSError:
        locked = True
    holders: List[Dict[str, str]] = []
    if locked:
        base = os.path.basename(ap).replace("'", "''")
        script = (
            "$ErrorActionPreference='SilentlyContinue';"
            f"$name = '{base}';"
            "Get-Process | Where-Object { $_.Path } |"
            " Select-Object -First 30 Id, ProcessName, Path |"
            " ForEach-Object {"
            "  if ($_.Path -and (Test-Path $_.Path) -and ($_.Path -like ('*' + $name))) {"
            "    $_"
            "  }"
            "} | ConvertTo-Json -Compress"
        )
        try:
            proc = _ps(script, timeout=45)
            data = json.loads((proc.stdout or "").strip() or "[]")
            rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
            for row in rows:
                if isinstance(row, dict):
                    holders.append(
                        {
                            "pid": str(row.get("Id") or ""),
                            "name": str(row.get("ProcessName") or ""),
                            "path": str(row.get("Path") or ""),
                        }
                    )
        except Exception:
            pass
    return {
        "ok": True,
        "locked": locked,
        "holders": holders,
        "message": "File is in use." if locked else "File is not locked.",
        "note": "Holder list is best-effort when exclusive open fails.",
    }