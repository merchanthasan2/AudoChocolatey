"""Remaining Wave 4/5 helpers (33.x / 34.x leftovers)."""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import tempfile
from typing import Any, Dict, List, Optional

SERVICE_DENYLIST = frozenset({
    "wuauserv", "bits", "cryptsvc", "TrustedInstaller", "WinDefend", "Sense",
    "SecurityHealthService", "mpssvc", "EventLog", "RpcSs", "DcomLaunch",
    "Lsass", "Winmgmt", "Schedule", "BrokerInfrastructure",
})


def _ps(command: str, timeout: int = 90) -> subprocess.CompletedProcess:
    flags = 0
    startupinfo = None
    if os.name == "nt":
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startupinfo.wShowWindow = 0
        flags = 0x08000000
    return subprocess.run(
        [
            "powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-Command", command,
        ],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
        timeout=timeout, startupinfo=startupinfo, creationflags=flags,
    )


def _run(args: List[str], timeout: int = 60) -> subprocess.CompletedProcess:
    flags = 0x08000000 if os.name == "nt" else 0
    startupinfo = None
    if os.name == "nt":
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startupinfo.wShowWindow = 0
    return subprocess.run(
        args, capture_output=True, text=True, encoding="utf-8", errors="replace",
        timeout=timeout, startupinfo=startupinfo, creationflags=flags,
    )


def open_privacy_settings() -> Dict[str, Any]:
    """34.10"""
    try:
        os.startfile("ms-settings:privacy")  # type: ignore[attr-defined]
        return {"ok": True, "message": "Opened Windows privacy settings."}
    except Exception as e:
        return {"ok": False, "error": str(e)}


def list_optional_features() -> Dict[str, Any]:
    """34.15"""
    script = (
        "Get-WindowsOptionalFeature -Online | Select-Object -First 200 FeatureName, State |"
        " ConvertTo-Json -Compress"
    )
    try:
        proc = _ps(script, timeout=180)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "features": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    features = [
        {"name": str(r.get("FeatureName") or ""), "state": str(r.get("State") or "")}
        for r in rows if isinstance(r, dict) and r.get("FeatureName")
    ]
    return {"ok": True, "features": features, "count": len(features)}


def set_optional_feature(name: str, enable: bool, confirmed: bool = False) -> Dict[str, Any]:
    """34.15"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    feat = str(name or "").strip()
    if not re.match(r"^[A-Za-z0-9._-]{1,128}$", feat):
        return {"ok": False, "error": "That feature name is not allowed."}
    cmd = "Enable-WindowsOptionalFeature" if enable else "Disable-WindowsOptionalFeature"
    script = f"$ErrorActionPreference='Stop'; {cmd} -Online -FeatureName '{feat}' -NoRestart; 'OK'"
    try:
        proc = _ps(script, timeout=600)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Optional feature change timed out."}
    if proc.returncode != 0:
        return {"ok": False, "error": (proc.stderr or proc.stdout or "Failed.").strip()}
    return {"ok": True, "message": f"{'Enabled' if enable else 'Disabled'} {feat}. A restart may be required."}


def list_services(limit: int = 200) -> Dict[str, Any]:
    """34.14"""
    script = (
        f"Get-Service | Select-Object -First {max(1, min(limit, 400))} Name, DisplayName, Status, StartType |"
        " ConvertTo-Json -Compress"
    )
    try:
        proc = _ps(script, timeout=60)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "services": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    services = []
    for r in rows:
        if not isinstance(r, dict):
            continue
        name = str(r.get("Name") or "")
        services.append({
            "name": name,
            "display": str(r.get("DisplayName") or ""),
            "status": str(r.get("Status") or ""),
            "start_type": str(r.get("StartType") or ""),
            "protected": name in SERVICE_DENYLIST,
        })
    return {"ok": True, "services": services, "count": len(services)}


def set_service_state(name: str, action: str, confirmed: bool = False) -> Dict[str, Any]:
    """34.14 action: start|stop|restart"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    svc = str(name or "").strip()
    if not re.match(r"^[A-Za-z0-9._-]{1,128}$", svc):
        return {"ok": False, "error": "That service name is not allowed."}
    if svc in SERVICE_DENYLIST:
        return {"ok": False, "error": "That security or update service is protected."}
    act = str(action or "").lower()
    if act not in ("start", "stop", "restart"):
        return {"ok": False, "error": "Choose start, stop, or restart."}
    try:
        if act == "restart":
            _run(["sc.exe", "stop", svc], timeout=60)
            proc = _run(["sc.exe", "start", svc], timeout=60)
        else:
            proc = _run(["sc.exe", act, svc], timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Service action timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": f"Service {svc} {act} requested." if proc.returncode == 0 else f"Could not {act} {svc}.",
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }


def reset_tcpip(confirmed: bool = False) -> Dict[str, Any]:
    """33.7 — backup ipconfig /all then netsh int ip reset"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required. Network may drop.", "needs_confirm": True}
    downloads = os.path.join(os.path.expanduser("~"), "Downloads")
    backup = os.path.join(downloads, "quantum-updater-ipconfig-backup.txt")
    try:
        info = _run(["ipconfig", "/all"], timeout=30)
        with open(backup, "w", encoding="utf-8") as handle:
            handle.write(info.stdout or "")
        proc = _run(["netsh", "int", "ip", "reset"], timeout=60)
    except Exception as e:
        return {"ok": False, "error": str(e)}
    return {
        "ok": proc.returncode == 0,
        "message": "TCP/IP reset requested. Restart may be required.",
        "backup": backup,
        "restart_required": True,
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }


def firewall_export(path: str = "") -> Dict[str, Any]:
    """33.18 / 33.9 prep"""
    downloads = os.path.join(os.path.expanduser("~"), "Downloads")
    out = path.strip() if path else os.path.join(downloads, "quantum-updater-firewall-backup.wfw")
    out = os.path.abspath(out)
    if not out.lower().startswith(os.path.abspath(downloads).lower()):
        return {"ok": False, "error": "Backup must be under Downloads."}
    try:
        proc = _run(["netsh", "advfirewall", "export", out], timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Firewall export timed out."}
    return {
        "ok": proc.returncode == 0 and os.path.isfile(out),
        "message": "Firewall rules exported." if proc.returncode == 0 else "Export failed.",
        "path": out,
    }


def firewall_restore_defaults(confirmed: bool = False) -> Dict[str, Any]:
    """33.9 — export first then reset"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    exported = firewall_export()
    try:
        proc = _run(["netsh", "advfirewall", "reset"], timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Firewall reset timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": "Firewall restored to defaults.",
        "backup": exported.get("path"),
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }


def firewall_rules_list(limit: int = 100) -> Dict[str, Any]:
    """33.18"""
    script = (
        f"Get-NetFirewallRule | Select-Object -First {max(1, min(limit, 300))} "
        "DisplayName, Enabled, Direction, Action | ConvertTo-Json -Compress"
    )
    try:
        proc = _ps(script, timeout=90)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "rules": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    rules = [
        {
            "name": str(r.get("DisplayName") or ""),
            "enabled": str(r.get("Enabled") or ""),
            "direction": str(r.get("Direction") or ""),
            "action": str(r.get("Action") or ""),
        }
        for r in rows if isinstance(r, dict)
    ]
    return {"ok": True, "rules": rules, "count": len(rules)}


