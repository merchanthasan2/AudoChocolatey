"""Wave 5 — files and admin helpers (tracker 34.x subset)."""
from __future__ import annotations

import hashlib
import os
import re
import shutil
import subprocess
import zipfile
from typing import Any, Dict, List, Optional

_SAFE_PATH_CHARS = re.compile(r"^[A-Za-z]:\\")


def _under_user_or_removable(path: str) -> bool:
    ap = os.path.abspath(path)
    home = os.path.abspath(os.path.expanduser("~"))
    if ap.lower().startswith(home.lower()):
        return True
    # Allow other fixed drives except forcing system root wipe elsewhere.
    return bool(_SAFE_PATH_CHARS.match(ap))


def create_zip(source_dir: str, zip_path: str) -> Dict[str, Any]:
    """34.1"""
    src = os.path.abspath(source_dir)
    out = os.path.abspath(zip_path)
    if not os.path.isdir(src):
        return {"ok": False, "error": "Source folder not found."}
    if not out.lower().endswith(".zip"):
        return {"ok": False, "error": "Output must be a .zip file."}
    if not _under_user_or_removable(src) or not _under_user_or_removable(out):
        return {"ok": False, "error": "Paths must be under your profile or a normal drive path."}
    try:
        with zipfile.ZipFile(out, "w", compression=zipfile.ZIP_DEFLATED) as zf:
            for root, _dirs, files in os.walk(src):
                for name in files:
                    full = os.path.join(root, name)
                    arc = os.path.relpath(full, src)
                    zf.write(full, arc)
    except OSError as e:
        return {"ok": False, "error": str(e)}
    return {"ok": True, "message": "ZIP created.", "path": out}


def extract_zip(zip_path: str, dest_dir: str, confirmed: bool = False) -> Dict[str, Any]:
    """34.1"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    zp = os.path.abspath(zip_path)
    dest = os.path.abspath(dest_dir)
    if not zp.lower().endswith(".zip") or not os.path.isfile(zp):
        return {"ok": False, "error": "ZIP file not found."}
    if not _under_user_or_removable(dest):
        return {"ok": False, "error": "Destination must be under your profile or a normal drive path."}
    try:
        os.makedirs(dest, exist_ok=True)
        with zipfile.ZipFile(zp, "r") as zf:
            for info in zf.infolist():
                # Zip-slip guard
                target = os.path.abspath(os.path.join(dest, info.filename))
                if not target.startswith(dest + os.sep) and target != dest:
                    return {"ok": False, "error": "ZIP contains an unsafe path."}
            zf.extractall(dest)
    except (OSError, zipfile.BadZipFile) as e:
        return {"ok": False, "error": str(e)}
    return {"ok": True, "message": "ZIP extracted.", "path": dest}


def file_checksum(path: str, algo: str = "sha256") -> Dict[str, Any]:
    """34.4"""
    ap = os.path.abspath(path)
    if not os.path.isfile(ap):
        return {"ok": False, "error": "File not found."}
    name = str(algo or "sha256").lower()
    if name not in ("sha256", "sha1", "md5"):
        return {"ok": False, "error": "Choose sha256, sha1, or md5."}
    h = hashlib.new(name)
    try:
        with open(ap, "rb") as handle:
            while True:
                chunk = handle.read(1024 * 1024)
                if not chunk:
                    break
                h.update(chunk)
    except OSError as e:
        return {"ok": False, "error": str(e)}
    return {"ok": True, "algo": name, "hex": h.hexdigest(), "path": ap}


def verify_checksum(path: str, expected_hex: str, algo: str = "sha256") -> Dict[str, Any]:
    """34.4"""
    got = file_checksum(path, algo=algo)
    if not got.get("ok"):
        return got
    exp = re.sub(r"[^0-9a-fA-F]", "", str(expected_hex or ""))
    match = got["hex"].lower() == exp.lower()
    return {"ok": True, "match": match, "hex": got["hex"], "expected": exp.lower(), "algo": got["algo"]}


def find_empty_folders(root: str, limit: int = 500) -> Dict[str, Any]:
    """34.5"""
    base = os.path.abspath(root)
    if not os.path.isdir(base):
        return {"ok": False, "error": "Folder not found.", "folders": []}
    if not _under_user_or_removable(base):
        return {"ok": False, "error": "Root must be under your profile or a normal drive path.", "folders": []}
    found: List[str] = []
    capped = max(1, min(int(limit), 2000))
    for dirpath, dirnames, filenames in os.walk(base):
        if not dirnames and not filenames:
            found.append(dirpath)
            if len(found) >= capped:
                break
    return {"ok": True, "folders": found, "count": len(found)}


def process_list(limit: int = 100) -> Dict[str, Any]:
    """34.12 — read-only process snapshot."""
    if os.name != "nt":
        return {"ok": False, "error": "Windows only.", "processes": []}
    flags = 0x08000000
    startupinfo = subprocess.STARTUPINFO()
    startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    n = max(1, min(int(limit), 300))
    script = (
        f"Get-Process | Sort-Object CPU -Descending | Select-Object -First {n} "
        "Id, ProcessName, CPU, WorkingSet |"
        " ConvertTo-Json -Compress"
    )
    proc = subprocess.run(
        ["powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=60,
        startupinfo=startupinfo,
        creationflags=flags,
    )
    try:
        import json

        data = json.loads((proc.stdout or "").strip() or "[]")
    except Exception as e:
        return {"ok": False, "error": str(e), "processes": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    processes = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        processes.append(
            {
                "pid": str(row.get("Id") or ""),
                "name": str(row.get("ProcessName") or ""),
                "cpu": str(row.get("CPU") or ""),
                "working_set": str(row.get("WorkingSet") or ""),
            }
        )
    return {"ok": True, "processes": processes, "count": len(processes)}


def end_process(pid: int, confirmed: bool = False) -> Dict[str, Any]:
    """34.13"""
    if not confirmed:
        return {"ok": False, "error": "Confirm required.", "needs_confirm": True}
    try:
        p = int(pid)
    except (TypeError, ValueError):
        return {"ok": False, "error": "That process id is not allowed."}
    if p <= 0 or p >= 2**31:
        return {"ok": False, "error": "That process id is not allowed."}
    # Refuse ending critical PIDs roughly
    if p in (0, 4):
        return {"ok": False, "error": "That process cannot be ended."}
    flags = 0x08000000
    try:
        proc = subprocess.run(
            ["taskkill", "/PID", str(p), "/F"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=30,
            creationflags=flags,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "End process timed out."}
    return {
        "ok": proc.returncode == 0,
        "message": f"Ended process {p}." if proc.returncode == 0 else f"Could not end process {p}.",
        "detail": ((proc.stdout or "") + (proc.stderr or "")).strip()[:1000],
    }


def verify_file_signature(path: str) -> Dict[str, Any]:
    """34.9 — Authenticode status via Get-AuthenticodeSignature."""
    ap = os.path.abspath(path)
    if not os.path.isfile(ap):
        return {"ok": False, "error": "File not found."}
    escaped = ap.replace("'", "''")
    script = (
        f"$s = Get-AuthenticodeSignature -FilePath '{escaped}'; "
        "$o = [pscustomobject]@{ Status=[string]$s.Status; Signer=$s.SignerCertificate.Subject; "
        "Issuer=$s.SignerCertificate.Issuer }; $o | ConvertTo-Json -Compress"
    )
    flags = 0x08000000
    startupinfo = subprocess.STARTUPINFO()
    startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    proc = subprocess.run(
        ["powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=60,
        startupinfo=startupinfo,
        creationflags=flags,
    )
    try:
        import json

        data = json.loads((proc.stdout or "").strip() or "{}")
    except Exception as e:
        return {"ok": False, "error": str(e)}
    return {
        "ok": True,
        "status": str(data.get("Status") or ""),
        "signer": str(data.get("Signer") or ""),
        "issuer": str(data.get("Issuer") or ""),
        "path": ap,
    }
