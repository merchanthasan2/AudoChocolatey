"""Large file scan and removal (recycle bin or confirmed permanent delete)."""

from __future__ import annotations

import hashlib
import json
import os
import secrets
import subprocess
import threading
import time
import uuid
from dataclasses import dataclass, field
from typing import Any, Callable, Dict, List, Optional, Set, Tuple

from core.system_tools.safety import is_reparse_point

SCAN_BUDGET_S = 180
TOKEN_TTL_S = 300

_jobs_lock = threading.Lock()
_jobs: Dict[str, Dict[str, Any]] = {}
_last_scan_paths: Dict[str, Dict[str, Any]] = {}
_confirm_tokens: Dict[str, Dict[str, Any]] = {}


def _quantum_dir() -> str:
    root = os.path.join(os.environ.get("LOCALAPPDATA", ""), "QuantumUpdater")
    os.makedirs(root, exist_ok=True)
    return root


def _history_path() -> str:
    return os.path.join(_quantum_dir(), "history.jsonl")


def human_size(size_bytes: int) -> str:
    if size_bytes < 1024:
        return f"{size_bytes} B"
    if size_bytes < 1024 * 1024:
        return f"{size_bytes / 1024:.1f} KB"
    if size_bytes < 1024 * 1024 * 1024:
        return f"{size_bytes / (1024 * 1024):.1f} MB"
    return f"{size_bytes / (1024 * 1024 * 1024):.2f} GB"


def large_files_deny_roots() -> List[str]:
    home = os.path.expanduser("~")
    system_root = os.environ.get("SystemRoot", r"C:\Windows")
    program_data = os.environ.get("ProgramData", r"C:\ProgramData")
    local = os.environ.get("LOCALAPPDATA", os.path.join(home, "AppData", "Local"))
    roots = [
        os.path.normcase(os.path.abspath(system_root)),
        os.path.normcase(os.path.abspath(os.environ.get("ProgramFiles", r"C:\Program Files"))),
        os.path.normcase(
            os.path.abspath(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"))
        ),
        os.path.normcase(os.path.abspath(program_data)),
        os.path.normcase(os.path.abspath(os.path.join(local, "Packages"))),
    ]
    for letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ":
        drive = f"{letter}:\\"
        if os.path.isdir(drive):
            roots.append(os.path.normcase(os.path.join(drive, "$Recycle.Bin")))
            roots.append(os.path.normcase(os.path.join(drive, "System Volume Information")))
    return roots


def _is_denied(path: str, deny: List[str]) -> bool:
    p = os.path.normcase(os.path.abspath(path))
    for root in deny:
        if p == root or p.startswith(root + os.sep):
            return True
    return False


def _log_permanent_delete(paths: List[str], total_bytes: int) -> None:
    path = _history_path()
    record = {
        "ts": time.time(),
        "action": "large_files_permanent_delete",
        "paths": paths,
        "total_bytes": total_bytes,
    }
    try:
        with open(path, "a", encoding="utf-8") as fh:
            fh.write(json.dumps(record) + "\n")
    except OSError:
        pass


def _scan_worker(
    job_id: str,
    root: str,
    min_bytes: int,
    older_than_days: int,
    budget_s: float,
) -> None:
    deny = large_files_deny_roots()
    cutoff = time.time() - (older_than_days * 86400) if older_than_days > 0 else None
    started = time.perf_counter()
    files_scanned = 0
    bytes_scanned = 0
    current_folder = root
    hits: List[Dict[str, Any]] = []
    partial = False

    def should_stop() -> bool:
        with _jobs_lock:
            job = _jobs.get(job_id)
            if job and job.get("cancel"):
                return True
        return (time.perf_counter() - started) >= budget_s

    stack = [root]
    while stack:
        if should_stop():
            partial = True
            break
        folder = stack.pop()
        current_folder = folder
        if _is_denied(folder, deny):
            continue
        try:
            with os.scandir(folder) as it:
                for entry in it:
                    if should_stop():
                        partial = True
                        break
                    try:
                        if entry.is_symlink() or is_reparse_point(entry.path):
                            continue
                        if entry.is_dir(follow_symlinks=False):
                            if not _is_denied(entry.path, deny):
                                stack.append(entry.path)
                            continue
                        if not entry.is_file(follow_symlinks=False):
                            continue
                        st = entry.stat(follow_symlinks=False)
                        files_scanned += 1
                        bytes_scanned += st.st_size
                        if st.st_size < min_bytes:
                            continue
                        if cutoff is not None and st.st_mtime > cutoff:
                            continue
                        hits.append(
                            {
                                "path": entry.path,
                                "size_bytes": st.st_size,
                                "size_str": human_size(st.st_size),
                                "modified": st.st_mtime,
                            }
                        )
                    except OSError:
                        continue
        except OSError:
            continue
        with _jobs_lock:
            if job_id in _jobs:
                _jobs[job_id]["progress"] = {
                    "files_scanned": files_scanned,
                    "bytes_scanned": bytes_scanned,
                    "current_folder": current_folder,
                }
    hits.sort(key=lambda x: x["size_bytes"], reverse=True)
    path_index = {h["path"]: h for h in hits}
    with _jobs_lock:
        _jobs[job_id].update(
            {
                "status": "done",
                "partial": partial,
                "results": hits,
                "ended_at": time.time(),
                "progress": {
                    "files_scanned": files_scanned,
                    "bytes_scanned": bytes_scanned,
                    "current_folder": current_folder,
                },
            }
        )
        _last_scan_paths[job_id] = path_index


def scan_start(
    root: str = "",
    min_mb: float = 500,
    older_than_days: int = 0,
    budget_s: float = SCAN_BUDGET_S,
) -> str:
    if not root:
        root = os.path.expanduser("~")
    root = os.path.abspath(root)
    if not os.path.isdir(root):
        raise ValueError("Scan root is not a folder.")
    job_id = uuid.uuid4().hex
    min_bytes = int(float(min_mb) * 1024 * 1024)
    with _jobs_lock:
        _jobs[job_id] = {
            "status": "running",
            "root": root,
            "min_bytes": min_bytes,
            "older_than_days": older_than_days,
            "started_at": time.time(),
            "budget_s": float(budget_s),
            "cancel": False,
            "partial": False,
            "results": [],
            "progress": {"files_scanned": 0, "bytes_scanned": 0, "current_folder": root},
        }
    threading.Thread(
        target=_scan_worker,
        args=(job_id, root, min_bytes, older_than_days, budget_s),
        daemon=True,
    ).start()
    return job_id


def scan_status(job_id: str) -> Dict[str, Any]:
    """Status for the UI. No invented mid-scan percent — only files/folder counts while running."""
    with _jobs_lock:
        job = _jobs.get(job_id)
        if not job:
            return {"found": False}
        status = job.get("status", "unknown")
        out: Dict[str, Any] = {
            "found": True,
            "status": status,
            "partial": bool(job.get("partial")),
            "root": job.get("root", ""),
            "progress": dict(job.get("progress") or {}),
            "results": list(job.get("results") or []),
            "count": len(job.get("results") or []),
        }
        # Honest percent only when finished. While running, UI must use progress.files_scanned /
        # current_folder (or indeterminate) — never elapsed/budget as a fake bar.
        if status == "done":
            out["percent"] = 100
        return out


def scan_cancel(job_id: str) -> Dict[str, Any]:
    with _jobs_lock:
        job = _jobs.get(job_id)
        if not job:
            return {"success": False, "message": "Job not found."}
        job["cancel"] = True
    return {"success": True}


def _paths_allowed(paths: List[str], job_hint: Optional[str] = None) -> Tuple[bool, str, Dict[str, Any]]:
    if not paths:
        return False, "No paths given.", {}
    deny = large_files_deny_roots()
    allowed_index: Dict[str, Dict[str, Any]] = {}
    if job_hint and job_hint in _last_scan_paths:
        allowed_index = _last_scan_paths[job_hint]
    else:
        for job_paths in _last_scan_paths.values():
            allowed_index.update(job_paths)
    allowed_norm = {os.path.normcase(os.path.abspath(k)) for k in allowed_index}
    for p in paths:
        if _is_denied(p, deny):
            return False, "That location is protected.", {}
        real = os.path.normcase(os.path.abspath(p))
        if real not in allowed_norm:
            return False, "Path was not part of the latest scan results.", {}
    return True, "ok", allowed_index


def _path_key(paths: List[str]) -> str:
    norm = sorted(os.path.normcase(os.path.abspath(p)) for p in paths)
    return hashlib.sha256("\n".join(norm).encode("utf-8")).hexdigest()


def move_to_recycle_bin(paths: List[str], job_id: str = "") -> Dict[str, Any]:
    if os.name != "nt":
        return {"success": False, "message": "Not supported on this platform."}
    ok, msg, _ = _paths_allowed(paths, job_hint=job_id or None)
    if not ok:
        return {"success": False, "message": msg}
    existing = [p for p in paths if os.path.lexists(p)]
    if not existing:
        return {"success": False, "message": "No matching files found."}
    try:
        import ctypes
        from ctypes import wintypes

        FO_DELETE = 0x0003
        FOF_ALLOWUNDO = 0x0040
        FOF_NOCONFIRMATION = 0x0010
        FOF_SILENT = 0x0004
        FOF_NOERRORUI = 0x0400

        class SHFILEOPSTRUCTW(ctypes.Structure):
            _fields_ = [
                ("hwnd", wintypes.HWND),
                ("wFunc", wintypes.UINT),
                ("pFrom", wintypes.LPCWSTR),
                ("pTo", wintypes.LPCWSTR),
                ("fFlags", wintypes.WORD),
                ("fAnyOperationsAborted", wintypes.BOOL),
                ("hNameMappings", wintypes.LPVOID),
                ("lpszProgressTitle", wintypes.LPCWSTR),
            ]

        joined = "\0".join(os.path.abspath(p) for p in existing) + "\0\0"
        op = SHFILEOPSTRUCTW()
        op.hwnd = None
        op.wFunc = FO_DELETE
        op.pFrom = joined
        op.pTo = None
        op.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
        code = ctypes.windll.shell32.SHFileOperationW(ctypes.byref(op))
        if code != 0 or op.fAnyOperationsAborted:
            return {"success": False, "message": "Could not move files to the Recycle Bin."}
        return {"success": True, "moved": len(existing)}
    except Exception as exc:
        return {"success": False, "message": str(exc)}


def confirm_delete(paths: List[str], job_id: str = "") -> Dict[str, Any]:
    ok, msg, index = _paths_allowed(paths, job_hint=job_id or None)
    if not ok:
        return {"success": False, "message": msg}
    rows = []
    total = 0
    for p in paths:
        if os.path.isfile(p):
            try:
                sz = os.path.getsize(p)
            except OSError:
                sz = index.get(p, {}).get("size_bytes", 0)
            total += sz
            rows.append({"path": p, "size_bytes": sz, "size_str": human_size(sz)})
    token = secrets.token_urlsafe(24)
    key = _path_key(paths)
    _confirm_tokens[token] = {
        "paths": list(paths),
        "path_key": key,
        "expires_at": time.time() + TOKEN_TTL_S,
        "total_bytes": total,
        "files": rows,
    }
    return {
        "success": True,
        "token": token,
        "expires_at": _confirm_tokens[token]["expires_at"],
        "files": rows,
        "total_bytes": total,
        "total_size_str": human_size(total),
        "message": "Permanent delete cannot be undone.",
    }


def delete_permanently(paths: List[str], token: str) -> Dict[str, Any]:
    entry = _confirm_tokens.get(token)
    if not entry:
        return {"success": False, "message": "Confirmation expired or invalid."}
    if time.time() > float(entry.get("expires_at", 0)):
        _confirm_tokens.pop(token, None)
        return {"success": False, "message": "Confirmation expired or invalid."}
    expected_paths = entry.get("paths") or []
    if _path_key(paths) != entry.get("path_key") or sorted(paths) != sorted(expected_paths):
        return {"success": False, "message": "File list does not match the confirmation."}
    ok, msg, _ = _paths_allowed(paths)
    if not ok:
        return {"success": False, "message": msg}
    removed = 0
    total = 0
    for p in paths:
        try:
            if os.path.isfile(p):
                sz = os.path.getsize(p)
                os.remove(p)
                removed += 1
                total += sz
        except OSError:
            continue
    _confirm_tokens.pop(token, None)
    if removed:
        _log_permanent_delete(paths, total)
    return {"success": removed > 0, "removed": removed, "total_bytes": total}


def reveal_in_explorer(path: str) -> Dict[str, Any]:
    if not path or not os.path.lexists(path):
        return {"success": False, "message": "Path not found."}
    target = os.path.abspath(path)
    try:
        if os.path.isfile(target):
            subprocess.Popen(["explorer.exe", f"/select,{target}"], shell=False)
        else:
            subprocess.Popen(["explorer.exe", target], shell=False)
        return {"success": True, "path": target}
    except OSError as exc:
        return {"success": False, "message": str(exc)}


def register_paths_from_scan(job_id: str, paths: Dict[str, Dict[str, Any]]) -> None:
    """Test helper to seed last-scan paths without a full job."""
    _last_scan_paths[job_id] = paths
