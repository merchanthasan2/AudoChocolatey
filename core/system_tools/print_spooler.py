"""Print queue repair for Wave 2 tracker 31.10."""
from __future__ import annotations

import json
import os
import re
import subprocess
import time
from typing import Any, Dict, List

_SAFE_PRINTER = re.compile(r"^[^\\/:\*\?\"<>|]{1,220}$")
_SAFE_JOB_ID = re.compile(r"^\d{1,10}$")


def _ps_run(command: str, timeout: int = 90) -> subprocess.CompletedProcess:
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


def list_print_jobs() -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Print queue tools are only available on Windows.", "jobs": []}
    script = (
        "$ErrorActionPreference='Stop';"
        "$rows = @();"
        "Get-Printer -ErrorAction SilentlyContinue | ForEach-Object {"
        "  $p = $_.Name;"
        "  Get-PrintJob -PrinterName $p -ErrorAction SilentlyContinue | ForEach-Object {"
        "    $rows += [pscustomobject]@{"
        "      printer=$p; id=$_.Id; document=$_.Document; user=$_.UserName; status=\"$($_.JobStatus)\";"
        "      size_bytes=[int64]($_.Size); submitted=\"$($_.SubmittedTime)\""
        "    }"
        "  }"
        "};"
        " if (-not $rows) { '[]' } else { $rows | ConvertTo-Json -Compress -Depth 3 }"
    )
    try:
        proc = _ps_run(script, timeout=90)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Listing print jobs timed out.", "jobs": []}
    except Exception as e:
        return {"ok": False, "error": str(e), "jobs": []}
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        return {"ok": False, "error": err or f"List failed (exit {proc.returncode}).", "jobs": []}
    raw = (proc.stdout or "").strip()
    try:
        data = json.loads(raw) if raw else []
    except json.JSONDecodeError:
        return {"ok": False, "error": "Could not read the print job list.", "jobs": []}
    rows = data if isinstance(data, list) else ([data] if isinstance(data, dict) else [])
    jobs: List[Dict[str, Any]] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        printer = str(row.get("printer") or "").strip()
        job_id = str(row.get("id") or "").strip()
        if not printer or not _SAFE_PRINTER.match(printer):
            continue
        if not job_id or not _SAFE_JOB_ID.match(job_id):
            continue
        jobs.append(
            {
                "printer": printer,
                "id": job_id,
                "document": str(row.get("document") or "").strip(),
                "user": str(row.get("user") or "").strip(),
                "status": str(row.get("status") or "").strip(),
                "size_bytes": int(row.get("size_bytes") or 0),
                "submitted": str(row.get("submitted") or "").strip(),
            }
        )
    return {"ok": True, "jobs": jobs, "count": len(jobs)}


def cancel_print_job(printer: str, job_id: str, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"ok": False, "error": "Cancel requires explicit confirmation.", "needs_confirm": True}
    if os.name != "nt":
        return {"ok": False, "error": "Print queue tools are only available on Windows."}
    printer = str(printer or "").strip()
    job_id = str(job_id or "").strip()
    if not printer or not _SAFE_PRINTER.match(printer):
        return {"ok": False, "error": "That printer name is not allowed."}
    if not job_id or not _SAFE_JOB_ID.match(job_id):
        return {"ok": False, "error": "That job id is not allowed."}
    listed = list_print_jobs()
    if not listed.get("ok"):
        return {"ok": False, "error": listed.get("error") or "Could not verify print jobs."}
    match = next(
        (
            j
            for j in (listed.get("jobs") or [])
            if j.get("printer") == printer and str(j.get("id")) == job_id
        ),
        None,
    )
    if not match:
        return {"ok": False, "error": "That print job is no longer in the queue."}
    escaped = printer.replace("'", "''")
    script = (
        f"$ErrorActionPreference='Stop'; "
        f"Remove-PrintJob -PrinterName '{escaped}' -ID {job_id}"
    )
    try:
        proc = _ps_run(script, timeout=60)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Cancel timed out."}
    except Exception as e:
        return {"ok": False, "error": str(e)}
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        return {"ok": False, "error": err or f"Cancel failed (exit {proc.returncode})."}
    return {
        "ok": True,
        "message": f"Cancelled job {job_id} on {printer}.",
        "printer": printer,
        "id": job_id,
    }


def clear_print_queue_and_restart_spooler(confirmed: bool = False) -> Dict[str, Any]:
    """Machine-wide queue clear: stop spooler, clear PRINTERS folder, start spooler."""
    if not confirmed:
        return {
            "ok": False,
            "error": "Machine-wide clear requires explicit confirmation.",
            "needs_confirm": True,
        }
    if os.name != "nt":
        return {"ok": False, "error": "Print queue tools are only available on Windows."}
    script = (
        "$ErrorActionPreference='Stop';"
        "Stop-Service -Name Spooler -Force;"
        "$dir = Join-Path $env:SystemRoot 'System32\\spool\\PRINTERS';"
        " if (Test-Path -LiteralPath $dir) {"
        "   Get-ChildItem -LiteralPath $dir -Force -ErrorAction SilentlyContinue |"
        "     Remove-Item -Force -Recurse -ErrorAction SilentlyContinue"
        " };"
        "Start-Service -Name Spooler;"
        "(Get-Service -Name Spooler).Status"
    )
    try:
        proc = _ps_run(script, timeout=120)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Spooler repair timed out."}
    except Exception as e:
        return {"ok": False, "error": str(e)}
    status = (proc.stdout or "").strip()
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        return {
            "ok": False,
            "error": err or f"Spooler repair failed (exit {proc.returncode}).",
            "spooler_status": status,
        }
    time.sleep(0.2)
    return {
        "ok": True,
        "message": "Print queue cleared and print spooler restarted.",
        "spooler_status": status or "Running",
    }
