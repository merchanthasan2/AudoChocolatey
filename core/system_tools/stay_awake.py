"""34.17 — Keep Windows awake temporarily (SetThreadExecutionState)."""
from __future__ import annotations

import ctypes
from typing import Any, Dict

_ES_CONTINUOUS = 0x80000000
_ES_SYSTEM_REQUIRED = 0x00000001
_ES_DISPLAY_REQUIRED = 0x00000002

_active = False


def keep_awake_start(display: bool = True) -> Dict[str, Any]:
    global _active
    if __import__("os").name != "nt":
        return {"ok": False, "error": "Windows only."}
    flags = _ES_CONTINUOUS | _ES_SYSTEM_REQUIRED
    if display:
        flags |= _ES_DISPLAY_REQUIRED
    try:
        ctypes.windll.kernel32.SetThreadExecutionState(flags)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    _active = True
    return {"ok": True, "message": "Keep-awake enabled.", "display": bool(display)}


def keep_awake_cancel() -> Dict[str, Any]:
    global _active
    try:
        ctypes.windll.kernel32.SetThreadExecutionState(_ES_CONTINUOUS)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    _active = False
    return {"ok": True, "message": "Keep-awake cancelled."}


def keep_awake_status() -> Dict[str, Any]:
    return {"ok": True, "active": _active}