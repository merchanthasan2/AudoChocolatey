"""33.3 — Sleep, wake, and power-request diagnostics."""
from __future__ import annotations

import os
from typing import Any, Dict

from core.system_tools.network_tools import _run


def sleep_wake_diagnostics() -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    sections: Dict[str, str] = {}
    for label, args in (
        ("last_wake", ["powercfg", "/lastwake"]),
        ("wake_armed", ["powercfg", "/devicequery", "wake_armed"]),
        ("requests", ["powercfg", "/requests"]),
    ):
        try:
            proc = _run(list(args), timeout=60)
            sections[label] = ((proc.stdout or "") + (proc.stderr or "")).strip()[:3000]
        except Exception as e:
            sections[label] = str(e)
    combined = "\n\n".join(f"=== {k} ===\n{v}" for k, v in sections.items())
    return {"ok": True, "sections": sections, "detail": combined[:9000]}