"""34.3 — Mount and eject ISO/VHD (read-only default for ISO)."""
from __future__ import annotations

import json
import os
import re
from typing import Any, Dict, List

from core.system_tools.file_tools import _under_user_or_removable
from core.system_tools.network_tools import _ps

_IMAGE_RE = re.compile(r"\.(iso|vhd|vhdx)$", re.I)


def _mounted_images() -> List[Dict[str, str]]:
    script = (
        "$ErrorActionPreference='SilentlyContinue';"
        "$rows = Get-DiskImage | Select-Object ImagePath, Attached, DevicePath, StorageType;"
        " if (-not $rows) { '[]' } else { $rows | ConvertTo-Json -Compress }"
    )
    try:
        proc = _ps(script, timeout=60)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception:
        return []
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    return [
        {
            "path": str(r.get("ImagePath") or ""),
            "attached": str(r.get("Attached") or ""),
            "device": str(r.get("DevicePath") or ""),
        }
        for r in rows
        if isinstance(r, dict)
    ]


def list_mounted_images() -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only.", "images": []}
    images = _mounted_images()
    return {"ok": True, "images": images, "count": len(images)}


def mount_disk_image(path: str, read_only: bool = True, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    ap = os.path.abspath(path)
    if not os.path.isfile(ap) or not _IMAGE_RE.search(ap):
        return {"ok": False, "error": "Choose an existing .iso, .vhd, or .vhdx file."}
    if not _under_user_or_removable(ap):
        return {"ok": False, "error": "Image path must be under your profile or a normal drive path."}
    esc = ap.replace("'", "''")
    ro = "-ReadOnly" if read_only or ap.lower().endswith(".iso") else ""
    script = f"Mount-DiskImage -ImagePath '{esc}' {ro} -PassThru | Out-Null; 'OK'"
    try:
        proc = _ps(script, timeout=120)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    ok = proc.returncode == 0
    return {
        "ok": ok,
        "message": "Image mounted." if ok else "Mount failed.",
        "read_only": bool(read_only or ap.lower().endswith(".iso")),
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }


def eject_disk_image(path: str = "", confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    target = str(path or "").strip()
    if target:
        ap = os.path.abspath(target)
        if not _IMAGE_RE.search(ap):
            return {"ok": False, "error": "Image path not allowed."}
        esc = ap.replace("'", "''")
        script = f"Dismount-DiskImage -ImagePath '{esc}' -ErrorAction Stop; 'OK'"
    else:
        script = (
            "$ErrorActionPreference='Stop';"
            "Get-DiskImage | Where-Object { $_.Attached } | ForEach-Object {"
            " Dismount-DiskImage -ImagePath $_.ImagePath -ErrorAction SilentlyContinue"
            "}; 'OK'"
        )
    try:
        proc = _ps(script, timeout=120)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    return {
        "ok": proc.returncode == 0,
        "message": "Image ejected." if proc.returncode == 0 else "Eject failed.",
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }