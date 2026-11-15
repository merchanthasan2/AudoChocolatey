"""Wave 3 — Windows health tools (tracker 32.x)."""
from __future__ import annotations

import json
import os
import re
import subprocess
from typing import Any, Dict, List, Optional

_SAFE_VOLUME = re.compile(r"^[A-Za-z]:$")
_SAFE_FEATURE = re.compile(r"^[A-Za-z0-9._-]{1,128}$")


def _ps(command: str, timeout: int = 120) -> subprocess.CompletedProcess:
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


def _run(args: List[str], timeout: int = 300) -> subprocess.CompletedProcess:
    flags = 0
    startupinfo = None
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


def _fail(msg: str, **extra: Any) -> Dict[str, Any]:
    out = {"ok": False, "error": msg}
    out.update(extra)
    return out


def _ok(message: str, **extra: Any) -> Dict[str, Any]:
    out = {"ok": True, "message": message}
    out.update(extra)
    return out


def open_windows_update_settings() -> Dict[str, Any]:
    """32.1 — open Windows Update (we do not replace the Windows Update agent)."""
    if os.name != "nt":
        return _fail("Windows only.")
    try:
        os.startfile("ms-settings:windowsupdate")  # type: ignore[attr-defined]
        return _ok("Opened Windows Update settings.")
    except Exception as e:
        return _fail(str(e))


def repair_windows_update_components(confirmed: bool = False) -> Dict[str, Any]:
    """32.2 — targeted WU service restart + SoftwareDistribution rename attempt."""
    if not confirmed:
        return _fail("Confirm required.", needs_confirm=True)
    if os.name != "nt":
        return _fail("Windows only.")
    script = (
        "$ErrorActionPreference='Stop';"
        "Stop-Service wuauserv,bits,cryptsvc -Force -ErrorAction SilentlyContinue;"
        "$sd = Join-Path $env:SystemRoot 'SoftwareDistribution';"
        "$cat = Join-Path $env:SystemRoot 'System32\\catroot2';"
        " if (Test-Path $sd) { Rename-Item $sd (\"SoftwareDistribution.bak.\" + (Get-Date -Format yyyyMMddHHmmss)) -ErrorAction SilentlyContinue };"
        " if (Test-Path $cat) { Rename-Item $cat (\"catroot2.bak.\" + (Get-Date -Format yyyyMMddHHmmss)) -ErrorAction SilentlyContinue };"
        "Start-Service cryptsvc,bits,wuauserv -ErrorAction SilentlyContinue;"
        "'OK'"
    )
    try:
        proc = _ps(script, timeout=180)
    except subprocess.TimeoutExpired:
        return _fail("Windows Update repair timed out.")
    if proc.returncode != 0:
        return _fail((proc.stderr or proc.stdout or "Repair failed.").strip())
    return _ok("Windows Update components were reset. Try Windows Update again.")


def list_uninstallable_updates() -> Dict[str, Any]:
    """32.3 — list updates that appear in uninstall history (read-only list)."""
    if os.name != "nt":
        return _fail("Windows only.", updates=[])
    script = (
        "$ErrorActionPreference='SilentlyContinue';"
        "$rows = Get-HotFix | Select-Object -First 200 HotFixID, Description, InstalledOn, InstalledBy;"
        " if (-not $rows) { '[]' } else { $rows | ConvertTo-Json -Compress }"
    )
    try:
        proc = _ps(script, timeout=90)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return _fail(str(e), updates=[])
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    updates = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        kb = str(row.get("HotFixID") or "").strip()
        if not kb or not re.match(r"^KB\d+$", kb, re.I):
            continue
        updates.append(
            {
                "id": kb,
                "description": str(row.get("Description") or ""),
                "installed_on": str(row.get("InstalledOn") or ""),
            }
        )
    return {"ok": True, "updates": updates, "count": len(updates)}


def uninstall_windows_update(kb_id: str, confirmed: bool = False) -> Dict[str, Any]:
    """32.3 — uninstall one hotfix via wusa /uninstall."""
    if not confirmed:
        return _fail("Confirm required. Removing updates can reduce security.", needs_confirm=True)
    kb = str(kb_id or "").strip().upper()
    if not re.match(r"^KB\d+$", kb):
        return _fail("That update id is not allowed.")
    listed = list_uninstallable_updates()
    ids = {u["id"].upper() for u in listed.get("updates") or []}
    if kb not in ids:
        return _fail("That update is not in the local hotfix list.")
    try:
        proc = _run(
            ["wusa.exe", "/uninstall", f"/kb:{kb[2:]}", "/quiet", "/norestart"],
            timeout=600,
        )
    except subprocess.TimeoutExpired:
        return _fail("Uninstall timed out.")
    # wusa often returns non-zero even on success; report output.
    return _ok(
        f"Requested uninstall of {kb}. A restart may be required.",
        exit_code=proc.returncode,
        detail=((proc.stdout or "") + (proc.stderr or ""))[:1500],
    )


def sfc_scan(repair: bool = False, confirmed: bool = False) -> Dict[str, Any]:
    """32.4 — sfc /verifyonly or /scannow."""
    if repair and not confirmed:
        return _fail("Confirm required for SFC repair.", needs_confirm=True)
    if os.name != "nt":
        return _fail("Windows only.")
    args = ["sfc.exe", "/scannow" if repair else "/verifyonly"]
    try:
        proc = _run(args, timeout=3600)
    except subprocess.TimeoutExpired:
        return _fail("SFC timed out.")
    out = ((proc.stdout or "") + "\n" + (proc.stderr or "")).strip()
    return {
        "ok": proc.returncode == 0,
        "message": "SFC finished." if proc.returncode == 0 else "SFC finished with warnings.",
        "exit_code": proc.returncode,
        "detail": out[-4000:],
        "repair": repair,
    }


def dism_restore_health(confirmed: bool = False, scan_only: bool = False) -> Dict[str, Any]:
    """32.5 — DISM ScanHealth or RestoreHealth."""
    if not scan_only and not confirmed:
        return _fail("Confirm required for DISM RestoreHealth.", needs_confirm=True)
    if os.name != "nt":
        return _fail("Windows only.")
    action = "ScanHealth" if scan_only else "RestoreHealth"
    args = [
        "DISM.exe",
        "/Online",
        "/Cleanup-Image",
        f"/{action}",
    ]
    try:
        proc = _run(args, timeout=7200)
    except subprocess.TimeoutExpired:
        return _fail(f"DISM {action} timed out.")
    out = ((proc.stdout or "") + "\n" + (proc.stderr or "")).strip()
    return {
        "ok": proc.returncode == 0,
        "message": f"DISM {action} finished.",
        "exit_code": proc.returncode,
        "detail": out[-4000:],
    }


def wmi_verify(salvage: bool = False, confirmed: bool = False) -> Dict[str, Any]:
    """32.6 — winmgmt /verifyrepository then optional salvage."""
    if salvage and not confirmed:
        return _fail("Confirm required for WMI salvage.", needs_confirm=True)
    if os.name != "nt":
        return _fail("Windows only.")
    try:
        verify = _run(["winmgmt.exe", "/verifyrepository"], timeout=120)
    except subprocess.TimeoutExpired:
        return _fail("WMI verify timed out.")
    detail = ((verify.stdout or "") + (verify.stderr or "")).strip()
    consistent = "consistent" in detail.lower() or verify.returncode == 0
    if not salvage:
        return {
            "ok": True,
            "message": "WMI repository looks consistent." if consistent else "WMI repository may be inconsistent.",
            "consistent": consistent,
            "detail": detail[-2000:],
            "exit_code": verify.returncode,
        }
    try:
        salvage_proc = _run(["winmgmt.exe", "/salvagerepository"], timeout=300)
    except subprocess.TimeoutExpired:
        return _fail("WMI salvage timed out.")
    sdetail = ((salvage_proc.stdout or "") + (salvage_proc.stderr or "")).strip()
    return _ok(
        "WMI salvage finished.",
        exit_code=salvage_proc.returncode,
        detail=sdetail[-2000:],
    )


def create_restore_point(description: str = "Quantum Updater", confirmed: bool = False) -> Dict[str, Any]:
    """32.7 — Checkpoint-Computer."""
    if not confirmed:
        return _fail("Confirm required.", needs_confirm=True)
    if os.name != "nt":
        return _fail("Windows only.")
    desc = re.sub(r"[^\w\s.\-]", "", str(description or "Quantum Updater"))[:60] or "Quantum Updater"
    script = (
        f"$ErrorActionPreference='Stop'; "
        f"Checkpoint-Computer -Description '{desc.replace(chr(39), '')}' -RestorePointType MODIFY_SETTINGS; 'OK'"
    )
    try:
        proc = _ps(script, timeout=180)
    except subprocess.TimeoutExpired:
        return _fail("Creating a restore point timed out.")
    if proc.returncode != 0:
        return _fail((proc.stderr or proc.stdout or "Restore point failed.").strip())
    return _ok("System restore point created.")


