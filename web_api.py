import threading
import json
import platform
import sys
from typing import List, Dict, Any, Optional
from core.models import PackageInfo, SearchResult, OperationStatus
from core.manager import PackageManager
from core.admin import is_admin

class WebAPI:
    """Python bridge exposed to JavaScript via pywebview."""

    def __init__(self, manager: PackageManager):
        self.manager = manager
        self._window = None

    def set_window(self, window):
        self._window = window

    def _eval_js(self, js_code: str):
        if self._window:
            try:
                self._window.evaluate_js(js_code)
            except Exception:
                pass

    def _on_log(self, text: str):
        escaped = json.dumps(text)
        self._eval_js(f"app.appendLog({escaped});")

    def _on_progress(self, pct: Optional[float], ticker: str):
        p_val = pct if pct is not None else "null"
        escaped_ticker = json.dumps(ticker)
        self._eval_js(f"app.setIslandProgress({p_val}, {escaped_ticker});")

    def scan_updates(self, *args, **kwargs) -> List[Dict[str, Any]]:
        """Scans system for available updates synchronously for JS call."""
        event = threading.Event()
        result_box = []
        error_box = []

        def on_success(packages: List[PackageInfo]):
            result_box.extend(packages)
            event.set()

        def on_error(err: Exception):
            error_box.append(err)
            event.set()

        self.manager.check_updates_async(
            on_log=self._on_log,
            on_success=on_success,
            on_error=on_error
        )

        event.wait(timeout=180) # allow up to 3 mins for deep system audit

        if error_box:
            raise RuntimeError(str(error_box[0]))

        return [
            {
                "id": p.id,
                "name": p.name,
                "installed_version": p.installed_version,
                "available_version": p.available_version,
                "source": p.source
            }
            for p in result_box
        ]

    def upgrade_packages(self, package_ids: Optional[List[str]] = None, *args, **kwargs) -> bool:
        """Upgrades multiple packages in background thread, sending live progress."""
        if package_ids is None and args and isinstance(args[0], list):
            package_ids = args[0]
        package_ids = package_ids or []
        def worker():
            def on_item_finish(status: OperationStatus):
                pass

            def on_all_finish(statuses: List[OperationStatus]):
                success_count = sum(1 for s in statuses if s.success)
                msg = f"Completed {success_count}/{len(statuses)} package updates."
                escaped_msg = json.dumps(msg)
                self._eval_js(f"app.onOperationFinished(true, {escaped_msg});")

            self.manager.upgrade_multiple_async(
                package_ids=package_ids,
                on_log=self._on_log,
                on_progress=self._on_progress,
                on_item_finished=on_item_finish,
                on_all_finished=on_all_finish
            )

        threading.Thread(target=worker, daemon=True).start()
        return True

    def search_catalog(self, query: str = "", *args, **kwargs) -> List[Dict[str, Any]]:
        if not query and args and isinstance(args[0], str):
            query = args[0]
        event = threading.Event()
        result_box = []
        error_box = []

        def on_success(results: List[SearchResult]):
            result_box.extend(results)
            event.set()

        def on_error(err: Exception):
            error_box.append(err)
            event.set()

        self.manager.search_packages_async(
            query=query,
            on_log=self._on_log,
            on_success=on_success,
            on_error=on_error
        )

        event.wait(timeout=60)
        return [
            {
                "id": r.id,
                "name": r.name,
                "version": r.version,
                "source": r.source,
                "description": r.description
            }
            for r in result_box
        ]

    def install_package(self, package_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not package_id and args and isinstance(args[0], str):
            package_id = args[0]
        event = threading.Event()
        result_box = []

        def on_prog(pct: Optional[float], ticker: str):
            self._on_progress(pct, ticker)

        def on_success(status: OperationStatus):
            result_box.append(status)
            event.set()

        def on_error(err: Exception):
            result_box.append(OperationStatus(package_id, False, str(err)))
            event.set()

        self.manager.install_package_async(
            package_id=package_id,
            on_log=self._on_log,
            on_progress=on_prog,
            on_success=on_success,
            on_error=on_error
        )

        event.wait(timeout=300)
        status = result_box[0] if result_box else OperationStatus(package_id, False, "Timed out")
        return {"id": status.package_id, "success": status.success, "message": status.message}

    def get_installed(self, *args, **kwargs) -> List[Dict[str, Any]]:
        event = threading.Event()
        result_box = []

        def on_success(packages: List[PackageInfo]):
            result_box.extend(packages)
            event.set()

        def on_error(err: Exception):
            event.set()

        self.manager.list_installed_async(
            filter_query=None,
            on_log=self._on_log,
            on_success=on_success,
            on_error=on_error
        )

        event.wait(timeout=60)
        return [
            {
                "id": p.id,
                "name": p.name,
                "installed_version": p.installed_version
            }
            for p in result_box
        ]

    def uninstall_package(self, package_id: str = "", *args, **kwargs) -> bool:
        if not package_id and args and isinstance(args[0], str):
            package_id = args[0]
        event = threading.Event()
        def on_success(st): event.set()
        def on_error(err): event.set()

        self.manager.uninstall_package_async(
            package_id=package_id,
            on_log=self._on_log,
            on_success=on_success,
            on_error=on_error
        )
        event.wait(timeout=180)
        return True

    def set_engine(self, engine_id: str = "", *args, **kwargs) -> bool:
        if not engine_id and args and isinstance(args[0], str):
            engine_id = args[0]
        self.manager.set_active_engine(engine_id)
        return True

    def save_setting(self, key: str = "", value: Any = None, *args, **kwargs) -> bool:
        if not key and args and len(args) >= 1:
            key = args[0]
        if value is None and args and len(args) >= 2:
            value = args[1]
        self.manager.settings[key] = value
        self.manager.save_settings()
        return True

    def scan_cleanup(self, *args, **kwargs) -> Dict[str, Any]:
        """Scan system cleanup targets for recoverable space."""
        if self.manager.cleaner:
            return self.manager.cleaner.scan()
        return {
            "total_reclaimable_bytes": 0,
            "total_reclaimable_str": "0 B",
            "total_files": 0,
            "disk_total_str": "Unknown",
            "disk_used_str": "Unknown",
            "disk_free_str": "Unknown",
            "disk_used_percent": 0,
            "categories": []
        }

    def execute_cleanup(self, categories: Optional[List[str]] = None, *args, **kwargs) -> bool:
        """Executes cleanup sequence on background thread with live progress."""
        if categories is None and args and isinstance(args[0], list):
            categories = args[0]
        categories = categories or []

        def worker():
            try:
                if self.manager.cleaner:
                    res = self.manager.cleaner.clean(
                        category_ids=categories,
                        on_progress=self._on_progress,
                        on_log=self._on_log
                    )
                    escaped = json.dumps(res)
                    self._eval_js(f"app.onCleanupFinished(true, {escaped});")
                else:
                    self._eval_js("app.onCleanupFinished(false, JSON.stringify({error: 'Cleaner not available on this platform'}));")
            except Exception as e:
                err_msg = json.dumps(str(e))
                self._eval_js(f"app.onCleanupFinished(false, JSON.stringify({{error: {err_msg}}}));")

        threading.Thread(target=worker, daemon=True).start()
        return True

    def get_diagnostics(self, *args, **kwargs) -> Dict[str, Any]:
        engines = self.manager.get_available_engines()
        os_sys = platform.system()
        os_display = "macOS" if os_sys == "Darwin" else os_sys

        winget_eng = self.manager.engines.get("winget")
        choco_eng = self.manager.engines.get("choco")
        brew_eng = self.manager.engines.get("brew")
        apple_eng = self.manager.engines.get("apple")

        diag = {
            "is_admin": is_admin(),
            "os_system": os_sys,
            "os_display": os_display,
            "os_info": f"{os_display} {platform.release()} ({platform.machine()})",
            "arch": platform.machine(),
            "active_engine": self.manager.active_engine_id,
            "engines": engines,
        }

        if os_sys == "Darwin":
            diag["brew_path"] = str(getattr(brew_eng, "_brew_path", "Not Located")) if brew_eng else "Not Installed"
            diag["brew_version"] = str(brew_eng.get_version() or "Not Available") if brew_eng else "Not Available"
            diag["apple_version"] = str(apple_eng.get_version() or "Current") if apple_eng else "Current"
        else:
            diag["winget_path"] = str(getattr(winget_eng, "_executable", "Not Located")) if winget_eng else "Not Installed"
            diag["winget_version"] = str(winget_eng.get_version() or "Not Available") if winget_eng else "Not Available"
            diag["choco_path"] = str(getattr(choco_eng, "_executable", "Not Located")) if choco_eng else "Not Installed"
            diag["choco_version"] = str(choco_eng.get_version() or "Not Available") if choco_eng else "Not Available"

        return diag

    def check_self_update(self, *args, **kwargs) -> Dict[str, Any]:
        """Check GitHub for new Quantum Updater releases."""
        if hasattr(self.manager, "updater") and self.manager.updater:
            return self.manager.updater.check_for_updates()
        return {"has_update": False, "current_version": "2.0.0"}

    def apply_self_update(self, download_url: str = "", *args, **kwargs) -> bool:
        """Download and install the updated binary in-place without prompting."""
        if not download_url and args and isinstance(args[0], str):
            download_url = args[0]
        if not download_url and "downloadUrl" in kwargs:
            download_url = kwargs["downloadUrl"]

        def worker():
            if hasattr(self.manager, "updater") and self.manager.updater:
                self.manager.updater.download_and_apply(
                    download_url=download_url,
                    on_progress=self._on_progress,
                    on_log=self._on_log
                )

        threading.Thread(target=worker, daemon=True).start()
        return True