def wifi_survey() -> Dict[str, Any]:
    """33.15"""
    try:
        proc = _run(["netsh", "wlan", "show", "networks", "mode=bssid"], timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Wi-Fi survey timed out."}
    text = (proc.stdout or "") + (proc.stderr or "")
    networks = []
    current: Dict[str, str] = {}
    for line in text.splitlines():
        if line.strip().startswith("SSID") and "BSSID" not in line:
            if current:
                networks.append(current)
            current = {"ssid": line.split(":", 1)[-1].strip()}
        elif "Signal" in line and current:
            current["signal"] = line.split(":", 1)[-1].strip()
        elif "Channel" in line and current and "channel" not in current:
            current["channel"] = line.split(":", 1)[-1].strip()
        elif "Authentication" in line and current and "auth" not in current:
            current["auth"] = line.split(":", 1)[-1].strip()
    if current:
        networks.append(current)
    return {"ok": True, "networks": networks[:80], "count": len(networks), "detail": text[-2000:]}


def sleep_wake_report() -> Dict[str, Any]:
    """33.3"""
    try:
        proc = _run(["powercfg", "/sleepstudy"], timeout=30)
    except Exception:
        proc = None
    try:
        avail = _run(["powercfg", "/availablesleepstates"], timeout=30)
        requests = _run(["powercfg", "/requests"], timeout=30)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Sleep report timed out."}
    return {
        "ok": True,
        "available": (avail.stdout or "")[:2000],
        "requests": (requests.stdout or "")[:2000],
        "note": "Use powercfg /sleepstudy from an elevated Command Prompt for the full HTML report if needed.",
    }


def bsod_triage(limit: int = 20) -> Dict[str, Any]:
    """33.2 — recent bugcheck events"""
    script = (
        f"Get-WinEvent -FilterHashtable @{{LogName='System'; Id=1001}} -MaxEvents {max(1, min(limit, 50))} "
        "| Select-Object TimeCreated, Id, Message | ConvertTo-Json -Compress"
    )
    try:
        proc = _ps(script, timeout=60)
        raw = (proc.stdout or "").strip() or "[]"
        data = json.loads(raw)
    except Exception as e:
        return {"ok": False, "error": str(e), "events": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    events = []
    for r in rows:
        if not isinstance(r, dict):
            continue
        msg = str(r.get("Message") or "").replace("\n", " ")
        code = ""
        m = re.search(r"0x[0-9a-fA-F]+", msg)
        if m:
            code = m.group(0)
        events.append({
            "time": str(r.get("TimeCreated") or ""),
            "stop_code": code,
            "message": msg[:400],
        })
    return {"ok": True, "events": events, "count": len(events)}


def keep_awake(minutes: int = 30, confirmed: bool = False) -> Dict[str, Any]:
    """34.17 — temporary request via powercfg /change monitor/standby timeouts note; use ES continuous in a child process is heavy.
    Simpler: set temporary away mode via powercfg /requestsoverride is complex. Use Start-Process powershell with sleep + ES_CONTINUOUS via kernel32 in a short script file.
    """
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    mins = max(1, min(int(minutes), 240))
    # Create a small helper that holds execution state then exits.
    script = (
        "Add-Type -TypeDefinition @'\n"
        "using System; using System.Runtime.InteropServices;\n"
        "public static class Native { [DllImport(\"kernel32.dll\")] public static extern uint SetThreadExecutionState(uint esFlags); }\n"
        "'@;\n"
        "[void][Native]::SetThreadExecutionState(0x80000000 -bor 0x00000001 -bor 0x00000002);\n"
        f"Start-Sleep -Seconds {mins * 60};\n"
        "[void][Native]::SetThreadExecutionState(0x80000000);\n"
    )
    try:
        flags = 0x08000000
        subprocess.Popen(
            ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script],
            creationflags=flags,
        )
    except Exception as e:
        return {"ok": False, "error": str(e)}
    return {"ok": True, "message": f"Keeping this PC awake for about {mins} minutes. Close that PowerShell window to cancel early."}


def folder_compression_savings(path: str) -> Dict[str, Any]:
    """34.2 — measure NTFS compression potential via compact /q"""
    ap = os.path.abspath(path)
    if not os.path.isdir(ap):
        return {"ok": False, "error": "Folder not found."}
    try:
        proc = _run(["compact.exe", "/q", "/s:" + ap], timeout=300)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Compression query timed out."}
    text = (proc.stdout or "") + (proc.stderr or "")
    return {"ok": True, "path": ap, "detail": text[-2500:], "message": "Compression report ready."}


def set_folder_compression(path: str, enable: bool, confirmed: bool = False) -> Dict[str, Any]:
    """34.2"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    ap = os.path.abspath(path)
    if not os.path.isdir(ap):
        return {"ok": False, "error": "Folder not found."}
    args = ["compact.exe", "/c" if enable else "/u", "/s:" + ap]
    try:
        proc = _run(args, timeout=600)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Compression change timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": ("Compression enabled." if enable else "Compression cleared.") if proc.returncode == 0 else "Compression change failed.",
        "detail": ((proc.stdout or "") + (proc.stderr or ""))[-2000:],
    }


def mount_image(path: str, confirmed: bool = False) -> Dict[str, Any]:
    """34.3 ISO/VHD mount read-only default"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    ap = os.path.abspath(path)
    if not os.path.isfile(ap):
        return {"ok": False, "error": "Image file not found."}
    if not ap.lower().endswith((".iso", ".vhd", ".vhdx")):
        return {"ok": False, "error": "Choose an .iso, .vhd, or .vhdx file."}
    escaped = ap.replace("'", "''")
    script = f"Mount-DiskImage -ImagePath '{escaped}' -Access ReadOnly; 'OK'"
    try:
        proc = _ps(script, timeout=120)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Mount timed out."}
    if proc.returncode != 0:
        return {"ok": False, "error": (proc.stderr or proc.stdout or "Mount failed.").strip()}
    return {"ok": True, "message": "Image mounted read-only.", "path": ap}


def eject_image(path: str, confirmed: bool = False) -> Dict[str, Any]:
    """34.3"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    ap = os.path.abspath(path)
    escaped = ap.replace("'", "''")
    script = f"Dismount-DiskImage -ImagePath '{escaped}'; 'OK'"
    try:
        proc = _ps(script, timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Eject timed out."}
    if proc.returncode != 0:
        return {"ok": False, "error": (proc.stderr or proc.stdout or "Eject failed.").strip()}
    return {"ok": True, "message": "Image ejected.", "path": ap}


def find_broken_shortcuts(root: str, limit: int = 200) -> Dict[str, Any]:
    """34.6"""
    base = os.path.abspath(root)
    if not os.path.isdir(base):
        return {"ok": False, "error": "Folder not found.", "shortcuts": []}
    script = (
        f"$sh = New-Object -ComObject WScript.Shell; $out=@(); "
        f"Get-ChildItem -LiteralPath '{base.replace(chr(39), chr(39)+chr(39))}' -Filter *.lnk -Recurse -ErrorAction SilentlyContinue | "
        f"Select-Object -First {max(1, min(limit, 500))} | ForEach-Object {{ "
        f"$t=$sh.CreateShortcut($_.FullName).TargetPath; "
        f"if ($t -and -not (Test-Path -LiteralPath $t)) {{ $out += [pscustomobject]@{{ path=$_.FullName; target=$t }} }} }}; "
        f"if (-not $out) {{ '[]' }} else {{ $out | ConvertTo-Json -Compress }}"
    )
    try:
        proc = _ps(script, timeout=180)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "shortcuts": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    shortcuts = [
        {"path": str(r.get("path") or ""), "target": str(r.get("target") or "")}
        for r in rows if isinstance(r, dict)
    ]
    return {"ok": True, "shortcuts": shortcuts, "count": len(shortcuts)}


def file_lock_holders(path: str) -> Dict[str, Any]:
    """34.7 — reuse openfiles /query /fo csv if enabled; else handle from files_in_use module if present."""
    ap = os.path.abspath(path)
    if not os.path.exists(ap):
        return {"ok": False, "error": "Path not found.", "holders": []}
    try:
        from core.files_in_use import find_process_holders
        holders = find_process_holders(ap)
        return {"ok": True, "path": ap, "holders": holders}
    except Exception:
        pass
    try:
        proc = _run(["openfiles", "/query", "/fo", "csv", "/v"], timeout=60)
        text = proc.stdout or ""
        holders = [ln for ln in text.splitlines() if ap.lower() in ln.lower()]
        return {"ok": True, "path": ap, "holders": holders[:50], "detail": "Requires openfiles /local on if empty."}
    except Exception as e:
        return {"ok": False, "error": str(e), "holders": []}


def format_removable(drive: str, label: str = "DATA", confirmed: bool = False) -> Dict[str, Any]:
    """34.8 — removable only"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required. This erases the volume.", "needs_confirm": True}
    letter = str(drive or "").strip().upper().rstrip("\\/")
    if len(letter) == 1:
        letter = letter + ":"
    if not re.match(r"^[A-Z]:$", letter):
        return {"ok": False, "error": "That drive letter is not allowed."}
    system = (os.environ.get("SystemDrive") or "C:").upper()
    if letter == system:
        return {"ok": False, "error": "The system drive cannot be formatted."}
    # Verify removable via Win32_LogicalDisk DriveType 2
    script = (
        f"$d = Get-CimInstance Win32_LogicalDisk -Filter \"DeviceID='{letter}'\"; "
        "if (-not $d) { 'MISSING' } else { [string]$d.DriveType }"
    )
    try:
        check = _ps(script, timeout=30)
        dtype = (check.stdout or "").strip()
    except Exception as e:
        return {"ok": False, "error": str(e)}
    if dtype != "2":
        return {"ok": False, "error": "Only removable drives can be formatted here."}
    safe_label = re.sub(r"[^A-Za-z0-9]", "", label or "DATA")[:11] or "DATA"
    try:
        proc = _run(["format.com", letter, "/Q", "/Y", f"/V:{safe_label}"], timeout=600)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Format timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": f"Formatted {letter}." if proc.returncode == 0 else f"Format of {letter} failed.",
        "detail": ((proc.stdout or "") + (proc.stderr or ""))[-1500:],
    }


