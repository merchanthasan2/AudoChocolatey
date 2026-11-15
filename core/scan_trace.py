"""JSONL trace for scan orchestrator jobs."""

from __future__ import annotations

import json
import os
import threading
import time
from typing import Any, Dict

_LOCK = threading.Lock()


def trace_path() -> str:
    root = os.path.join(
        os.environ.get("LOCALAPPDATA", ""),
        "QuantumUpdater",
        "logs",
    )
    os.makedirs(root, exist_ok=True)
    return os.path.join(root, "scan-trace.jsonl")


def trace_event(event: str, **fields: Any) -> None:
    record: Dict[str, Any] = {"event": event, "ts": time.time(), **fields}
    line = json.dumps(record, ensure_ascii=False)
    with _LOCK:
        try:
            with open(trace_path(), "a", encoding="utf-8") as fh:
                fh.write(line + "\n")
        except OSError:
            pass
