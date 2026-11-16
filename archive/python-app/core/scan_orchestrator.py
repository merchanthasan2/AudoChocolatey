"""Multi-lane scan orchestrator (Track 0)."""

from __future__ import annotations

import os
import queue
import threading
import time
from typing import Any, Callable, Dict, List, Optional

from core.models import PackageInfo
from core.scan_cache import load_cache, save_cache
from core.scan_trace import trace_event

LANE_CATALOG = "catalog"
LANE_LOCAL = "local"
LANE_DISK = "disk"
LANE_POLISH = "polish"

CATALOG_BUDGET_S = 60.0
LOCAL_BUDGET_S = 10.0
CATALOG_RETRY_SLEEP_S = 0

BUILTIN_JOBS = {
    "catalog-updates": {"lane": LANE_CATALOG, "budget_s": CATALOG_BUDGET_S, "stage": "updates"},
    "catalog-installed": {"lane": LANE_CATALOG, "budget_s": CATALOG_BUDGET_S, "stage": "installed"},
    "local-hardware": {"lane": LANE_LOCAL, "budget_s": LOCAL_BUDGET_S, "stage": "hardware"},
    "local-arp": {"lane": LANE_LOCAL, "budget_s": LOCAL_BUDGET_S, "stage": "arp"},
}

# App bootstrap: Software tabs only; defer maintenance and hardware to tab focus.
BOOTSTRAP_JOB_IDS = (
    "catalog-updates",
    "catalog-installed",
    "local-arp",
)

DEFERRED_MAINTENANCE_JOB_IDS = (
    "cleanup_scan",
    "startup",
    "leftovers",
)


