"""34.15 — Windows optional features (read + enable/disable with confirm)."""
from __future__ import annotations

import json
from typing import Any, Dict, List

from core.system_tools.network_tools import _ps


def list_optional_features(limit: int = 120) -> Dict[str, Any]:
    if __import__("os").name != "nt":
        return {"ok": False, "error": "Windows only.", "features": []}
    n = max(1, min(int(limit), 300))
    script = (
        f"$ErrorActionPreference='SilentlyContinue';"
        f"$rows = Get-WindowsOptionalFeature -Online | Select-Object -First {n} "
        "FeatureName, State, DisplayName;"
        " if (-not $rows) { '[]' } else { $rows | ConvertTo-Json -Compress }"
    )
    try:
        proc = _ps(script, timeout=120)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "features": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    features: List[Dict[str, str]] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        features.append(
            {
                "name": str(row.get("FeatureName") or ""),
                "state": str(row.get("State") or ""),
                "display": str(row.get("DisplayName") or ""),
            }
        )
    return {"ok": True, "features": features, "count": len(features)}


def set_optional_feature(name: str, enable: bool, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required. Restart may be required.", "needs_confirm": True}
    feat = str(name or "").strip()
    if not feat or len(feat) > 120 or "/" in feat or "\\" in feat:
        return {"ok": False, "error": "Feature name not allowed."}
    known = {f.get("name") for f in list_optional_features().get("features") or []}
    if feat not in known:
        return {"ok": False, "error": "Feature not found in the current list."}
    esc = feat.replace("'", "''")
    cmd = "Enable-WindowsOptionalFeature" if enable else "Disable-WindowsOptionalFeature"
    script = (
        f"$ErrorActionPreference='Stop';"
        f"{cmd} -Online -FeatureName '{esc}' -NoRestart | Out-Null;"
        "'OK'"
    )
    try:
        proc = _ps(script, timeout=600)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    if proc.returncode != 0:
        return {"ok": False, "error": (proc.stderr or proc.stdout or "Feature change failed.")[:800]}
    verb = "enabled" if enable else "disabled"
    return {"ok": True, "message": f"Optional feature {feat} {verb}. Restart if Windows asks.", "restart_maybe": True}