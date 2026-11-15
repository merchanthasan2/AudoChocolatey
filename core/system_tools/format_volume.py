"""34.8 — Quick-format removable volumes only (never system drive)."""
from __future__ import annotations

import os
import subprocess
from typing import Any, Dict, List

from core.system_tools.drives import enumerate_cleanup_volumes


def list_removable_volumes() -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only.", "volumes": []}
    volumes = [v for v in enumerate_cleanup_volumes(include_external=True) if v.get("drive_type") == "removable"]
    system = os.environ.get("SystemDrive", "C:").rstrip("\\").upper()
    for vol in volumes:
        vol["system_drive"] = vol.get("letter", "").upper() == system
    return {"ok": True, "volumes": volumes, "count": len(volumes)}


def format_removable_volume(letter: str, filesystem: str = "NTFS", confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required. All data on the volume will be erased.", "needs_confirm": True}
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    let = str(letter or "").strip().upper().rstrip(":")
    if len(let) != 1 or not let.isalpha():
        return {"ok": False, "error": "That drive letter is not allowed."}
    system = os.environ.get("SystemDrive", "C:").rstrip("\\").upper()
    if f"{let}:" == system:
        return {"ok": False, "error": "The system drive cannot be formatted."}
    listed = list_removable_volumes()
    allowed: List[str] = [v.get("letter", "") for v in listed.get("volumes") or []]
    if f"{let}:" not in allowed:
        return {"ok": False, "error": "That volume is not a removable drive on this PC."}
    fs = str(filesystem or "NTFS").upper()
    if fs not in ("NTFS", "FAT32", "exFAT"):
        return {"ok": False, "error": "Filesystem must be NTFS, FAT32, or exFAT."}
    flags = 0x08000000
    try:
        proc = subprocess.run(
            ["format", f"{let}:", "/FS:" + fs, "/Q", "/Y"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=300,
            creationflags=flags,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Format timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": f"Volume {let}: formatted as {fs}." if proc.returncode == 0 else "Format failed.",
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1500],
    }