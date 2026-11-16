"""34.6 — Find and repair broken Windows shortcuts (.lnk)."""
from __future__ import annotations

import glob
import json
import os
from typing import Any, Dict, List

from core.system_tools.file_tools import _under_user_or_removable
from core.system_tools.network_tools import _ps


def find_broken_shortcuts(root: str, limit: int = 200) -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Windows only.", "shortcuts": []}
    base = os.path.abspath(root)
    if not os.path.isdir(base):
        return {"ok": False, "error": "Folder not found.", "shortcuts": []}
    if not _under_user_or_removable(base):
        return {"ok": False, "error": "Root must be under your profile or a normal drive path.", "shortcuts": []}
    capped = max(1, min(int(limit), 1000))
    pattern = os.path.join(base, "**", "*.lnk")
    paths = glob.glob(pattern, recursive=True)[: capped * 2]
    broken: List[Dict[str, str]] = []
    for lnk in paths:
        if len(broken) >= capped:
            break
        esc = lnk.replace("'", "''")
        script = (
            "$sh = New-Object -ComObject WScript.Shell;"
            f"$s = $sh.CreateShortcut('{esc}');"
            "[pscustomobject]@{target=$s.TargetPath; exists=(Test-Path $s.TargetPath)}"
            " | ConvertTo-Json -Compress"
        )
        try:
            proc = _ps(script, timeout=20)
            data = json.loads((proc.stdout or "").strip() or "{}")
        except Exception:
            continue
        if not isinstance(data, dict) or data.get("exists"):
            continue
        target = str(data.get("target") or "")
        broken.append({"path": lnk, "target": target})
    return {"ok": True, "shortcuts": broken, "count": len(broken)}


def repair_broken_shortcut(lnk_path: str, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    ap = os.path.abspath(lnk_path)
    if not ap.lower().endswith(".lnk") or not os.path.isfile(ap):
        return {"ok": False, "error": "Shortcut not found."}
    scan = find_broken_shortcuts(os.path.dirname(ap), limit=500)
    match = next((s for s in scan.get("shortcuts") or [] if os.path.normcase(s["path"]) == os.path.normcase(ap)), None)
    if not match:
        return {"ok": False, "error": "Shortcut is not in the broken list. Scan first."}
    target = match.get("target") or ""
    if not target:
        return {"ok": False, "error": "Shortcut has no target to repair."}
    base = os.path.basename(target)
    parent = os.path.dirname(target)
    candidate = os.path.join(parent, base)
    if not os.path.isfile(candidate):
        # search same folder as shortcut for same file name
        alt = os.path.join(os.path.dirname(ap), base)
        if os.path.isfile(alt):
            candidate = alt
        else:
            return {"ok": False, "error": "No replacement target found nearby."}
    esc_lnk = ap.replace("'", "''")
    esc_tgt = candidate.replace("'", "''")
    script = (
        "$sh = New-Object -ComObject WScript.Shell;"
        f"$s = $sh.CreateShortcut('{esc_lnk}');"
        f"$s.TargetPath = '{esc_tgt}';"
        "$s.Save(); 'OK'"
    )
    try:
        proc = _ps(script, timeout=30)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    return {
        "ok": proc.returncode == 0,
        "message": "Shortcut target updated." if proc.returncode == 0 else "Repair failed.",
        "new_target": candidate,
    }