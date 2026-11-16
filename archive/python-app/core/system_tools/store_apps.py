"""Microsoft Store (AppX) repair helpers for Wave 2 tracker 31.8.

Re-register only packages already installed for this Windows account.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
from typing import Any, Dict, List, Optional

_SAFE_FULL_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,255}$")


def _ps_run(command: str, timeout: int = 120) -> subprocess.CompletedProcess:
    flags = 0
    startupinfo = None
    if os.name == "nt":
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startupinfo.wShowWindow = 0
        flags = 0x08000000
    return subprocess.run(
        [
            "powershell.exe",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            command,
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        startupinfo=startupinfo,
        creationflags=flags,
    )


def list_store_packages(limit: int = 400) -> Dict[str, Any]:
    """Return AppX packages for the current user (not all users)."""
    if os.name != "nt":
        return {"ok": False, "error": "Store apps are only available on Windows.", "packages": []}
    capped = max(1, min(int(limit), 1000))
    script = (
        "$ErrorActionPreference='Stop';"
        f"$pkgs = Get-AppxPackage | Where-Object {{ -not $_.IsFramework }} |"
        f" Select-Object -First {capped} Name, PackageFullName, InstallLocation, Publisher;"
        " if (-not $pkgs) { '[]' } else { $pkgs | ConvertTo-Json -Compress -Depth 3 }"
    )
    try:
        proc = _ps_run(script, timeout=120)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Listing Store apps timed out.", "packages": []}
    except Exception as e:
        return {"ok": False, "error": str(e), "packages": []}
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        return {"ok": False, "error": err or f"List failed (exit {proc.returncode}).", "packages": []}
    raw = (proc.stdout or "").strip()
    if not raw:
        data: Any = []
    else:
        try:
            data = json.loads(raw)
        except json.JSONDecodeError:
            return {"ok": False, "error": "Could not read the Store app list.", "packages": []}
    if isinstance(data, dict):
        rows = [data]
    elif isinstance(data, list):
        rows = data
    else:
        rows = []
    packages: List[Dict[str, str]] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        full = str(row.get("PackageFullName") or "").strip()
        name = str(row.get("Name") or "").strip()
        loc = str(row.get("InstallLocation") or "").strip()
        if not full or not _SAFE_FULL_NAME.match(full):
            continue
        packages.append(
            {
                "name": name or full,
                "package_full_name": full,
                "install_location": loc,
                "publisher": str(row.get("Publisher") or "").strip(),
            }
        )
    packages.sort(key=lambda p: p["name"].lower())
    return {"ok": True, "packages": packages, "count": len(packages)}


def _manifest_for_package(package_full_name: str) -> Optional[str]:
    listed = list_store_packages()
    if not listed.get("ok"):
        return None
    for pkg in listed.get("packages") or []:
        if pkg.get("package_full_name") != package_full_name:
            continue
        loc = pkg.get("install_location") or ""
        if not loc or not os.path.isdir(loc):
            return None
        manifest = os.path.join(loc, "AppxManifest.xml")
        if os.path.isfile(manifest):
            return manifest
        return None
    return None


def reregister_store_package(package_full_name: str, confirmed: bool = False) -> Dict[str, Any]:
    """Re-register one installed Store package for this account."""
    if not confirmed:
        return {
            "ok": False,
            "error": "Re-register requires explicit confirmation.",
            "needs_confirm": True,
        }
    if os.name != "nt":
        return {"ok": False, "error": "Store apps are only available on Windows."}
    full = str(package_full_name or "").strip()
    if not full or not _SAFE_FULL_NAME.match(full):
        return {"ok": False, "error": "That package id is not allowed."}
    manifest = _manifest_for_package(full)
    if not manifest:
        return {
            "ok": False,
            "error": "That Store app is not installed for this account, or its manifest is missing.",
        }
    # Path comes only from Get-AppxPackage InstallLocation for a listed package.
    escaped = manifest.replace("'", "''")
    try:
        proc = _ps_run(
            f"$ErrorActionPreference='Stop'; "
            f"Add-AppxPackage -DisableDevelopmentMode -Register '{escaped}'",
            timeout=180,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Store app re-register timed out."}
    except Exception as e:
        return {"ok": False, "error": str(e)}
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        return {
            "ok": False,
            "error": err or f"Re-register failed (exit {proc.returncode}).",
            "exit_code": proc.returncode,
        }
    return {
        "ok": True,
        "message": "Store app re-registered for this account.",
        "package_full_name": full,
        "manifest": manifest,
        "exit_code": 0,
    }