def read_permissions(path: str) -> Dict[str, Any]:
    """34.11"""
    ap = os.path.abspath(path)
    if not os.path.exists(ap):
        return {"ok": False, "error": "Path not found."}
    try:
        proc = _run(["icacls", ap], timeout=30)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Permissions read timed out."}
    return {"ok": True, "path": ap, "detail": (proc.stdout or "")[:3000]}


def preview_reg_file(path: str) -> Dict[str, Any]:
    """34.16 — preview only, import off"""
    ap = os.path.abspath(path)
    if not ap.lower().endswith(".reg") or not os.path.isfile(ap):
        return {"ok": False, "error": "Choose a .reg file."}
    try:
        with open(ap, "r", encoding="utf-16", errors="ignore") as handle:
            text = handle.read(20000)
    except Exception:
        with open(ap, "r", encoding="utf-8", errors="replace") as handle:
            text = handle.read(20000)
    return {"ok": True, "path": ap, "preview": text, "import_enabled": False, "message": "Preview only. Import stays off."}


def context_menu_third_party() -> Dict[str, Any]:
    """33.1 — list common shell extensions from registry (read-only first pass)."""
    script = (
        "$paths=@("
        "'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Shell Extensions\\Approved',"
        "'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Shell Extensions\\Approved'"
        "); $rows=@(); foreach($p in $paths){ if(Test-Path $p){ Get-ItemProperty $p | "
        "ForEach-Object { $_.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | "
        "ForEach-Object { $rows += [pscustomobject]@{ hive=$p; clsid=$_.Name; name=[string]$_.Value } } } } }; "
        "if(-not $rows){'[]'} else { $rows | Select-Object -First 200 | ConvertTo-Json -Compress }"
    )
    try:
        proc = _ps(script, timeout=60)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "items": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    items = [
        {"hive": str(r.get("hive") or ""), "clsid": str(r.get("clsid") or ""), "name": str(r.get("name") or "")}
        for r in rows if isinstance(r, dict)
    ]
    return {
        "ok": True,
        "items": items,
        "count": len(items),
        "message": "Read-only list. Disabling a third-party entry is a later reversible toggle.",
    }
