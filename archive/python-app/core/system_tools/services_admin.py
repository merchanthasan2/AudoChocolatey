"""34.14 — Service management with security/update denylist."""
from __future__ import annotations

import json
import re
from typing import Any, Dict, List

from core.system_tools.network_tools import _ps

_SERVICE_NAME = re.compile(r"^[A-Za-z0-9_.-]{1,120}$")

DENY_SERVICES = {
    "wuauserv",
    "UsoSvc",
    "bits",
    "WinDefend",
    "WdNisSvc",
    "Sense",
    "SecurityHealthService",
    "MpsSvc",
    "mpssvc",
    "BFE",
    "Dhcp",
    "Dnscache",
    "EventLog",
    "LSM",
    "RpcSs",
    "SamSs",
    "LanmanWorkstation",
    "CryptSvc",
}


def list_manageable_services(limit: int = 150) -> Dict[str, Any]:
    if __import__("os").name != "nt":
        return {"ok": False, "error": "Windows only.", "services": []}
    n = max(1, min(int(limit), 400))
    script = (
        f"Get-Service | Sort-Object DisplayName | Select-Object -First {n} "
        "Name, DisplayName, Status, StartType | ConvertTo-Json -Compress"
    )
    try:
        proc = _ps(script, timeout=60)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "services": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    services: List[Dict[str, Any]] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        name = str(row.get("Name") or "")
        services.append(
            {
                "name": name,
                "display_name": str(row.get("DisplayName") or ""),
                "status": str(row.get("Status") or ""),
                "start_type": str(row.get("StartType") or ""),
                "denylisted": name in DENY_SERVICES,
            }
        )
    return {"ok": True, "services": services, "count": len(services)}


def set_service_state(name: str, action: str, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    svc = str(name or "").strip()
    if not _SERVICE_NAME.match(svc):
        return {"ok": False, "error": "Service name not allowed."}
    if svc in DENY_SERVICES:
        return {"ok": False, "error": "That service is protected and cannot be changed."}
    act = str(action or "").strip().lower()
    if act not in ("start", "stop", "restart"):
        return {"ok": False, "error": "Action must be start, stop, or restart."}
    escaped = svc.replace("'", "''")
    if act == "restart":
        body = f"Restart-Service -Name '{escaped}' -Force -ErrorAction Stop"
    elif act == "start":
        body = f"Start-Service -Name '{escaped}' -ErrorAction Stop"
    else:
        body = f"Stop-Service -Name '{escaped}' -Force -ErrorAction Stop"
    script = f"$ErrorActionPreference='Stop'; {body}; 'OK'"
    try:
        proc = _ps(script, timeout=90)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    if proc.returncode != 0:
        return {"ok": False, "error": (proc.stderr or proc.stdout or "Service action failed.")[:500]}
    return {"ok": True, "message": f"Service {svc} {act} requested."}