def disk_health() -> Dict[str, Any]:
    """32.8 — read-only physical disk health."""
    if os.name != "nt":
        return _fail("Windows only.", disks=[])
    script = (
        "$ErrorActionPreference='SilentlyContinue';"
        "$rows = Get-PhysicalDisk | Select-Object FriendlyName, MediaType, HealthStatus, OperationalStatus, Size;"
        " if (-not $rows) { '[]' } else { $rows | ConvertTo-Json -Compress }"
    )
    try:
        proc = _ps(script, timeout=60)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return _fail(str(e), disks=[])
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    disks = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        disks.append(
            {
                "name": str(row.get("FriendlyName") or ""),
                "media": str(row.get("MediaType") or ""),
                "health": str(row.get("HealthStatus") or ""),
                "status": str(row.get("OperationalStatus") or ""),
                "size_bytes": int(row.get("Size") or 0),
            }
        )
    return {"ok": True, "disks": disks, "count": len(disks)}


def volume_scan(drive: str = "C:", repair: bool = False, confirmed: bool = False) -> Dict[str, Any]:
    """32.9 / 32.10 — Repair-Volume -Scan or -SpotFix."""
    if repair and not confirmed:
        return _fail("Confirm required to repair the volume.", needs_confirm=True)
    vol = str(drive or "C:").strip().upper()
    if len(vol) == 1:
        vol = vol + ":"
    if not _SAFE_VOLUME.match(vol):
        return _fail("That volume letter is not allowed.")
    # Never force repair of unknown; SpotFix is online.
    mode = "SpotFix" if repair else "Scan"
    script = (
        f"$ErrorActionPreference='Stop'; "
        f"Repair-Volume -DriveLetter '{vol[0]}' -{mode} | "
        f"Select-Object DriveLetter, Action, Result | ConvertTo-Json -Compress"
    )
    try:
        proc = _ps(script, timeout=3600)
    except subprocess.TimeoutExpired:
        return _fail("Volume scan timed out.")
    if proc.returncode != 0:
        return _fail((proc.stderr or proc.stdout or "Volume scan failed.").strip())
    return _ok(f"Volume {vol} {mode} finished.", detail=(proc.stdout or "").strip()[:2000])


def ssd_retrim(drive: str = "C:", confirmed: bool = False) -> Dict[str, Any]:
    """32.11 — Optimize-Volume -ReTrim."""
    if not confirmed:
        return _fail("Confirm required.", needs_confirm=True)
    vol = str(drive or "C:").strip().upper()
    if len(vol) == 1:
        vol = vol + ":"
    if not _SAFE_VOLUME.match(vol):
        return _fail("That volume letter is not allowed.")
    script = (
        f"$ErrorActionPreference='Stop'; "
        f"Optimize-Volume -DriveLetter '{vol[0]}' -ReTrim -Verbose; 'OK'"
    )
    try:
        proc = _ps(script, timeout=1800)
    except subprocess.TimeoutExpired:
        return _fail("ReTrim timed out.")
    if proc.returncode != 0:
        return _fail((proc.stderr or proc.stdout or "ReTrim failed.").strip())
    return _ok(f"ReTrim finished on {vol}.")


def list_windows_events(log_name: str = "System", max_events: int = 50) -> Dict[str, Any]:
    """32.12 — recent events (read-only)."""
    if os.name != "nt":
        return _fail("Windows only.", events=[])
    log = str(log_name or "System")
    if log not in ("System", "Application", "Security"):
        return _fail("Choose System, Application, or Security.", events=[])
    n = max(1, min(int(max_events), 200))
    script = (
        f"$ErrorActionPreference='SilentlyContinue';"
        f"$rows = Get-WinEvent -LogName '{log}' -MaxEvents {n} |"
        " Select-Object TimeCreated, Id, LevelDisplayName, ProviderName, Message;"
        " if (-not $rows) { '[]' } else { $rows | ConvertTo-Json -Compress -Depth 3 }"
    )
    try:
        proc = _ps(script, timeout=90)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return _fail(str(e), events=[])
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    events = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        msg = str(row.get("Message") or "").replace("\r", " ").replace("\n", " ")
        events.append(
            {
                "time": str(row.get("TimeCreated") or ""),
                "id": str(row.get("Id") or ""),
                "level": str(row.get("LevelDisplayName") or ""),
                "provider": str(row.get("ProviderName") or ""),
                "message": msg[:300],
            }
        )
    return {"ok": True, "events": events, "count": len(events), "log": log}


