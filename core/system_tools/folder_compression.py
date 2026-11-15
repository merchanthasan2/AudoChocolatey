"""34.2 — NTFS transparent folder compression with measured savings."""
from __future__ import annotations

import os
import subprocess
from typing import Any, Dict

from core.system_tools.file_tools import _under_user_or_removable


def _folder_size(path: str) -> int:
    total = 0
    for root, _dirs, files in os.walk(path):
        for name in files:
            try:
                total += os.path.getsize(os.path.join(root, name))
            except OSError:
                continue
    return total


def compress_folder(folder: str, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required. Compression changes file storage.", "needs_confirm": True}
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    path = os.path.abspath(folder)
    if not os.path.isdir(path):
        return {"ok": False, "error": "Folder not found."}
    if not _under_user_or_removable(path):
        return {"ok": False, "error": "Folder must be under your profile or a normal drive path."}
    before = _folder_size(path)
    flags = 0x08000000
    try:
        proc = subprocess.run(
            ["compact", "/c", "/s", f"{path}", "/i", "/q"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=600,
            creationflags=flags,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Compression timed out."}
    after = _folder_size(path)
    saved = max(0, before - after)
    return {
        "ok": proc.returncode == 0,
        "message": f"Compression finished. Approximate savings: {saved} bytes.",
        "bytes_before": before,
        "bytes_after": after,
        "bytes_saved": saved,
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1500],
    }