class ScanOrchestrator:
    """Coordinates catalog, local, disk, and polish scan lanes."""

    def __init__(self, manager: Any, *, time_fn: Callable[[], float] = time.time) -> None:
        self.manager = manager
        self._time = time_fn
        self._lock = threading.RLock()
        self._jobs: Dict[str, Dict[str, Any]] = {}
        self._registered: Dict[str, Dict[str, Any]] = {}
        self._durations: Dict[str, List[float]] = {}
        self._cancel_events: Dict[str, threading.Event] = {}
        self._catalog_queue: queue.Queue[str] = queue.Queue()
        self._catalog_paused = False
        self._catalog_worker = threading.Thread(target=self._catalog_worker_loop, daemon=True)
        self._catalog_worker.start()
        self._init_builtin_jobs()
        self._hydrate_from_cache()

    def register_job(
        self,
        job_id: str,
        lane: str,
        fn: Callable[..., Any],
        budget_s: float,
        *,
        stage: str = "",
    ) -> None:
        """
        Register a scan job for another agent (disk lane, etc.).

        Contract for `fn(ctx)`:
        - `ctx.job_id` str, `ctx.budget_s` float remaining, `ctx.cancelled` threading.Event
        - Return the JSON-serializable `result` dict/list
        - Raise on failure; orchestrator records error and may cache partial results
        """
        with self._lock:
            self._registered[job_id] = {
                "lane": lane,
                "budget_s": budget_s,
                "fn": fn,
                "stage": stage or job_id,
            }
            self._ensure_job_record(job_id, lane, budget_s, stage or job_id)

    def _init_builtin_jobs(self) -> None:
        for job_id, meta in BUILTIN_JOBS.items():
            self._ensure_job_record(
                job_id,
                meta["lane"],
                meta["budget_s"],
                meta["stage"],
            )

    def _ensure_job_record(self, job_id: str, lane: str, budget_s: float, stage: str) -> None:
        if job_id not in self._jobs:
            self._jobs[job_id] = {
                "id": job_id,
                "lane": lane,
                "status": "queued",
                "percent": 0,
                "stage": stage,
                "current_item": "",
                "eta_s": 0,
                "started_at": None,
                "finished_at": None,
                "queued_at": None,
                "error": None,
                "partial": False,
                "cached": False,
                "cached_at": None,
                "budget_s": budget_s,
            }
            self._durations[job_id] = []

    @staticmethod
    def _ready_has_usable_result(rec: Dict[str, Any]) -> bool:
        if rec.get("partial"):
            return False
        if rec.get("result") is not None:
            return True
        return False

    def _hydrate_from_cache(self) -> None:
        for job_id in list(self._jobs):
            payload = load_cache(job_id)
            if not payload:
                continue
            self._durations[job_id] = list(payload.get("durations") or [])
            with self._lock:
                rec = self._jobs[job_id]
                rec["status"] = "ready"
                rec["percent"] = 100
                rec["cached"] = True
                rec["cached_at"] = payload.get("saved_at")
                rec["result"] = payload.get("result")
                rec["partial"] = bool(payload.get("partial"))

    def scans_start_all(self, *, force: bool = False) -> None:
        for job_id in BOOTSTRAP_JOB_IDS:
            if job_id in self._jobs:
                self.scan_start(job_id, force=force)

    def scans_start_maintenance(
        self,
        job_ids: Optional[List[str]] = None,
        *,
        force: bool = False,
    ) -> None:
        """Start deferred maintenance scans (cleanup, startup, leftovers) on tab focus."""
        targets = list(job_ids) if job_ids else list(DEFERRED_MAINTENANCE_JOB_IDS)
        for job_id in targets:
            if job_id in self._jobs:
                self.scan_start(job_id, force=force)

    def scan_start_deferred_maintenance(
        self,
        job_ids: Optional[List[str]] = None,
        *,
        force: bool = False,
    ) -> None:
        self.scans_start_maintenance(job_ids, force=force)

    def scan_start(self, job_id: str, *, force: bool = False) -> None:
        with self._lock:
            if job_id not in self._jobs:
                return
            rec = self._jobs[job_id]
            if rec["status"] == "scanning" and not force:
                return
            if not force and rec["status"] == "ready" and self._ready_has_usable_result(rec):
                return
            rec["status"] = "queued"
            rec["percent"] = 0
            rec["error"] = None
            rec["partial"] = False
            rec["cached"] = False
            rec["queued_at"] = self._time()
            rec["started_at"] = None
            rec["finished_at"] = None
            trace_event(
                "job_queued",
                job_id=job_id,
                lane=rec["lane"],
                queued_at=rec["queued_at"],
            )
            lane = rec["lane"]
        if lane == LANE_CATALOG:
            self._catalog_queue.put(job_id)
        elif lane == LANE_POLISH:
            threading.Thread(target=self._run_job, args=(job_id,), daemon=True).start()
        else:
            threading.Thread(target=self._run_job, args=(job_id,), daemon=True).start()

    def update_job_progress(
        self,
        job_id: str,
        *,
        percent: Optional[int] = None,
        current_item: str = "",
    ) -> None:
        with self._lock:
            rec = self._jobs.get(job_id)
            if not rec or rec["status"] != "scanning":
                return
            if percent is not None:
                rec["percent"] = max(0, min(100, int(percent)))
            if current_item:
                rec["current_item"] = current_item

    def scan_cancel(self, job_id: str) -> None:
        with self._lock:
            rec = self._jobs.get(job_id)
            if not rec:
                return
            ev = self._cancel_events.setdefault(job_id, threading.Event())
            ev.set()
            rec["status"] = "cancelled"
            rec["finished_at"] = self._time()
        from core.winget_engine import cancel_catalog_query

        cancel_catalog_query(job_id)
        trace_event("job_cancelled", job_id=job_id)

    def scans_state(self) -> Dict[str, Any]:
        with self._lock:
            jobs = {jid: self._public_job(jid) for jid in self._jobs}
        done = sum(1 for j in jobs.values() if j["status"] in ("ready", "failed", "cancelled"))
        total = len(jobs)
        running = [j for j in jobs.values() if j["status"] == "scanning"]
        eta_s = max((j.get("eta_s") or 0) for j in running) if running else 0
        return {"jobs": jobs, "summary": {"done": done, "total": total, "eta_s": eta_s, "running": len(running)}}

    def scan_result(self, job_id: str) -> Any:
        with self._lock:
            rec = self._jobs.get(job_id)
            if not rec:
                return None
            if "result" in rec:
                return rec.get("result")
            cached = load_cache(job_id)
            if cached:
                return cached.get("result")
        return None

    def wait_for_job(self, job_id: str, budget_s: Optional[float] = None) -> Dict[str, Any]:
        with self._lock:
            if job_id not in self._jobs:
                raise KeyError(job_id)
            max_b = float(budget_s or self._jobs[job_id].get("budget_s") or CATALOG_BUDGET_S)
        self.scan_start(job_id)
        while True:
            with self._lock:
                rec = self._jobs[job_id]
                status = rec["status"]
                started = rec.get("started_at")
                queued = rec.get("queued_at")
            if status in ("ready", "failed", "cancelled"):
                break
            now = self._time()
            if started is not None and now - started >= max_b:
                break
            if status == "queued" and queued is not None and now - queued >= max_b:
                break
            time.sleep(0.05)
        return self._public_job(job_id)

    def pause_catalog_for_install(self) -> None:
        self._catalog_paused = True
        for job_id, rec in self._jobs.items():
            if rec["lane"] == LANE_CATALOG and rec["status"] == "scanning":
                self.scan_cancel(job_id)

    def resume_catalog_after_install(self) -> None:
        self._catalog_paused = False
        self.scan_start("catalog-updates", force=True)
        self.scan_start("catalog-installed", force=False)

    def _public_job(self, job_id: str) -> Dict[str, Any]:
        rec = dict(self._jobs[job_id])
        rec.pop("result", None)
        return rec

    def _catalog_worker_loop(self) -> None:
        while True:
            job_id = self._catalog_queue.get()
            if self._catalog_paused:
                with self._lock:
                    if job_id in self._jobs:
                        self._jobs[job_id]["status"] = "queued"
                self._catalog_queue.put(job_id)
                time.sleep(0.2)
                continue
            self._run_job(job_id)

    def _set_disk_background(self, begin: bool) -> None:
        if os.name != "nt":
            return
        try:
            import ctypes

            kernel32 = ctypes.windll.kernel32
            mode = 0x00010000 if begin else 0x00020000
            kernel32.SetThreadPriority(kernel32.GetCurrentThread(), mode)
        except Exception:
            pass

    def _run_job(self, job_id: str) -> None:
        with self._lock:
            if job_id not in self._jobs:
                return
            rec = self._jobs[job_id]
            rec["status"] = "scanning"
            rec["started_at"] = self._time()
            rec["percent"] = 0
            budget_s = float(rec.get("budget_s") or CATALOG_BUDGET_S)
            lane = rec["lane"]
            queue_wait = 0.0
            if rec.get("queued_at"):
                queue_wait = rec["started_at"] - rec["queued_at"]
        trace_event(
            "job_started",
            job_id=job_id,
            lane=lane,
            started_at=rec["started_at"],
            queue_wait_s=round(queue_wait, 3),
        )
        cancel_ev = threading.Event()
        self._cancel_events[job_id] = cancel_ev
        t0 = self._time()
        result = None
        error = None
        partial = False
        if lane == LANE_DISK:
            self._set_disk_background(True)
        attempts = 2 if lane == LANE_CATALOG else 1
        try:
            for attempt in range(1, attempts + 1):
                if cancel_ev.is_set():
                    error = "cancelled"
                    break
                elapsed = self._time() - t0
                if elapsed >= budget_s:
                    error = "budget exceeded"
                    break
                remaining = budget_s - elapsed
                try:
                    if job_id in self._registered:
                        fn = self._registered[job_id]["fn"]
                        result = fn(self._JobCtx(job_id, remaining, cancel_ev))
                    else:
                        result = self._run_builtin(job_id, remaining, cancel_ev)
                    if self._time() - t0 >= budget_s:
                        error = "budget exceeded"
                        result = None
                        break
                    error = None
                    break
                except Exception as exc:
                    error = str(exc)
                    if attempt < attempts and lane == LANE_CATALOG:
                        if self._time() - t0 + CATALOG_RETRY_SLEEP_S < budget_s:
                            time.sleep(CATALOG_RETRY_SLEEP_S)
                            continue
                    break
                finally:
                    self._tick_progress(job_id, t0, done=False)
        finally:
            if lane == LANE_DISK:
                self._set_disk_background(False)
        duration = self._time() - t0
        with self._lock:
            rec = self._jobs[job_id]
            rec["finished_at"] = self._time()
            if error:
                rec["status"] = "cancelled" if error == "cancelled" else "failed"
                rec["error"] = error
                rec["percent"] = 0
            else:
                rec["status"] = "ready"
                rec["result"] = result
                rec["percent"] = 100
                rec["cached"] = False
            rec["eta_s"] = 0
            durations = self._durations.setdefault(job_id, [])
            if duration > 0 and rec["status"] == "ready":
                durations.append(duration)
                durations[:] = durations[-5:]
                save_cache(job_id, result, durations, partial=partial)
                if job_id == "catalog-updates" and isinstance(result, list):
                    self.manager.cached_updates = self._dicts_to_packages(result)
                if job_id == "catalog-installed" and isinstance(result, list):
                    self.manager.cached_installed = self._dicts_to_packages(result)
        trace_event(
            "job_ended",
            job_id=job_id,
            duration_s=round(duration, 3),
            status=self._jobs[job_id]["status"],
            error=error,
        )
        self._cancel_events.pop(job_id, None)

    class _JobCtx:
        def __init__(self, job_id: str, budget_s: float, cancelled: threading.Event) -> None:
            self.job_id = job_id
            self.budget_s = budget_s
            self.cancelled = cancelled

    def _tick_progress(self, job_id: str, started_at: float, *, done: bool) -> None:
        # Progress percent/ETA come only from update_job_progress or job completion.
        return

    def _run_builtin(self, job_id: str, budget_s: float, cancel_ev: threading.Event) -> Any:
        if cancel_ev.is_set():
            raise RuntimeError("cancelled")
        if job_id == "catalog-updates":
            engine = self.manager.current_engine
            pkgs = engine.check_updates(job_id=job_id)
            err = getattr(engine, "last_error", "") or ""
            if not pkgs and err:
                raise RuntimeError(err)
            return [self._package_dict(p) for p in pkgs]
        if job_id == "catalog-installed":
            engine = self.manager.current_engine
            pkgs = engine.list_installed(job_id=job_id)
            return [self._package_dict(p) for p in pkgs]
        if job_id == "local-hardware":
            from core.hardware_facts import collect_windows_hardware

            return collect_windows_hardware(deadline=min(budget_s, LOCAL_BUDGET_S))
        if job_id == "local-arp":
            return self._run_arp_inventory()
        raise KeyError(job_id)

    def _run_arp_inventory(self) -> List[Dict[str, Any]]:
        from core.arp_inventory import list_arp_entries, plan_uninstall

        rows: List[Dict[str, Any]] = []
        for entry in list_arp_entries():
            plan = plan_uninstall(entry)
            uninstall_kind = "registry"
            if plan and plan.get("exe", "").lower().endswith("msiexec.exe"):
                uninstall_kind = "msi"
            elif plan:
                uninstall_kind = "exe"
            rows.append(
                {
                    "id": entry.id,
                    "name": entry.name,
                    "installed_version": entry.version or "",
                    "source": "registry",
                    "origin": "this-pc",
                    "can_uninstall": plan is not None,
                    "engine": "direct",
                    "uninstall_kind": uninstall_kind,
                }
            )
        return rows

    @staticmethod
    def _package_dict(p: PackageInfo) -> Dict[str, Any]:
        return {
            "id": p.id,
            "name": p.name,
            "installed_version": p.installed_version,
            "available_version": p.available_version,
            "source": p.source,
            "origin": getattr(p, "origin", "catalog"),
            "can_uninstall": getattr(p, "can_uninstall", True),
            "engine": p.engine,
            "uninstall_kind": getattr(p, "uninstall_kind", "catalog"),
        }

    @staticmethod
    def _dicts_to_packages(rows: List[Dict[str, Any]]) -> List[PackageInfo]:
        out: List[PackageInfo] = []
        for row in rows:
            out.append(
                PackageInfo(
                    name=row.get("name", ""),
                    id=row.get("id", ""),
                    installed_version=row.get("installed_version", ""),
                    available_version=row.get("available_version", row.get("installed_version", "")),
                    source=row.get("source", "winget"),
                    engine=row.get("engine", "winget"),
                    origin=row.get("origin", "catalog"),
                    can_uninstall=row.get("can_uninstall", True),
                    uninstall_kind=row.get("uninstall_kind", "catalog"),
                )
            )
        return out

    @staticmethod
    def merge_installed(catalog_rows: List[Dict[str, Any]], arp_rows: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        seen = {r["name"].casefold() for r in catalog_rows if r.get("name")}
        merged = list(catalog_rows)
        for row in arp_rows:
            name = (row.get("name") or "").casefold()
            if name and name not in seen:
                seen.add(name)
                merged.append(row)
        return merged
