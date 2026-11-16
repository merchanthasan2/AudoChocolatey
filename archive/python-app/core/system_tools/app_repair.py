"""Desktop and packaged app repair for Wave 2 tracker 31.5 / 31.6."""
from __future__ import annotations

import os
import re
import subprocess
from typing import Any, Dict, Optional

from core.system_tools import store_apps
from core.system_tools.catalog_export import resolve_winget_executable

_SAFE_PACKAGE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,180}$")
_SAFE_FULL_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,255}$")


def _run_winget(args: list, timeout: int = 900) -> subprocess.CompletedProcess:
    startupinfo = None
    flags = 0
    if os.name == "nt":
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startupinfo.wShowWindow = 0
        flags = 0x08000000
    return subprocess.run(
        args,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        startupinfo=startupinfo,
        creationflags=flags,
    )


def list_packaged_apps(limit: int = 400) -> Dict[str, Any]:
    """List AppX packages for the current user (same as Store app repair list)."""
    return store_apps.list_store_packages(limit=limit)


def repair_desktop(package_id: str, confirmed: bool = False) -> Dict[str, Any]:
    """Run winget repair on one package id when supported."""
    if not confirmed:
        return {
            "ok": False,
            "error": "Desktop app repair requires explicit confirmation.",
            "needs_confirm": True,
        }
    if os.name != "nt":
        return {"ok": False, "error": "App repair is only available on Windows."}
    pid = str(package_id or "").strip()
    if not pid or not _SAFE_PACKAGE_ID.match(pid):
        return {"ok": False, "error": "That package id is not allowed."}
    exe = resolve_winget_executable()
    if not exe:
        return {"ok": False, "error": "WinGet is not available on this PC."}
    try:
        res = _run_winget(
            [exe, "repair", "--id", pid, "--exact", "--disable-interactivity"],
            timeout=900,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Desktop app repair timed out."}
    except Exception as e:
        return {"ok": False, "error": str(e)}
    out = ((res.stdout or "") + "\n" + (res.stderr or "")).strip()
    ok = res.returncode == 0
    if not ok and "not found" in out.lower():
        return {
            "ok": False,
            "error": "Repair is not available for that package, or it is not installed.",
            "exit_code": res.returncode,
            "detail": out[:2000],
        }
    return {
        "ok": ok,
        "message": "Desktop app repair finished." if ok else (out or f"Repair failed (exit {res.returncode})."),
        "package_id": pid,
        "exit_code": res.returncode,
        "detail": out[:2000],
        "error": None if ok else (out or f"Repair failed (exit {res.returncode})."),
    }


def _package_listed(package_full_name: str) -> bool:
    listed = list_packaged_apps()
    if not listed.get("ok"):
        return False
    for pkg in listed.get("packages") or []:
        if pkg.get("package_full_name") == package_full_name:
            return True
    return False


def reset_packaged_app(package_full_name: str, confirmed: bool = False) -> Dict[str, Any]:
    """Reset one listed Store app — removes that app's local data."""
    if not confirmed:
        return {
            "ok": False,
            "error": "Reset requires explicit confirmation.",
            "needs_confirm": True,
            "warn_data_wipe": True,
        }
    if os.name != "nt":
        return {"ok": False, "error": "Packaged app reset is only available on Windows."}
    full = str(package_full_name or "").strip()
    if not full or not _SAFE_FULL_NAME.match(full):
        return {"ok": False, "error": "That package id is not allowed."}
    if not _package_listed(full):
        return {
            "ok": False,
            "error": "That Store app is not installed for this account.",
        }
    escaped = full.replace("'", "''")
    try:
        proc = store_apps._ps_run(
            f"$ErrorActionPreference='Stop'; Reset-AppxPackage -Package '{escaped}' -Confirm:$false",
            timeout=300,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Packaged app reset timed out."}
    except Exception as e:
        return {"ok": False, "error": str(e)}
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        return {
            "ok": False,
            "error": err or f"Reset failed (exit {proc.returncode}).",
            "exit_code": proc.returncode,
        }
    return {
        "ok": True,
        "message": (
            "Store app reset finished. That app's saved data on this PC was removed."
        ),
        "package_full_name": full,
        "exit_code": 0,
        "warn_data_wipe": True,
    }


def repair_packaged_app(package_full_name: str, confirmed: bool = False) -> Dict[str, Any]:
    """Optional packaged repair — winget repair by package name when listed."""
    if not confirmed:
        return {
            "ok": False,
            "error": "Packaged app repair requires explicit confirmation.",
            "needs_confirm": True,
        }
    if os.name != "nt":
        return {"ok": False, "error": "Packaged app repair is only available on Windows."}
    full = str(package_full_name or "").strip()
    if not full or not _SAFE_FULL_NAME.match(full):
        return {"ok": False, "error": "That package id is not allowed."}
    listed = list_packaged_apps()
    if not listed.get("ok"):
        return {"ok": False, "error": listed.get("error") or "Could not load Store apps."}
    name = None
    for pkg in listed.get("packages") or []:
        if pkg.get("package_full_name") == full:
            name = str(pkg.get("name") or "").strip()
            break
    if not name:
        return {
            "ok": False,
            "error": "That Store app is not installed for this account.",
        }
    exe = resolve_winget_executable()
    if not exe:
        return {"ok": False, "error": "WinGet is not available on this PC.", "supported": False}
    try:
        res = _run_winget(
            [exe, "repair", "--name", name, "--exact", "--disable-interactivity"],
            timeout=900,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Packaged app repair timed out."}
    except Exception as e:
        return {"ok": False, "error": str(e)}
    out = ((res.stdout or "") + "\n" + (res.stderr or "")).strip()
    ok = res.returncode == 0
    if not ok:
        lower = out.lower()
        if "no applicable" in lower or "not found" in lower or res.returncode != 0:
            return {
                "ok": False,
                "error": "Packaged app repair is not supported for that app on this PC.",
                "supported": False,
                "exit_code": res.returncode,
                "detail": out[:2000],
            }
    return {
        "ok": ok,
        "message": "Packaged app repair finished." if ok else (out or "Repair failed."),
        "package_full_name": full,
        "exit_code": res.returncode,
        "detail": out[:2000],
        "supported": True,
        "error": None if ok else (out or "Repair failed."),
    }