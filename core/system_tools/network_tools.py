"""Wave 4 — network and power helpers (tracker 33.x subset)."""
from __future__ import annotations

import json
import os
import re
import shutil
import socket
import subprocess
from typing import Any, Dict, List

_SAFE_HOST = re.compile(r"^[A-Za-z0-9._:-]{1,253}$")
_SAFE_ADAPTER = re.compile(r"^[^\\/:\*\?\"<>|]{1,200}$")


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


def _run(args: List[str], timeout: int = 60) -> subprocess.CompletedProcess:
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


def flush_dns(confirmed: bool = False) -> Dict[str, Any]:
    """33.5"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    if os.name != "nt":
        return {"ok": False, "error": "Windows only."}
    try:
        proc = _run(["ipconfig", "/flushdns"], timeout=30)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Flush DNS timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": "DNS resolver cache flushed." if proc.returncode == 0 else "Flush DNS failed.",
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }


def reset_winsock(confirmed: bool = False) -> Dict[str, Any]:
    """33.6 — requires restart; warn in message."""
    if not confirmed:
        return {"ok": False, "error": "Confirm required. VPN may break until restart.", "needs_confirm": True}
    try:
        proc = _run(["netsh", "winsock", "reset"], timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Winsock reset timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": "Winsock reset requested. Restart this PC to finish. VPN apps may need setup again.",
        "restart_required": True,
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }


def renew_dhcp(adapter: str = "", confirmed: bool = False) -> Dict[str, Any]:
    """33.8"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    try:
        release = _run(["ipconfig", "/release"], timeout=60)
        renew = _run(["ipconfig", "/renew"], timeout=90)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "DHCP renew timed out."}
    return {
        "ok": renew.returncode == 0,
        "message": "DHCP lease renewed." if renew.returncode == 0 else "DHCP renew failed.",
        "detail": ((release.stdout or "") + (renew.stdout or "")).strip()[:1500],
        "adapter": adapter,
    }


def list_power_plans() -> Dict[str, Any]:
    """33.4"""
    if os.name != "nt":
        return {"ok": False, "error": "Windows only.", "plans": []}
    try:
        proc = _run(["powercfg", "/list"], timeout=30)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Listing power plans timed out.", "plans": []}
    plans = []
    active = ""
    for line in (proc.stdout or "").splitlines():
        m = re.search(r"([0-9a-fA-F-]{36})\s+\((.+)\)(\s+\*)?", line)
        if not m:
            continue
        guid, name, star = m.group(1), m.group(2).strip(), m.group(3)
        plans.append({"guid": guid, "name": name, "active": bool(star and "*" in star)})
        if star and "*" in star:
            active = guid
    return {"ok": True, "plans": plans, "active": active}


