"""33.2 — Blue-screen dump triage (read-only; stop code from events)."""
from __future__ import annotations

import glob
import json
import os
from typing import Any, Dict, List

from core.system_tools.network_tools import _ps


def bsod_triage() -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    mini_dir = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "Minidump")
    dumps: List[str] = []
    try:
        dumps = sorted(glob.glob(os.path.join(mini_dir, "*.dmp")))[-10:]
    except OSError:
        dumps = []
    dump_names = [os.path.basename(p) for p in dumps]
    script = (
        "$ErrorActionPreference='SilentlyContinue';"
        "$e = Get-WinEvent -FilterHashtable @{LogName='System'; Id=1001} -MaxEvents 8 |"
        " ForEach-Object {"
        "  [pscustomobject]@{"
        "    time = $_.TimeCreated.ToString('o');"
        "    message = $_.Message"
        "  }"
        "};"
        "if (-not $e) { '[]' } else { $e | ConvertTo-Json -Compress }"
    )
    events: List[Dict[str, str]] = []
    stop_hint = ""
    try:
        proc = _ps(script, timeout=45)
        data = json.loads((proc.stdout or "").strip() or "[]")
        rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
        for row in rows:
            if not isinstance(row, dict):
                continue
            msg = str(row.get("message") or "")
            events.append({"time": str(row.get("time") or ""), "message": msg[:800]})
            if not stop_hint and "0x" in msg:
                for token in msg.split():
                    if token.startswith("0x") and len(token) <= 12:
                        stop_hint = token
                        break
    except Exception:
        pass
    lead = stop_hint or (dump_names[-1] if dump_names else "No recent bugcheck event found.")
    return {
        "ok": True,
        "stop_code_lead": lead,
        "minidumps": dump_names,
        "events": events,
        "message": f"Stop code lead: {lead}",
    }