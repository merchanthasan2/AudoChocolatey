import threading
import json
import os
from typing import List, Dict, Optional, Callable, Any
import platform
from core.models import PackageInfo, SearchResult, OperationStatus
from core.base_engine import BaseEngine
from core.winget_engine import WingetEngine
from core.choco_engine import ChocolateyEngine
from core.mac_cleaner import MacCleaner
from core.mac_engine import HomebrewEngine, AppleUpdateEngine, MacApplicationsEngine
from core.updater import AppUpdater

CONFIG_FILE = os.path.join(os.path.expanduser("~"), ".quantum_updater_config.json")

class PackageManager:
    """Central manager handling engines, worker threads, and system orchestration."""

    def __init__(self):
        self.is_mac = (platform.system() == "Darwin")
        self.cleaner = MacCleaner() if self.is_mac else None
        self.updater = AppUpdater()

        if self.is_mac:
            self.engines: Dict[str, BaseEngine] = {
                "brew": HomebrewEngine(),
                "apple": AppleUpdateEngine(),
                "apps": MacApplicationsEngine()
            }
        else:
            self.engines: Dict[str, BaseEngine] = {
                "winget": WingetEngine(),
                "choco": ChocolateyEngine()
            }

        self.settings = self._load_settings()
        default_engine = "brew" if self.is_mac else "winget"
        self.active_engine_id = self.settings.get("active_engine", default_engine)
        if self.active_engine_id not in self.engines or not self.engines[self.active_engine_id].is_available():
            # Fallback to whatever is available
            for eng_id, eng in self.engines.items():
                if eng.is_available():
                    self.active_engine_id = eng_id
                    break
            else:
                self.active_engine_id = list(self.engines.keys())[0]


        self.cached_updates: List[PackageInfo] = []
        self.cached_installed: List[PackageInfo] = []
        self._current_worker: Optional[threading.Thread] = None
        self.is_busy = False

    def _load_settings(self) -> dict:
        default_settings = {
            "active_engine": "winget",
            "silent_install": True,
            "accept_agreements": True,
            "theme": "dark",
            "auto_check_on_start": True
        }
        if os.path.exists(CONFIG_FILE):
            try:
                with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    default_settings.update(data)
            except Exception:
                pass
        return default_settings

    def save_settings(self):
        try:
            with open(CONFIG_FILE, "w", encoding="utf-8") as f:
                json.dump(self.settings, f, indent=2)
        except Exception:
            pass

    @property
    def current_engine(self) -> BaseEngine:
        return self.engines.get(self.active_engine_id, next(iter(self.engines.values())))

    def set_active_engine(self, engine_id: str):
        if engine_id in self.engines:
            self.active_engine_id = engine_id
            self.settings["active_engine"] = engine_id
            self.save_settings()

    def get_available_engines(self) -> List[dict]:
        res = []
        for eng_id, eng in self.engines.items():
            avail = eng.is_available()
            ver = eng.get_version() if avail else None
            res.append({
                "id": eng_id,
                "name": eng.name,
                "available": avail,
                "version": ver
            })
        return res

    def abort_all(self):
        """Immediately abort all active tasks, worker threads, and child processes."""
        self.is_busy = False
        for engine in self.engines.values():
            try:
                engine.abort()
            except Exception:
                pass

    def run_in_background(
        self,
        task_fn: Callable,
        on_done: Optional[Callable[[Any], None]] = None,
        on_error: Optional[Callable[[Exception], None]] = None
    ):
        """Execute a task in a background thread."""
        def worker():
            self.is_busy = True
            try:
                result = task_fn()
                if on_done:
                    on_done(result)
            except Exception as e:
                if on_error:
                    on_error(e)
            finally:
                self.is_busy = False

        thread = threading.Thread(target=worker, daemon=True)
        self._current_worker = thread
        thread.start()

    def check_updates_async(
        self,
        on_log: Callable[[str], None],
        on_success: Callable[[List[PackageInfo]], None],
        on_error: Callable[[Exception], None]
    ):
        def task():
            engine = self.current_engine
            updates = engine.check_updates(on_log=on_log)
            self.cached_updates = updates
            return updates

        self.run_in_background(task, on_done=on_success, on_error=on_error)

    def list_installed_async(
        self,
        filter_query: Optional[str],
        on_log: Callable[[str], None],
        on_success: Callable[[List[PackageInfo]], None],
        on_error: Callable[[Exception], None]
    ):
        def task():
            engine = self.current_engine
            installed = engine.list_installed(filter_query=filter_query, on_log=on_log)
            self.cached_installed = installed
            return installed

        self.run_in_background(task, on_done=on_success, on_error=on_error)

    def search_packages_async(
        self,
        query: str,
        on_log: Callable[[str], None],
        on_success: Callable[[List[SearchResult]], None],
        on_error: Callable[[Exception], None]
    ):
        def task():
            engine = self.current_engine
            return engine.search_packages(query=query, on_log=on_log)

        self.run_in_background(task, on_done=on_success, on_error=on_error)

    def upgrade_package_async(
        self,
        package_id: str,
        on_log: Callable[[str], None],
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_success: Optional[Callable[[OperationStatus], None]] = None,
        on_error: Optional[Callable[[Exception], None]] = None
    ):
        def task():
            engine = self.current_engine
            silent = self.settings.get("silent_install", True)
            agree = self.settings.get("accept_agreements", True)
            return engine.upgrade_package(
                package_id=package_id,
                on_log=on_log,
                on_progress=on_progress,
                silent=silent,
                accept_agreements=agree
            )

        self.run_in_background(task, on_done=on_success, on_error=on_error)

    def upgrade_all_async(
        self,
        on_log: Callable[[str], None],
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_success: Optional[Callable[[OperationStatus], None]] = None,
        on_error: Optional[Callable[[Exception], None]] = None
    ):
        def task():
            engine = self.current_engine
            silent = self.settings.get("silent_install", True)
            agree = self.settings.get("accept_agreements", True)
            return engine.upgrade_all(
                on_log=on_log,
                on_progress=on_progress,
                silent=silent,
                accept_agreements=agree
            )

        self.run_in_background(task, on_done=on_success, on_error=on_error)

    def upgrade_multiple_async(
        self,
        package_ids: List[str],
        on_log: Callable[[str], None],
        on_item_start: Optional[Callable[[str, int, int], None]] = None,
        on_item_progress: Optional[Callable[[str, Optional[float], str], None]] = None,
        on_item_done: Optional[Callable[[str, OperationStatus, int, int], None]] = None,
        on_complete: Optional[Callable[[List[OperationStatus]], None]] = None,
        on_error: Optional[Callable[[Exception], None]] = None
    ):
        """Upgrade a specific list of selected packages sequentially with live per-item updates."""
        def task():
            engine = self.current_engine
            silent = self.settings.get("silent_install", True)
            agree = self.settings.get("accept_agreements", True)
            total = len(package_ids)
            results = []

            on_log(f"\n==================================================\n")
            on_log(f"[WinPackageHub]: Starting batch upgrade of {total} selected package(s)...\n")
            on_log(f"==================================================\n\n")

            for idx, pkg_id in enumerate(package_ids, start=1):
                if on_item_start:
                    on_item_start(pkg_id, idx, total)

                on_log(f"\n--- [{idx}/{total}] Upgrading: {pkg_id} ---\n")

                def per_item_prog(pct_val, status_str):
                    if on_item_progress:
                        on_item_progress(pkg_id, pct_val, status_str)

                status = engine.upgrade_package(
                    package_id=pkg_id,
                    on_log=on_log,
                    on_progress=per_item_prog,
                    silent=silent,
                    accept_agreements=agree
                )
                results.append(status)

                if on_item_done:
                    on_item_done(pkg_id, status, idx, total)

            on_log(f"\n==================================================\n")
            successful = sum(1 for r in results if r.success)
            on_log(f"[WinPackageHub]: Batch upgrade finished: {successful}/{total} successful.\n")
            on_log(f"==================================================\n\n")

            return results

        self.run_in_background(task, on_done=on_complete, on_error=on_error)

    def install_package_async(
        self,
        package_id: str,
        on_log: Callable[[str], None],
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_success: Optional[Callable[[OperationStatus], None]] = None,
        on_error: Optional[Callable[[Exception], None]] = None
    ):
        def task():
            engine = self.current_engine
            silent = self.settings.get("silent_install", True)
            agree = self.settings.get("accept_agreements", True)
            return engine.install_package(
                package_id=package_id,
                on_log=on_log,
                on_progress=on_progress,
                silent=silent,
                accept_agreements=agree
            )

        self.run_in_background(task, on_done=on_success, on_error=on_error)

    def uninstall_package_async(
        self,
        package_id: str,
        on_log: Callable[[str], None],
        on_success: Callable[[OperationStatus], None],
        on_error: Callable[[Exception], None]
    ):
        def task():
            engine = self.current_engine
            return engine.uninstall_package(
                package_id=package_id,
                on_log=on_log
            )

        self.run_in_background(task, on_done=on_success, on_error=on_error)
