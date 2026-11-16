"""On-disk scan result cache with schema validation."""

from __future__ import annotations

import hashlib
import json
import os
import platform
import tempfile
from typing import Any, Dict, List, Optional

SCHEMA_VERSION = 1
APP_VERSION = "2.0.0"


def scans_dir() -> str:
    root = os.path.join(
        os.environ.get("LOCALAPPDATA", ""),
        "QuantumUpdater",
        "scans",
    )
    os.makedirs(root, exist_ok=True)
    return root


def _registry_machine_guid() -> str:
    if os.name != "nt":
        return platform.node()
    try:
        import winreg

        access = winreg.KEY_READ | getattr(winreg, "KEY_WOW64_64KEY", 0)
        with winreg.OpenKey(
            winreg.HKEY_LOCAL_MACHINE,
            r"SOFTWARE\Microsoft\Cryptography",
            0,
            access,
        ) as key:
            val, _ = winreg.QueryValueEx(key, "MachineGuid")
            return str(val)
    except OSError:
        return ""


def machine_fingerprint() -> str:
    blob = f"{platform.node()}:{_registry_machine_guid()}:{platform.machine()}"
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()[:32]


def cache_path(job_id: str) -> str:
    safe = hashlib.sha256(job_id.encode("utf-8")).hexdigest()[:16]
    return os.path.join(scans_dir(), f"{safe}-{job_id.replace('/', '_')}.json")


def load_cache(job_id: str) -> Optional[Dict[str, Any]]:
    path = cache_path(job_id)
    if not os.path.isfile(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as fh:
            payload = json.load(fh)
    except (OSError, json.JSONDecodeError, TypeError, ValueError):
        return None
    if payload.get("schema_version") != SCHEMA_VERSION:
        return None
    if payload.get("app_version") != APP_VERSION:
        return None
    if payload.get("machine_fingerprint") != machine_fingerprint():
        return None
    if "result" not in payload:
        return None
    return payload


def save_cache(
    job_id: str,
    result: Any,
    durations: List[float],
    *,
    partial: bool = False,
) -> None:
    path = cache_path(job_id)
    payload = {
        "schema_version": SCHEMA_VERSION,
        "app_version": APP_VERSION,
        "machine_fingerprint": machine_fingerprint(),
        "job_id": job_id,
        "saved_at": _now_ts(),
        "partial": partial,
        "result": result,
        "durations": durations[-5:],
    }
    directory = os.path.dirname(path)
    fd, tmp = tempfile.mkstemp(suffix=".json", dir=directory)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as fh:
            json.dump(payload, fh, indent=2)
        os.replace(tmp, path)
    except OSError:
        try:
            os.remove(tmp)
        except OSError:
            pass


def _now_ts() -> float:
    import time

    return time.time()