def set_power_plan(guid: str, confirmed: bool = False) -> Dict[str, Any]:
    """33.4"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    g = str(guid or "").strip()
    if not re.match(r"^[0-9a-fA-F-]{36}$", g):
        return {"ok": False, "error": "That power plan id is not allowed."}
    listed = list_power_plans()
    known = {p["guid"].lower() for p in listed.get("plans") or []}
    if g.lower() not in known:
        return {"ok": False, "error": "That power plan is not on this PC."}
    try:
        proc = _run(["powercfg", "/setactive", g], timeout=30)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Setting power plan timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": "Power plan selected." if proc.returncode == 0 else "Could not set power plan.",
        "guid": g,
    }


def test_tcp_port(host: str, port: int, timeout_s: float = 3.0) -> Dict[str, Any]:
    """33.13"""
    host = str(host or "").strip()
    if not host or not _SAFE_HOST.match(host):
        return {"ok": False, "error": "That host is not allowed."}
    try:
        p = int(port)
    except (TypeError, ValueError):
        return {"ok": False, "error": "That port is not allowed."}
    if p < 1 or p > 65535:
        return {"ok": False, "error": "That port is not allowed."}
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(max(0.5, min(float(timeout_s), 10.0)))
    try:
        sock.connect((host, p))
        open_ = True
    except OSError:
        open_ = False
    finally:
        sock.close()
    return {"ok": True, "host": host, "port": p, "open": open_, "message": "Open" if open_ else "Closed or filtered"}


def dns_lookup(name: str) -> Dict[str, Any]:
    """33.14"""
    name = str(name or "").strip()
    if not name or not _SAFE_HOST.match(name):
        return {"ok": False, "error": "That name is not allowed."}
    try:
        infos = socket.getaddrinfo(name, None)
        addrs = sorted({item[4][0] for item in infos})
    except OSError as e:
        return {"ok": False, "error": str(e)}
    return {"ok": True, "name": name, "addresses": addrs}


def ping_host(host: str, count: int = 4) -> Dict[str, Any]:
    """33.12 — latency sample via ping."""
    host = str(host or "").strip()
    if not host or not _SAFE_HOST.match(host):
        return {"ok": False, "error": "That host is not allowed."}
    n = max(1, min(int(count), 10))
    try:
        proc = _run(["ping", "-n", str(n), host], timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Ping timed out."}
    text = (proc.stdout or "") + (proc.stderr or "")
    lost_m = re.search(r"Lost\s*=\s*(\d+)", text, re.I)
    avg_m = re.search(r"Average\s*=\s*(\d+)ms", text, re.I)
    return {
        "ok": proc.returncode == 0,
        "host": host,
        "lost": int(lost_m.group(1)) if lost_m else None,
        "avg_ms": int(avg_m.group(1)) if avg_m else None,
        "detail": text[-1500:],
    }


def traceroute(host: str) -> Dict[str, Any]:
    """33.12 — route trace."""
    host = str(host or "").strip()
    if not host or not _SAFE_HOST.match(host):
        return {"ok": False, "error": "That host is not allowed."}
    try:
        proc = _run(["tracert", "-d", "-h", "15", host], timeout=120)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Trace timed out."}
    return {
        "ok": proc.returncode == 0,
        "host": host,
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[-4000:],
    }


def connection_inventory() -> Dict[str, Any]:
    """33.11 — listening ports read-only."""
    if os.name != "nt":
        return {"ok": False, "error": "Windows only.", "listeners": []}
    script = (
        "$ErrorActionPreference='SilentlyContinue';"
        "$rows = Get-NetTCPConnection -State Listen |"
        " Select-Object -First 200 LocalAddress, LocalPort, OwningProcess;"
        " if (-not $rows) { '[]' } else { $rows | ConvertTo-Json -Compress }"
    )
    try:
        proc = _ps(script, timeout=60)
        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "listeners": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    listeners = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        listeners.append(
            {
                "address": str(row.get("LocalAddress") or ""),
                "port": str(row.get("LocalPort") or ""),
                "pid": str(row.get("OwningProcess") or ""),
            }
        )
    return {"ok": True, "listeners": listeners, "count": len(listeners)}


def winhttp_proxy_status() -> Dict[str, Any]:
    """33.17 — read WinHTTP proxy."""
    try:
        proc = _run(["netsh", "winhttp", "show", "proxy"], timeout=30)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Proxy status timed out."}
    text = ((proc.stdout or "") + (proc.stderr or "")).strip()
    return {"ok": True, "detail": text[:2000]}


def winhttp_proxy_reset(confirmed: bool = False) -> Dict[str, Any]:
    """33.17"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    try:
        proc = _run(["netsh", "winhttp", "reset", "proxy"], timeout=30)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Proxy reset timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": "WinHTTP proxy reset." if proc.returncode == 0 else "Proxy reset failed.",
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }


def hosts_file_preview() -> Dict[str, Any]:
    """33.10 — show hosts path and non-comment lines."""
    path = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32", "drivers", "etc", "hosts")
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            lines = handle.read().splitlines()
    except OSError as e:
        return {"ok": False, "error": str(e)}
    custom = [ln for ln in lines if ln.strip() and not ln.strip().startswith("#")]
    return {"ok": True, "path": path, "custom_lines": custom[:200], "count": len(custom)}


def restore_default_hosts(confirmed: bool = False) -> Dict[str, Any]:
    """33.10 — backup then write default hosts stub."""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    path = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32", "drivers", "etc", "hosts")
    backup = path + ".quantum-updater.bak"
    default = (
        "# Copyright (c) Microsoft Corp.\n"
        "#\n"
        "# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.\n"
        "#\n"
        "127.0.0.1       localhost\n"
        "::1             localhost\n"
    )
    try:
        shutil.copy2(path, backup)
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(default)
    except OSError as e:
        return {"ok": False, "error": str(e)}
    return {"ok": True, "message": "Hosts file restored to defaults.", "backup": backup}


def clock_sync_status() -> Dict[str, Any]:
    """33.16"""
    try:
        proc = _run(["w32tm", "/query", "/status"], timeout=30)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Clock status timed out."}
    return {
        "ok": proc.returncode == 0,
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:2000],
    }


def clock_sync_now(confirmed: bool = False) -> Dict[str, Any]:
    """33.16"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    try:
        proc = _run(["w32tm", "/resync", "/force"], timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Clock sync timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": "Clock sync requested." if proc.returncode == 0 else "Clock sync failed.",
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }
