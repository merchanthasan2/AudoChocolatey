"""34.10 — Open Windows privacy settings (review in Windows)."""
from __future__ import annotations

import os
import subprocess
from typing import Any, Dict


def open_privacy_settings() -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    flags = 0x08000000
    try:
        proc = subprocess.run(
            ["cmd", "/c", "start", "", "ms-settings:privacy"],
            capture_output=True,
            text=True,
            timeout=15,
            creationflags=flags,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Could not open privacy settings."}
    return {
        "ok": proc.returncode == 0,
        "message": "Opened Windows privacy settings.",
    }