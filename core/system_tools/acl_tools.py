"""34.11 — Read-only ACL inspection via icacls."""
from __future__ import annotations

import os
import subprocess
from typing import Any, Dict

from core.system_tools.file_tools import _under_user_or_removable


def inspect_permissions(path: str) -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    ap = os.path.abspath(path)
    if not os.path.exists(ap):
        return {"ok": False, "error": "Path not found."}
    if not _under_user_or_removable(ap):
        return {"ok": False, "error": "Path must be under your profile or a normal drive path."}
    flags = 0x08000000
    try:
        proc = subprocess.run(
            ["icacls", ap],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=60,
            creationflags=flags,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Permission read timed out."}
    text = ((proc.stdout or "") + (proc.stderr or "")).strip()
    return {"ok": proc.returncode == 0, "detail": text[:8000], "path": ap}