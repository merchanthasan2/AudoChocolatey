"""Unit tests for scan orchestrator, cache, and progress (no live winget)."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from typing import Any, List
from unittest.mock import MagicMock, patch

from core import scan_cache
from core.progress import estimate_percent
from core.scan_orchestrator import (
    CATALOG_BUDGET_S,
    CATALOG_RETRY_SLEEP_S,
    LANE_CATALOG,
    LANE_DISK,
    LANE_LOCAL,
    ScanOrchestrator,
)


class FakeClock:
    def __init__(self, start: float = 1_000_000.0) -> None:
        self.t = start
        self._lock = threading.Lock()

    def now(self) -> float:
        with self._lock:
            return self.t

    def advance(self, seconds: float) -> None:
        with self._lock:
            self.t += seconds


class FakeManager:
    def __init__(self) -> None:
        self.cached_updates: List[Any] = []
        self.cached_installed: List[Any] = []
        self.current_engine = MagicMock()
        self.current_engine.check_updates = MagicMock(return_value=[])
        self.current_engine.list_installed = MagicMock(return_value=[])
        self.current_engine.last_error = ""


class ScanOrchestratorTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self._orig_scans_dir = scan_cache.scans_dir

        def tmp_scans_dir() -> str:
            root = os.path.join(self._tmp.name, "scans")
            os.makedirs(root, exist_ok=True)
            return root

        scan_cache.scans_dir = tmp_scans_dir  # type: ignore[method-assign]

    def tearDown(self) -> None:
        scan_cache.scans_dir = self._orig_scans_dir  # type: ignore[method-assign]
        self._tmp.cleanup()

    def _orch(self) -> tuple[ScanOrchestrator, FakeClock, FakeManager]:
        clock = FakeClock()
        mgr = FakeManager()
        orch = ScanOrchestrator(mgr, time_fn=clock.now)
        return orch, clock, mgr

    def test_catalog_lane_serializes(self) -> None:
        orch, clock, _mgr = self._orch()
        order: List[str] = []

        def slow_catalog(job_id: str):
            def fn(ctx: Any) -> dict:
                order.append(job_id)
                clock.advance(0.5)
                return {"job": job_id}

            return fn

        orch.register_job("cat-a", LANE_CATALOG, slow_catalog("cat-a"), CATALOG_BUDGET_S)
        orch.register_job("cat-b", LANE_CATALOG, slow_catalog("cat-b"), CATALOG_BUDGET_S)
        orch.scan_start("cat-a")
        orch.scan_start("cat-b")
        deadline = time.time() + 5.0
        while time.time() < deadline:
            jobs = orch.scans_state()["jobs"]
            if jobs.get("cat-a", {}).get("status") == "ready" and jobs.get("cat-b", {}).get("status") == "ready":
                break
            time.sleep(0.02)
        self.assertEqual(order, ["cat-a", "cat-b"])

    def test_local_jobs_run_independently(self) -> None:
        orch, clock, _mgr = self._orch()
        done = {"a": False, "b": False}

        def mk(job: str):
            def fn(ctx: Any) -> dict:
                clock.advance(0.2)
                done[job] = True
                return {"job": job}

            return fn

        orch.register_job("local-a", LANE_LOCAL, mk("a"), 10.0)
        orch.register_job("local-b", LANE_LOCAL, mk("b"), 10.0)
        orch.scan_start("local-a")
        orch.scan_start("local-b")
        deadline = time.time() + 5.0
        while time.time() < deadline and not (done["a"] and done["b"]):
            time.sleep(0.02)
        self.assertTrue(done["a"] and done["b"])

    def test_budget_measured_from_job_start(self) -> None:
        orch, clock, _mgr = self._orch()

        def slow_fn(_ctx: Any) -> dict:
            clock.advance(0.5)
            return {"late": True}

        orch.register_job("budget-job", LANE_LOCAL, slow_fn, 0.1)
        rec = orch.wait_for_job("budget-job", budget_s=0.1)
        self.assertEqual(rec["status"], "failed")
        self.assertEqual(rec.get("error"), "budget exceeded")

    def test_catalog_retries_once_after_sleep(self) -> None:
        orch, _clock, _mgr = self._orch()
        attempts = {"n": 0}

        def flaky(_ctx: Any) -> dict:
            attempts["n"] += 1
            if attempts["n"] == 1:
                raise RuntimeError("transient")
            return {"ok": True}

        orch.register_job("retry-cat", LANE_CATALOG, flaky, CATALOG_BUDGET_S)
        with patch.object(ScanOrchestrator, "_run_job", wraps=orch._run_job):
            with patch("core.scan_orchestrator.CATALOG_RETRY_SLEEP_S", 0.05):
                rec = orch.wait_for_job("retry-cat", budget_s=CATALOG_BUDGET_S)
        self.assertEqual(rec["status"], "ready")
        self.assertEqual(attempts["n"], 2)

    def test_cancel_kills_slow_subprocess(self) -> None:
        orch, _clock, _mgr = self._orch()
        holder: dict = {}

        def slow(ctx: Any) -> dict:
            proc = subprocess.Popen(
                [sys.executable, "-c", "import time; time.sleep(30)"],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
            holder["proc"] = proc
            while proc.poll() is None:
                if ctx.cancelled.is_set():
                    proc.kill()
                    raise RuntimeError("cancelled")
                time.sleep(0.05)
            return {"done": True}

        orch.register_job("slow-disk", LANE_DISK, slow, 60.0)
        orch.scan_start("slow-disk")
        deadline = time.time() + 3.0
        while time.time() < deadline and "proc" not in holder:
            time.sleep(0.02)
        self.assertIn("proc", holder)
        orch.scan_cancel("slow-disk")
        proc = holder["proc"]
        proc.wait(timeout=5)
        self.assertIsNotNone(proc.returncode)
        rec = orch.scans_state()["jobs"]["slow-disk"]
        self.assertEqual(rec["status"], "cancelled")

    def test_cache_atomic_write_and_corrupt_ignored(self) -> None:
        job_id = "cache-test"
        scan_cache.save_cache(job_id, {"v": 1}, [1.0])
        path = scan_cache.cache_path(job_id)
        self.assertTrue(os.path.isfile(path))
        with open(path, "r", encoding="utf-8") as fh:
            json.load(fh)
        with open(path, "w", encoding="utf-8") as fh:
            fh.write("{not json")
        self.assertIsNone(scan_cache.load_cache(job_id))

    def test_cache_schema_mismatch_ignored(self) -> None:
        job_id = "schema-test"
        path = scan_cache.cache_path(job_id)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(
                {
                    "schema_version": 99,
                    "app_version": scan_cache.APP_VERSION,
                    "machine_fingerprint": scan_cache.machine_fingerprint(),
                    "result": {},
                },
                fh,
            )
        self.assertIsNone(scan_cache.load_cache(job_id))

    def test_percent_never_hundred_before_done(self) -> None:
        for elapsed in (0.0, 3.0, 6.0, 11.0, 24.0):
            pct = estimate_percent(elapsed, [12.0, 12.0, 12.0], done=False)
            self.assertLessEqual(pct, 95)
        self.assertEqual(estimate_percent(12.0, [12.0], done=True), 100)

    def test_optimize_memory_api_shape(self) -> None:
        from web_api import WebAPI
        from core.manager import PackageManager

        api = WebAPI(PackageManager())
        payload = {
            "freed_bytes": 1024,
            "before": 100,
            "after": 1124,
            "processes_trimmed": 3,
            "processes_skipped": 2,
        }
        with patch("core.memory_trim.optimize_ram_trim", return_value=payload):
            result = api.optimize_memory()
        self.assertEqual(set(result.keys()), set(payload.keys()))

    def test_webapi_registers_tool_scan_jobs(self) -> None:
        from web_api import WebAPI
        from core.manager import PackageManager

        api = WebAPI(PackageManager())
        jobs = api.scans_state()["jobs"]
        self.assertIn("startup", jobs)
        self.assertIn("leftovers", jobs)
        self.assertIn("cleanup_scan", jobs)
        self.assertEqual(jobs["startup"]["lane"], "local")
        self.assertEqual(jobs["leftovers"]["lane"], "local")
        self.assertEqual(jobs["cleanup_scan"]["lane"], "disk")


if __name__ == "__main__":
    unittest.main()
