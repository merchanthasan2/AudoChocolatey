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

    def scan_updates(self) -> List[Dict[str, Any]]:
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

    def upgrade_packages(self, package_ids: List[str]) -> bool:
        """Upgrades multiple packages in background thread, sending live progress."""
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

    def search_catalog(self, query: str) -> List[Dict[str, Any]]:
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

    def install_package(self, package_id: str) -> Dict[str, Any]:
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

    def get_installed(self) -> List[Dict[str, Any]]:
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

    def uninstall_package(self, package_id: str) -> bool:
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

    def set_engine(self, engine_id: str) -> bool:
        self.manager.set_active_engine(engine_id)
        return True

    def save_setting(self, key: str, value: Any) -> bool:
        self.manager.settings[key] = value
        self.manager.save_settings()
        return True

    def get_diagnostics(self) -> Dict[str, Any]:
        engines = self.manager.get_available_engines()
        return {
            "is_admin": is_admin(),
            "os_info": f"{platform.system()} {platform.release()} (Build {platform.version().split('.')[-1] if '.' in platform.version() else platform.version()})",
            "arch": platform.machine(),
            "active_engine": self.manager.active_engine_id,
            "engines": engines,
            "winget_path": str(self.manager.engines["winget"]._executable or "Not Located"),
            "winget_version": str(self.manager.engines["winget"].get_version() or "Not Available"),
            "choco_path": str(self.manager.engines["choco"]._executable or "Not Located"),
            "choco_version": str(self.manager.engines["choco"].get_version() or "Not Available")
        }