def export_windows_events(log_name: str = "System", path: str = "") -> Dict[str, Any]:
    """32.12 — export recent events to a json file under Downloads."""
    listed = list_windows_events(log_name=log_name, max_events=100)
    if not listed.get("ok"):
        return listed
    downloads = os.path.join(os.path.expanduser("~"), "Downloads")
    out = path.strip() if path else os.path.join(downloads, f"quantum-updater-{log_name.lower()}-events.json")
    out = os.path.abspath(out)
    if not out.lower().startswith(os.path.abspath(downloads).lower()):
        return _fail("Export path must be under Downloads.")
    if not out.lower().endswith(".json"):
        return _fail("Export must be a .json file.")
    try:
        with open(out, "w", encoding="utf-8") as handle:
            json.dump(listed.get("events") or [], handle, indent=2)
    except OSError as e:
        return _fail(str(e))
    return _ok("Events exported.", path=out, count=listed.get("count", 0))


def defender_status() -> Dict[str, Any]:
    """32.15 — Get-MpComputerStatus (also covers 32.13 readiness)."""
    if os.name != "nt":
        return _fail("Windows only.")
    script = (
        "$ErrorActionPreference='Stop';"
        "Get-MpComputerStatus | Select-Object AMServiceEnabled, AntivirusEnabled, "
        "RealTimeProtectionEnabled, IoavProtectionEnabled, AntispywareEnabled, "
        "AntivirusSignatureLastUpdated, QuickScanAge, FullScanAge |"
        " ConvertTo-Json -Compress"
    )
    try:
        proc = _ps(script, timeout=60)
        data = json.loads((proc.stdout or "").strip() or "{}")
    except Exception as e:
        return _fail(str(e))
    if not isinstance(data, dict):
        return _fail("Could not read Defender status.")
    return {"ok": True, "status": data}


def update_defender_signatures(confirmed: bool = False) -> Dict[str, Any]:
    """32.15 — Update-MpSignature."""
    if not confirmed:
        return _fail("Confirm required.", needs_confirm=True)
    if os.name != "nt":
        return _fail("Windows only.")
    try:
        proc = _ps("$ErrorActionPreference='Stop'; Update-MpSignature; 'OK'", timeout=300)
    except subprocess.TimeoutExpired:
        return _fail("Signature update timed out.")
    if proc.returncode != 0:
        return _fail((proc.stderr or proc.stdout or "Update failed.").strip())
    return _ok("Defender intelligence updated.")


def start_defender_scan(full: bool = False, confirmed: bool = False) -> Dict[str, Any]:
    """32.13 — Start-MpScan."""
    if not confirmed:
        return _fail("Confirm required.", needs_confirm=True)
    if os.name != "nt":
        return _fail("Windows only.")
    kind = "FullScan" if full else "QuickScan"
    script = f"$ErrorActionPreference='Stop'; Start-MpScan -ScanType {kind}; 'OK'"
    try:
        proc = _ps(script, timeout=60)
    except subprocess.TimeoutExpired:
        return _fail("Could not start the scan.")
    if proc.returncode != 0:
        return _fail((proc.stderr or proc.stdout or "Scan failed to start.").strip())
    return _ok(f"Windows Security {kind} started.")


def bitlocker_status() -> Dict[str, Any]:
    """32.14 — Get-BitLockerVolume status only. Never returns recovery keys."""
    if os.name != "nt":
        return _fail("Windows only.", volumes=[])
    script = (
        "$ErrorActionPreference='SilentlyContinue';"
        "$rows = Get-BitLockerVolume | Select-Object MountPoint, VolumeStatus, ProtectionStatus, EncryptionPercentage, VolumeType;"
        " if (-not $rows) { '[]' } else { $rows | ConvertTo-Json -Compress }"
    )
    try:
        proc = _ps(script, timeout=60)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return _fail(str(e), volumes=[])
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    volumes = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        # Explicitly drop any key-like fields if present.
        volumes.append(
            {
                "mount": str(row.get("MountPoint") or ""),
                "volume_status": str(row.get("VolumeStatus") or ""),
                "protection": str(row.get("ProtectionStatus") or ""),
                "encryption_percent": str(row.get("EncryptionPercentage") or ""),
                "volume_type": str(row.get("VolumeType") or ""),
            }
        )
    return {"ok": True, "volumes": volumes, "count": len(volumes), "note": "Recovery keys are never shown."}
