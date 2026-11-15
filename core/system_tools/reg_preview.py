"""34.16 — Preview .reg files; import off unless explicitly confirmed."""
from __future__ import annotations

import os
import subprocess
from typing import Any, Dict, List

from core.system_tools.file_tools import _under_user_or_removable


def preview_reg_file(path: str) -> Dict[str, Any]:
    ap = os.path.abspath(path)
    if not ap.lower().endswith(".reg") or not os.path.isfile(ap):
        return {"ok": False, "error": ".reg file not found."}
    if not _under_user_or_removable(ap):
        return {"ok": False, "error": "Path must be under your profile or a normal drive path."}
    lines: List[str] = []
    try:
        with open(ap, "r", encoding="utf-16-le", errors="replace") as handle:
            for i, ln in enumerate(handle):
                if i >= 400:
                    break
                lines.append(ln.rstrip("\r\n")[:500])
    except OSError:
        try:
            with open(ap, "r", encoding="utf-8", errors="replace") as handle:
                for i, ln in enumerate(handle):
                    if i >= 400:
                        break
                    lines.append(ln.rstrip("\n")[:500])
        except OSError as e:
            return {"ok": False, "error": str(e)}
    keys = [ln for ln in lines if ln.startswith("[")]
    return {
        "ok": True,
        "path": ap,
        "line_count": len(lines),
        "keys": keys[:40],
        "preview": "\n".join(lines[:80]),
        "import_allowed": False,
        "message": "Preview only. Import is off by default.",
    }


def import_reg_file(path: str, confirmed: bool = False, allow_import: bool = False) -> Dict[str, Any]:
    if not allow_import:
        return {"ok": False, "error": "Registry import is disabled by default.", "import_blocked": True}
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    preview = preview_reg_file(path)
    if not preview.get("ok"):
        return preview
    ap = preview["path"]
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    flags = 0x08000000
    try:
        proc = subprocess.run(
            ["reg", "import", ap],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=120,
            creationflags=flags,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Registry import timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": "Registry file imported." if proc.returncode == 0 else "Import failed.",
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }