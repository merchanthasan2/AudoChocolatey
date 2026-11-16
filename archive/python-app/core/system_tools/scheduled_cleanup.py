"""Scheduled cleanup via Task Scheduler and saved category list."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from typing import Any, Dict, List, Optional

TASK_NAME = "QuantumUpdaterCleanup"
ALLOWED_CATEGORIES = frozenset({"user_temp", "browser_caches", "recycle_bin"})
ALLOWED_CADENCE = frozenset({"daily", "weekly"})

_test_schedule_path: Optional[str] = None
_test_task_runner: Optional[Any] = None


def configure_test_hooks(*, schedule_path: Optional[str] = None, task_runner: Optional[Any] = None) -> None:
    global _test_schedule_path, _test_task_runner
    _test_schedule_path = schedule_path
    _test_task_runner = task_runner


def reset_test_hooks() -> None:
    configure_test_hooks(schedule_path=None, task_runner=None)


def _quantum_dir() -> str:
    root = os.path.join(os.environ.get("LOCALAPPDATA", ""), "QuantumUpdater")
    os.makedirs(root, exist_ok=True)
    return root


def _schedule_path() -> str:
    if _test_schedule_path:
        return _test_schedule_path
    return os.path.join(_quantum_dir(), "cleanup_schedule.json")


def _read_schedule_file() -> Optional[Dict[str, Any]]:
    path = _schedule_path()
    if not os.path.isfile(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        if not isinstance(data, dict):
            return None
        return data
    except (OSError, json.JSONDecodeError, TypeError):
        return None


def _write_schedule_file(data: Dict[str, Any]) -> None:
    path = _schedule_path()
    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2)
    os.replace(tmp, path)


def _normalize_categories(category_ids: List[str]) -> List[str]:
    out: List[str] = []
    for raw in category_ids or []:
        cid = str(raw).strip()
        if cid in ALLOWED_CATEGORIES and cid not in out:
            out.append(cid)
    return out


def list_schedule() -> Dict[str, Any]:
    data = _read_schedule_file()
    if not data:
        return {"configured": False, "categories": [], "cadence": None, "task_registered": _task_exists()}
    categories = _normalize_categories(list(data.get("categories") or []))
    cadence = str(data.get("cadence") or "").strip().lower()
    if cadence not in ALLOWED_CADENCE:
        cadence = None
    return {
        "configured": bool(categories and cadence),
        "categories": categories,
        "cadence": cadence,
        "updated_at": data.get("updated_at"),
        "task_registered": _task_exists(),
        "task_name": TASK_NAME,
    }


def save_schedule(category_ids: List[str], cadence: str) -> Dict[str, Any]:
    categories = _normalize_categories(category_ids)
    cad = str(cadence or "").strip().lower()
    if not categories:
        return {"success": False, "message": "Choose at least one cleanup category."}
    if cad not in ALLOWED_CADENCE:
        return {"success": False, "message": "Cadence must be daily or weekly."}
    payload = {
        "categories": categories,
        "cadence": cad,
        "updated_at": time.time(),
    }
    _write_schedule_file(payload)
    task = _upsert_task(cad)
    if not task.get("ok"):
        return {"success": False, "message": task.get("error", "Could not register the scheduled task.")}
    return {
        "success": True,
        "categories": categories,
        "cadence": cad,
        "task_name": TASK_NAME,
        "message": "Scheduled cleanup saved.",
    }


def remove_schedule() -> Dict[str, Any]:
    path = _schedule_path()
    if os.path.isfile(path):
        try:
            os.remove(path)
        except OSError:
            return {"success": False, "message": "Could not remove the saved schedule."}
    task = _delete_task()
    if not task.get("ok"):
        return {"success": False, "message": task.get("error", "Could not remove the scheduled task.")}
    return {"success": True, "message": "Scheduled cleanup removed."}


def scheduled_cleanup_command() -> List[str]:
    if getattr(sys, "frozen", False):
        exe = os.path.abspath(sys.executable)
        return [exe, "--scheduled-cleanup", "--no-elevate"]
    script = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "main.py"))
    return [sys.executable, script, "--scheduled-cleanup", "--no-elevate"]


def _quote_cmd(argv: List[str]) -> str:
    parts = []
    for arg in argv:
        if not arg:
            parts.append('""')
        elif any(ch in arg for ch in ' \t"'):
            parts.append('"' + arg.replace('"', '\\"') + '"')
        else:
            parts.append(arg)
    return " ".join(parts)


def _run_schtasks(args: List[str]) -> subprocess.CompletedProcess:
    if _test_task_runner is not None:
        return _test_task_runner(args)
    return subprocess.run(
        ["schtasks.exe", *args],
        capture_output=True,
        text=True,
        timeout=60,
        check=False,
    )


def _task_exists() -> bool:
    if os.name != "nt":
        return False
    proc = _run_schtasks(["/query", "/tn", TASK_NAME])
    return proc.returncode == 0


def _delete_task() -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": True}
    if not _task_exists():
        return {"ok": True}
    proc = _run_schtasks(["/delete", "/tn", TASK_NAME, "/f"])
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        return {"ok": False, "error": err or "schtasks delete failed."}
    return {"ok": True}


def _upsert_task(cadence: str) -> Dict[str, Any]:
    if os.name != "nt":
        return {"ok": False, "error": "Task Scheduler is only available on Windows."}
    del_result = _delete_task()
    if not del_result.get("ok"):
        return del_result
    tr = _quote_cmd(scheduled_cleanup_command())
    args = [
        "/create",
        "/tn",
        TASK_NAME,
        "/tr",
        tr,
        "/sc",
        cadence,
        "/st",
        "03:00",
        "/f",
    ]
    if cadence == "weekly":
        args.extend(["/d", "MON"])
    proc = _run_schtasks(args)
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        return {"ok": False, "error": err or "schtasks create failed."}
    return {"ok": True}


def run_scheduled_cleanup() -> Dict[str, Any]:
    data = _read_schedule_file()
    if not data:
        return {"success": False, "message": "No cleanup schedule is saved."}
    categories = _normalize_categories(list(data.get("categories") or []))
    if not categories:
        return {"success": False, "message": "Saved schedule has no categories."}
    from core.system_tools.cleanup import CleanupEngine

    engine = CleanupEngine()
    result = engine.clean(categories)
    return {
        "success": True,
        "categories": categories,
        "freed_bytes": result.get("total_freed_bytes", 0),
        "freed_str": result.get("total_freed_str", ""),
        "files_removed": result.get("files_removed", 0),
        "result": result,
    }