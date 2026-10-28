import threading
import json
import platform
import sys
import time
from typing import List, Dict, Any, Optional
from core.models import PackageInfo, SearchResult, OperationStatus
from core.manager import PackageManager
from core.admin import is_admin

class WebAPI:
    """Python bridge exposed to JavaScript via pywebview."""

    def __init__(self, manager: PackageManager):
        self.manager = manager
        self._window = None
        self._last_progress_time = 0.0

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
        now = time.time()
        # Cap IPC evaluation to at most 16 calls per second unless completing
        if (now - self._last_progress_time < 0.06) and (pct not in (0.0, 1.0, None)):
            return
        self._last_progress_time = now

        p_val = pct if pct is not None else "null"
        escaped_ticker = json.dumps(ticker)
        self._eval_js(f"app.setIslandProgress({p_val}, {escaped_ticker});")

    def scan_updates(self, *args, **kwargs) -> List[Dict[str, Any]]:
        """Scans system for available updates synchronously for JS call."""
        if self.manager.is_busy and self.manager.cached_updates:
            return [
                {
                    "id": p.id,
                    "name": p.name,
                    "installed_version": p.installed_version,
                    "available_version": p.available_version,
                    "source": p.source
                }
                for p in self.manager.cached_updates
            ]

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

    def upgrade_packages(self, package_ids: Optional[List[str]] = None, *args, **kwargs) -> List[Dict[str, Any]]:
        """Upgrades packages and returns list of OperationStatus dictionaries."""
        if package_ids is None and args and isinstance(args[0], list):
            package_ids = args[0]
        if not package_ids and "packageIds" in kwargs:
            package_ids = kwargs["packageIds"]
        if not package_ids and "package_ids" in kwargs:
            package_ids = kwargs["package_ids"]
        if isinstance(package_ids, str):
            package_ids = [package_ids]
        package_ids = package_ids or []

        if not package_ids:
            return []

        engine_id = kwargs.get("engine")
        if engine_id and engine_id in self.manager.engines:
            self.manager.set_active_engine(engine_id)

        engine = self.manager.current_engine
        results = []

        for pid in package_ids:
            try:
                silent = self.manager.settings.get("silent_install", True)
                agree = self.manager.settings.get("accept_agreements", True)
                status = engine.upgrade_package(
                    package_id=pid,
                    on_log=self._on_log,
                    on_progress=self._on_progress,
                    silent=silent,
                    accept_agreements=agree
                )
                results.append({
                    "package_id": status.package_id,
                    "success": status.success,
                    "message": status.message,
                    "exit_code": getattr(status, "exit_code", 0)
                })
            except Exception as e:
                self._on_log(f"[Upgrade Exception {pid}]: {e}\n")
                results.append({
                    "package_id": pid,
                    "success": False,
                    "message": str(e),
                    "exit_code": -1
                })

        return results

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

    def one_click_clean(self, *args, **kwargs) -> Dict[str, Any]:
        """Perform one-click clean of all safe low-risk categories."""
        if not self.manager.cleaner:
            return {"total_freed_bytes": 0, "total_freed_str": "0 B", "files_removed": 0, "error": "Cleaner not available"}

        categories = self.manager.cleaner.get_category_definitions()
        safe_ids = [c["id"] for c in categories if c.get("risk") == "low"]

        return self.manager.cleaner.clean(
            category_ids=safe_ids,
            on_progress=self._on_progress,
            on_log=self._on_log
        )

    def execute_cleanup(self, categories: Optional[List[str]] = None, *args, **kwargs) -> Dict[str, Any]:
        """Executes cleanup sequence across selected categories with live progress."""
        if categories is None and args and isinstance(args[0], list):
            categories = args[0]
        categories = categories or []

        if not self.manager.cleaner:
            return {"total_freed_bytes": 0, "total_freed_str": "0 B", "files_removed": 0, "error": "Cleaner not available"}

        return self.manager.cleaner.clean(
            category_ids=categories,
            on_progress=self._on_progress,
            on_log=self._on_log
        )

    def inspect_cleanup_category(self, category_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        """Inspect and trace specific files within a cleanup category."""
        if not category_id and args and isinstance(args[0], str):
            category_id = args[0]
        if not category_id and "categoryId" in kwargs:
            category_id = kwargs["categoryId"]
        if not category_id and "category_id" in kwargs:
            category_id = kwargs["category_id"]

        if self.manager.cleaner and hasattr(self.manager.cleaner, "inspect_category"):
            return self.manager.cleaner.inspect_category(category_id)
        return {"category_id": category_id, "name": "Unknown", "files": [], "total_files": 0, "total_size_bytes": 0, "total_size_str": "0 B"}

    def get_cleanup_plan(self, categories: Optional[List[str]] = None, *args, **kwargs) -> Dict[str, Any]:
        """Generate structured cleanup plan requiring user consent."""
        if categories is None and args and isinstance(args[0], list):
            categories = args[0]
        if not categories and "categories" in kwargs:
            categories = kwargs["categories"]

        if self.manager.cleaner and hasattr(self.manager.cleaner, "get_cleanup_plan"):
            return self.manager.cleaner.get_cleanup_plan(categories)
        return {"requires_consent": False, "categories": [], "total_size_str": "0 B", "total_files": 0}

    def reveal_in_explorer(self, package_id: str = "", path: str = "", *args, **kwargs) -> Dict[str, Any]:
        """Reveal file or package directory in Windows File Explorer."""
        import subprocess
        if not package_id and args and isinstance(args[0], str):
            package_id = args[0]
        if not path and "path" in kwargs:
            path = kwargs["path"]

        target = path
        if not target or not os.path.exists(target):
            # Try to find standard installation path
            prog_files = os.environ.get("ProgramFiles", "C:\\Program Files")
            prog_files_x86 = os.environ.get("ProgramFiles(x86)", "C:\\Program Files (x86)")
            local_appdata = os.environ.get("LOCALAPPDATA", "")
            for base in [prog_files, prog_files_x86, os.path.join(local_appdata, "Programs")]:
                for entry in (os.listdir(base) if os.path.exists(base) else []):
                    if package_id.lower() in entry.lower():
                        target = os.path.join(base, entry)
                        break
                if target:
                    break

        if target and os.path.exists(target):
            try:
                if os.path.isfile(target):
                    subprocess.Popen(f'explorer.exe /select,"{os.path.abspath(target)}"')
                else:
                    subprocess.Popen(f'explorer.exe "{os.path.abspath(target)}"')
                return {"success": True, "path": target}
            except Exception as e:
                return {"success": False, "error": str(e)}

        try:
            subprocess.Popen('explorer.exe shell:AppsFolder')
            return {"success": True, "path": "shell:AppsFolder"}
        except Exception as e:
            return {"success": False, "error": str(e)}

    def reveal_in_finder(self, package_id: str = "", path: str = "", *args, **kwargs) -> Dict[str, Any]:
        """Backward-compatible alias for reveal_in_explorer."""
        return self.reveal_in_explorer(package_id, path, *args, **kwargs)

    def optimize_memory(self, *args, **kwargs) -> Dict[str, Any]:
        """Compact memory and purge inactive RAM working sets."""
        if self.manager.cleaner and hasattr(self.manager.cleaner, "optimize_memory"):
            return self.manager.cleaner.optimize_memory()
        return {"freed_bytes": 0, "freed_str": "0 B"}

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


