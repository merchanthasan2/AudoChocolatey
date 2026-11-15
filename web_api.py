import threading
import json
import os
import platform
import shutil
import subprocess
import sys
import time
from typing import List, Dict, Any, Optional
from core.models import PackageInfo, SearchResult, OperationStatus
from core.manager import PackageManager
from core.admin import is_admin

_SERIAL_PLACEHOLDERS = frozenset({
    "",
    "0",
    "none",
    "not applicable",
    "not specified",
    "default string",
    "system serial number",
    "to be filled by o.e.m.",
    "to be filled by o.e.m",
})


def _diag_format_bytes(size_bytes: int) -> str:
    if size_bytes < 1024:
        return f"{size_bytes} B"
    if size_bytes < 1024 * 1024:
        return f"{size_bytes / 1024:.1f} KB"
    if size_bytes < 1024 * 1024 * 1024:
        return f"{size_bytes / (1024 * 1024):.1f} MB"
    return f"{size_bytes / (1024 * 1024 * 1024):.2f} GB"


def _windows_registry_value(path: str, value_name: str) -> str:
    if platform.system() != "Windows":
        return ""
    try:
        import winreg
    except ImportError:
        return ""
    try:
        access = winreg.KEY_READ | getattr(winreg, "KEY_WOW64_64KEY", 0)
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, path, 0, access) as key:
            raw, _ = winreg.QueryValueEx(key, value_name)
            if raw is None:
                return ""
            return str(raw).strip()
    except OSError:
        return ""


def _normalize_serial(raw: str) -> str:
    text = (raw or "").strip()
    if text.casefold() in _SERIAL_PLACEHOLDERS:
        return ""
    return text


def _windows_physical_memory_str() -> str:
    if platform.system() != "Windows":
        return ""
    try:
        import ctypes
        from ctypes import wintypes

        class MEMORYSTATUSEX(ctypes.Structure):
            _fields_ = [
                ("dwLength", wintypes.DWORD),
                ("dwMemoryLoad", wintypes.DWORD),
                ("ullTotalPhys", ctypes.c_ulonglong),
                ("ullAvailPhys", ctypes.c_ulonglong),
                ("ullTotalPageFile", ctypes.c_ulonglong),
                ("ullAvailPageFile", ctypes.c_ulonglong),
                ("ullTotalVirtual", ctypes.c_ulonglong),
                ("ullAvailVirtual", ctypes.c_ulonglong),
                ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
            ]

        status = MEMORYSTATUSEX()
        status.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
        if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)):
            return ""
        total = int(status.ullTotalPhys)
        if total <= 0:
            return ""
        gib = (total + (1 << 30) - 1) >> 30
        return f"{gib} GB"
    except Exception:
        return ""


def _windows_storage_facts() -> Dict[str, Any]:
    if platform.system() != "Windows":
        return {
            "storage_total_str": "",
            "storage_free_str": "",
            "storage_used_percent": 0,
        }
    system_drive = os.environ.get("SystemDrive", "C:")
    root = f"{system_drive}\\"
    try:
        usage = shutil.disk_usage(root)
    except OSError:
        return {
            "storage_total_str": "",
            "storage_free_str": "",
            "storage_used_percent": 0,
        }
    total = int(usage.total)
    free = int(usage.free)
    used = max(0, total - free)
    used_pct = int(round((used / total) * 100)) if total > 0 else 0
    return {
        "storage_total_str": _diag_format_bytes(total),
        "storage_free_str": _diag_format_bytes(free),
        "storage_used_percent": used_pct,
    }


def _windows_nt_build_number() -> int:
    build_raw = _windows_registry_value(
        r"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
        "CurrentBuildNumber",
    )
    if not build_raw:
        return 0
    try:
        return int(str(build_raw).split(".", 1)[0])
    except ValueError:
        return 0


def _windows_product_display() -> str:
    product = _windows_registry_value(
        r"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
        "ProductName",
    )
    if product and _windows_nt_build_number() >= 22000 and product.startswith("Windows 10"):
        product = "Windows 11" + product[len("Windows 10") :]
    if product:
        return product
    release = (platform.release() or "").strip()
    if release.isdigit() and int(release) >= 10:
        return f"Windows {release}"
    return release or "Windows"


def _windows_hardware_facts() -> Dict[str, Any]:
    from core.hardware_facts import collect_windows_hardware, flat_legacy_from_hardware

    hardware = collect_windows_hardware(deadline=2.8)
    return {**flat_legacy_from_hardware(hardware), "_hardware_struct": hardware}


def _empty_hardware_facts() -> Dict[str, Any]:
    return {
        "model_name": "",
        "model_id": "",
        "serial_number": "",
        "cpu": "",
        "memory": "",
        "os_display": "",
        "os_info": "",
        "arch": platform.machine() or "",
        "storage_total_str": "",
        "storage_free_str": "",
        "storage_used_percent": 0,
        "camera": "",
        "display": "",
    }


class WebAPI(__import__("web_api_tools").SystemToolsAPI):
    """Python bridge exposed to JavaScript via pywebview."""

    def __init__(self, manager: PackageManager):
        self.manager = manager
        self._window = None
        self._last_progress_time = 0.0
        self._register_tool_scan_jobs()

    def _register_tool_scan_jobs(self) -> None:
        orch = getattr(self.manager, "scan_orchestrator", None)
        if orch is None:
            return
        from web_api_tools import register_system_tool_scan_jobs
        from core.system_tools.cleanup import register_cleanup_scan_job

        register_system_tool_scan_jobs(orch)
        cleaner = getattr(self.manager, "cleaner", None)
        engine = getattr(cleaner, "_engine", None) if cleaner else None
        if engine is not None:
            register_cleanup_scan_job(orch, engine)

    def set_window(self, window):
        self._window = window

    def _eval_js(self, js_code: str):
        if self._window:
            try:
                self._window.evaluate_js(js_code)
            except Exception:
                pass

    def _on_log(self, text: str):
        # Do not wait on the page here. The scan call is still outstanding, and a
        # blocking script evaluation holds the result until the wait gives up.
        escaped = json.dumps(text)
        script = f"app.appendLog({escaped});"

        def send():
            self._eval_js(script)

        threading.Thread(target=send, daemon=True).start()

    def _on_progress(self, pct: Optional[float], ticker: str):
        now = time.time()
        if (now - self._last_progress_time < 0.06) and (pct not in (0.0, 1.0, None)):
            return
        self._last_progress_time = now

        p_val = pct if pct is not None else "null"
        escaped_ticker = json.dumps(ticker)
        script = (
            f"app.setIslandProgress({p_val}, {escaped_ticker});"
            f"if (app.onProgressUpdate) app.onProgressUpdate({p_val}, {escaped_ticker});"
        )

        def send():
            self._eval_js(script)

        threading.Thread(target=send, daemon=True).start()

    def _estimate_package_size_bytes(self, pkg_id: str, pkg_name: str) -> int:
        pid = (pkg_id or "").lower().strip()
        pname = (pkg_name or "").lower().strip()
        table = {
            "git.git": 58 * 1024 * 1024,
            "github.cli": 18 * 1024 * 1024,
            "github.githubdesktop": 148 * 1024 * 1024,
            "xai.grokbot": 65 * 1024 * 1024,
            "amazon.kiro": 95 * 1024 * 1024,
            "microsoft.dotnet.desktopruntime.9": 56 * 1024 * 1024,
            "microsoft.dotnet.desktopruntime.10": 58 * 1024 * 1024,
            "mobatek.mobaxterm": 32 * 1024 * 1024,
            "obsproject.obsstudio": 132 * 1024 * 1024,
            "shininglight.openssl.light": 4800 * 1024,
            "microsoft.visualstudio.community": 2400 * 1024 * 1024,
            "microsoft.wsl": 124 * 1024 * 1024,
            "google.chrome": 112 * 1024 * 1024,
            "mozilla.firefox": 65 * 1024 * 1024,
            "brave.brave": 118 * 1024 * 1024,
            "microsoft.edge": 160 * 1024 * 1024,
            "microsoft.visualstudiocode": 98 * 1024 * 1024,
            "docker.dockerdesktop": 580 * 1024 * 1024,
            "python.python.3.13": 26 * 1024 * 1024,
            "python.python.3": 26 * 1024 * 1024,
            "openjs.nodejs.lts": 34 * 1024 * 1024,
            "7zip.7zip": 1900 * 1024,
            "rarlab.winrar": 3800 * 1024,
            "microsoft.powertoys": 188 * 1024 * 1024,
            "voidtools.everything": 2100 * 1024,
            "bitwarden.bitwarden": 98 * 1024 * 1024,
            "spotify.spotify": 88 * 1024 * 1024,
            "videolan.vlc": 44 * 1024 * 1024,
            "discord.discord": 92 * 1024 * 1024,
            "slacktechnologies.slack": 118 * 1024 * 1024,
            "telegram.telegramdesktop": 52 * 1024 * 1024,
            "whatsapp.whatsapp": 142 * 1024 * 1024,
            "zoom.zoom": 68 * 1024 * 1024,
            "microsoft.teams": 155 * 1024 * 1024,
            "notion.notion": 96 * 1024 * 1024,
            "obsidian.obsidian": 86 * 1024 * 1024,
            "notepad++.notepad++": 4600 * 1024,
            "adobe.acrobat.reader.64-bit": 320 * 1024 * 1024,
            "figma.figma": 112 * 1024 * 1024,
            "blenderfoundation.blender": 380 * 1024 * 1024
        }
        for k, v in table.items():
            if k in pid or (pname and k in pname):
                return v
        h = abs(hash(pid))
        return (35 + (h % 85)) * 1024 * 1024

    def _estimate_package_size_str(self, pkg_id: str, pkg_name: str) -> str:
        b = self._estimate_package_size_bytes(pkg_id, pkg_name)
        mb = b / (1024 * 1024)
        if mb >= 1024:
            return f"{mb / 1024:.1f} GB"
        if mb >= 10:
            return f"{mb:.1f} MB"
        return f"{mb:.1f} MB"

    def _estimate_package_ete_seconds(self, pkg_id: str, pkg_name: str) -> int:
        b = self._estimate_package_size_bytes(pkg_id, pkg_name)
        mb = b / (1024 * 1024)
        # Average broadband at ~14 MB/s + 3s execution
        return max(4, int(mb / 14) + 3)

    def _estimate_package_ete_str(self, pkg_id: str, pkg_name: str) -> str:
        sec = self._estimate_package_ete_seconds(pkg_id, pkg_name)
        if sec < 60:
            return f"~{sec}s"
        m = sec // 60
        s = sec % 60
        return f"~{m}m {s}s" if s else f"~{m}m"

    def _enrich_package_dict(self, p: PackageInfo) -> Dict[str, Any]:
        size_str = getattr(p, "size_str", "") or self._estimate_package_size_str(p.id, p.name)
        size_bytes = getattr(p, "size_bytes", 0) or self._estimate_package_size_bytes(p.id, p.name)
        ete_str = getattr(p, "ete_str", "") or self._estimate_package_ete_str(p.id, p.name)
        ete_seconds = getattr(p, "ete_seconds", 0) or self._estimate_package_ete_seconds(p.id, p.name)
        return {
            "id": p.id,
            "name": p.name,
            "installed_version": p.installed_version,
            "available_version": p.available_version,
            "source": p.source,
            "size_str": size_str,
            "size_bytes": size_bytes,
            "ete_str": ete_str,
            "ete_seconds": ete_seconds
        }

    def bridge_ping(self, *args, **kwargs) -> Dict[str, Any]:
        from core.updater import CURRENT_VERSION

        return {"ok": True, "version": CURRENT_VERSION}

    def scans_start_all(self, *args, **kwargs) -> Dict[str, Any]:
        orch = self.manager.scan_orchestrator
        if orch is None:
            return {"started": False}
        force = bool(kwargs.get("force") or (args[0] if args else False))
        orch.scans_start_all(force=force)
        return {"started": True}

    def scans_start_maintenance(self, *args, **kwargs) -> Dict[str, Any]:
        orch = self.manager.scan_orchestrator
        if orch is None:
            return {"started": False}
        job_ids = kwargs.get("job_ids")
        if job_ids is None and args:
            job_ids = args[0]
        if isinstance(job_ids, str):
            job_ids = [job_ids]
        force = bool(kwargs.get("force"))
        orch.scans_start_maintenance(job_ids, force=force)
        return {"started": True, "job_ids": list(job_ids or [])}

    def scan_start(self, job_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not job_id and args:
            job_id = str(args[0])
        if not job_id and kwargs.get("job_id"):
            job_id = str(kwargs["job_id"])
        orch = self.manager.scan_orchestrator
        if orch is None or not job_id:
            return {"started": False, "job_id": job_id}
        force = bool(kwargs.get("force") or (args[1] if len(args) > 1 else False))
        orch.scan_start(job_id, force=force)
        return {"started": True, "job_id": job_id}

    def scan_cancel(self, job_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not job_id and args:
            job_id = str(args[0])
        orch = self.manager.scan_orchestrator
        if orch is None or not job_id:
            return {"cancelled": False, "job_id": job_id}
        orch.scan_cancel(job_id)
        return {"cancelled": True, "job_id": job_id}

    def scans_state(self, *args, **kwargs) -> Dict[str, Any]:
        orch = self.manager.scan_orchestrator
        if orch is None:
            return {"jobs": {}, "summary": {"done": 0, "total": 0, "eta_s": 0, "running": 0}}
        return orch.scans_state()

    def scan_result(self, job_id: str = "", *args, **kwargs) -> Any:
        if not job_id and args:
            job_id = str(args[0])
        orch = self.manager.scan_orchestrator
        if orch is None or not job_id:
            return None
        return orch.scan_result(job_id)

    def scan_updates(self, *args, **kwargs) -> List[Dict[str, Any]]:
        """Scans system for available updates synchronously for JS call."""
        orch = self.manager.scan_orchestrator
        if orch is not None:
            rec = orch.wait_for_job("catalog-updates", budget_s=60.0)
            if rec.get("status") == "ready":
                packages = list(self.manager.cached_updates)
            elif rec.get("cached"):
                raw = orch.scan_result("catalog-updates") or []
                packages = orch._dicts_to_packages(raw) if raw else list(self.manager.cached_updates)
            else:
                err = rec.get("error") or getattr(self.manager.current_engine, "last_error", "") or ""
                raise RuntimeError(self._scan_failure_message(err or "update scan failed"))
            if not packages:
                err = getattr(self.manager.current_engine, "last_error", "") or ""
                if err:
                    raise RuntimeError(self._scan_failure_message(err))
            return [self._enrich_package_dict(p) for p in packages]

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
            on_error=on_error,
        )

        finished = event.wait(timeout=120)
        if error_box:
            raise RuntimeError(self._scan_failure_message(str(error_box[0])))
        if not finished:
            raise RuntimeError(
                "The update scan took too long and was stopped. Use Scan for Updates to try again."
            )

        packages = [self._enrich_package_dict(p) for p in result_box]
        if not packages:
            err = getattr(self.manager.current_engine, "last_error", "") or ""
            if err:
                raise RuntimeError(self._scan_failure_message(err))
        return packages

    def _scan_failure_message(self, detail: str) -> str:
        text = (detail or "").lower()
        if "timed out" in text or "timeout" in text:
            return "The catalog did not answer in time. Use Scan for Updates to try again."
        return "The update scan failed. Use Scan for Updates to try again."

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
                    "exit_code": getattr(status, "exit_code", 0),
                    "reason": getattr(status, "reason", "") or "",
                    "holders": list(getattr(status, "holders", None) or []),
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
        orch = self.manager.scan_orchestrator
        if orch is not None:
            orch.scan_start("local-arp")
            orch.scan_start("catalog-installed")
            from core.scan_orchestrator import ScanOrchestrator

            cat = orch.scan_result("catalog-installed") or []
            arp = orch.scan_result("local-arp") or []
            merged = ScanOrchestrator.merge_installed(cat, arp)
            return [
                {
                    "id": row.get("id", ""),
                    "name": row.get("name", ""),
                    "installed_version": row.get("installed_version", ""),
                    "source": row.get("source", ""),
                    "origin": row.get("origin", "catalog"),
                    "can_uninstall": row.get("can_uninstall", True),
                    "engine": row.get("engine", "winget"),
                }
                for row in merged
            ]

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
            on_error=on_error,
        )

        event.wait(timeout=180)
        return [
            {
                "id": p.id,
                "name": p.name,
                "installed_version": p.installed_version,
                "source": p.source,
                "origin": getattr(p, "origin", "catalog"),
                "can_uninstall": getattr(p, "can_uninstall", True),
                "engine": p.engine,
            }
            for p in result_box
        ]

    def prefetch_package(self, package_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not package_id and args and isinstance(args[0], str):
            package_id = args[0]
        if not package_id and kwargs.get("package_id"):
            package_id = str(kwargs["package_id"])
        if not package_id:
            return {
                "id": "",
                "success": False,
                "message": "Package id is required.",
                "path": "",
            }

        status = self.manager.prefetch_package(package_id, on_log=self._on_log)
        path = ""
        if status.success:
            path = os.path.join(
                os.environ.get("LOCALAPPDATA", ""),
                "QuantumUpdater",
                "downloads",
                package_id,
            )
        return {
            "id": package_id,
            "success": status.success,
            "message": status.message,
            "path": path,
        }

    def launch_downloaded_installer(self, package_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not package_id and args and isinstance(args[0], str):
            package_id = args[0]
        if not package_id and kwargs.get("package_id"):
            package_id = str(kwargs["package_id"])
        if not package_id and kwargs.get("packageId"):
            package_id = str(kwargs["packageId"])
        if not package_id:
            return {
                "success": False,
                "message": "Package id is required.",
                "path": "",
            }

        local_appdata = os.environ.get("LOCALAPPDATA", "")
        downloads_root = os.path.join(local_appdata, "QuantumUpdater", "downloads")
        dest_dir = os.path.join(downloads_root, package_id)
        dest_real = os.path.realpath(dest_dir)
        root_real = os.path.realpath(downloads_root)
        dest_cmp = os.path.normcase(dest_real)
        root_cmp = os.path.normcase(root_real)

        try:
            under_downloads = os.path.commonpath([dest_cmp, root_cmp]) == root_cmp
        except ValueError:
            under_downloads = False
        if not under_downloads or dest_cmp == root_cmp:
            return {
                "success": False,
                "message": "That package id is not allowed.",
                "path": "",
            }

        if not os.path.isdir(dest_real):
            return {
                "success": False,
                "message": "No downloaded installer folder was found for this package.",
                "path": dest_real,
            }

        allowed = {".exe", ".msi", ".msix", ".appx", ".msixbundle"}
        refused = {".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".wsh"}
        preference = (".exe", ".msi", ".msix", ".appx", ".msixbundle")
        matches: List[str] = []

        try:
            names = os.listdir(dest_real)
        except OSError:
            names = []
        for name in names:
            candidate = os.path.join(dest_real, name)
            try:
                cand_real = os.path.realpath(candidate)
            except OSError:
                continue
            if not os.path.isfile(cand_real):
                continue
            cand_cmp = os.path.normcase(cand_real)
            try:
                stays = os.path.commonpath([cand_cmp, dest_cmp]) == dest_cmp
            except ValueError:
                stays = False
            if not stays:
                continue
            ext = os.path.splitext(cand_real)[1].lower()
            if ext in refused or ext not in allowed:
                continue
            matches.append(cand_real)

        if not matches:
            return {
                "success": False,
                "message": "No allowed installer was found in the download folder.",
                "path": dest_real,
            }

        if len(matches) > 1:
            names_list = ", ".join(os.path.basename(p) for p in matches)
            return {
                "success": False,
                "message": (
                    "More than one installer is in the download folder. "
                    f"Remove the extras, then try again. Found: {names_list}."
                ),
                "path": dest_real,
            }

        chosen = matches[0]
        # Prefer the preferred extension order when exactly one match already chosen.
        for ext in preference:
            for path in matches:
                if os.path.splitext(path)[1].lower() == ext:
                    chosen = path
                    break
            else:
                continue
            break

        try:
            ext = os.path.splitext(chosen)[1].lower()
            if os.name == "nt" and ext != ".exe":
                os.startfile(chosen)
            else:
                subprocess.Popen([chosen], shell=False)
        except Exception as err:
            return {
                "success": False,
                "message": str(err),
                "path": chosen,
            }

        file_name = os.path.basename(chosen)
        return {
            "success": True,
            "message": f"Started {file_name}.",
            "path": chosen,
        }

    def uninstall_package(self, package_id: str = "", *args, **kwargs):
        if not package_id and args and isinstance(args[0], str):
            package_id = args[0]
        if package_id.startswith("arp:"):
            from core.arp_inventory import find_arp_entry, plan_uninstall

            entry = find_arp_entry(package_id)
            if not entry:
                return {"success": False, "message": "Registry app not found."}
            plan = plan_uninstall(entry)
            if not plan:
                return {
                    "success": False,
                    "message": "This app has no safe uninstall command.",
                }
            argv = [plan["exe"]] + list(plan.get("args") or [])
            cwd = plan.get("cwd")
            quiet_msi = os.path.basename(plan["exe"]).lower() == "msiexec.exe"
            try:
                self.manager._install_semaphore.acquire()
                startupinfo = None
                flags = 0
                # A quiet Windows Installer can stay hidden. Any other uninstaller needs its window.
                if os.name == "nt" and quiet_msi:
                    startupinfo = subprocess.STARTUPINFO()
                    startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                    startupinfo.wShowWindow = 0
                    flags = 0x08000000
                proc = subprocess.run(
                    argv,
                    cwd=cwd,
                    shell=False,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    timeout=1800,
                    startupinfo=startupinfo,
                    creationflags=flags,
                )
                ok = proc.returncode in (0, 3010)
                detail = (proc.stdout or proc.stderr or "").strip()
                if proc.returncode == 3010:
                    msg = "Uninstall completed. Windows may ask for a restart."
                elif ok:
                    msg = "Uninstall completed successfully."
                elif proc.returncode in (1602, 1223):
                    msg = "Nothing was removed."
                else:
                    msg = detail or f"Uninstall exited with code {proc.returncode}."
                return {"success": ok, "message": msg}
            except subprocess.TimeoutExpired:
                return {"success": False, "message": "Uninstall timed out."}
            except Exception as exc:
                return {"success": False, "message": str(exc)}
            finally:
                self.manager._install_semaphore.release()

        event = threading.Event()
        result_box = []

        def on_success(status: OperationStatus):
            result_box.append(status)
            event.set()

        def on_error(err: Exception):
            result_box.append(OperationStatus(package_id, False, str(err)))
            event.set()

        self.manager.uninstall_package_async(
            package_id=package_id,
            on_log=self._on_log,
            on_progress=self._on_progress,
            on_success=on_success,
            on_error=on_error
        )
        event.wait(timeout=180)
        status = result_box[0] if result_box else OperationStatus(package_id, False, "Uninstall timed out.")
        return {"success": status.success, "message": status.message}

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

    def get_settings(self, *args, **kwargs) -> Dict[str, Any]:
        return dict(self.manager.settings or {})

    def cleanup_scan_start(self, *args, **kwargs) -> Dict[str, Any]:
        cleaner = self.manager.cleaner
        if cleaner and hasattr(cleaner, "scan_start"):
            return cleaner.scan_start()
        if cleaner:
            result = cleaner.scan()
            return {"started": True, "scan_id": result.get("scan_id"), "result": result, "status": "done"}
        return {"started": False, "error": "Cleaner not available"}

    def cleanup_scan_status(self, *args, **kwargs) -> Dict[str, Any]:
        cleaner = self.manager.cleaner
        if cleaner and hasattr(cleaner, "scan_status"):
            return cleaner.scan_status()
        return {"status": "idle"}

    def cleanup_scan_cancel(self, *args, **kwargs) -> Dict[str, Any]:
        cleaner = self.manager.cleaner
        if cleaner and hasattr(cleaner, "scan_cancel"):
            return cleaner.scan_cancel()
        return {"cancelled": False}

    def cleanup_get_plan(self, categories: Optional[List[str]] = None, *args, **kwargs) -> Dict[str, Any]:
        if categories is None and args and isinstance(args[0], list):
            categories = args[0]
        if not categories and "categories" in kwargs:
            categories = kwargs["categories"]
        categories = categories or []
        cleaner = self.manager.cleaner
        if cleaner and hasattr(cleaner, "cleanup_get_plan"):
            return cleaner.cleanup_get_plan(categories)
        return {"error": "Cleaner not available", "token": None}

    def cleanup_execute(self, token: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not token and args and isinstance(args[0], str):
            token = args[0]
        if not token:
            token = kwargs.get("token", "")
        cleaner = self.manager.cleaner
        if cleaner and hasattr(cleaner, "cleanup_execute"):
            return cleaner.cleanup_execute(token, kwargs.get("category_ids"))
        return {"error": "Cleaner not available"}

    def cleanup_inspect(self, category_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not category_id and args and isinstance(args[0], str):
            category_id = args[0]
        if not category_id:
            category_id = kwargs.get("category_id") or kwargs.get("categoryId") or ""
        cleaner = self.manager.cleaner
        if cleaner and hasattr(cleaner, "inspect_category"):
            return cleaner.inspect_category(category_id)
        return {"category_id": category_id, "name": "Unknown", "files": [], "total_files": 0}

    def cleanup_drives(self, *args, **kwargs) -> Dict[str, Any]:
        from core.system_tools.drives import enumerate_cleanup_volumes, list_physical_drives

        try:
            volumes = enumerate_cleanup_volumes(include_external=True)
            hardware = list_physical_drives()
            return {"volumes": volumes, "physical_drives": hardware}
        except Exception as exc:
            return {"volumes": [], "physical_drives": [], "error": str(exc)}

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
        return self.cleanup_inspect(category_id, *args, **kwargs)

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
                real = os.path.realpath(target)
                if any(ch in real for ch in ('"', "\n", "\r")):
                    return {"success": False, "error": "That path cannot be opened."}
                if os.path.isfile(real):
                    subprocess.Popen(["explorer.exe", f"/select,{real}"], shell=False)
                else:
                    subprocess.Popen(["explorer.exe", real], shell=False)
                return {"success": True, "path": real}
            except Exception as e:
                return {"success": False, "error": str(e)}

        try:
            subprocess.Popen(["explorer.exe", "shell:AppsFolder"], shell=False)
            return {"success": True, "path": "shell:AppsFolder"}
        except Exception as e:
            return {"success": False, "error": str(e)}

    def reveal_in_finder(self, package_id: str = "", path: str = "", *args, **kwargs) -> Dict[str, Any]:
        """Backward-compatible alias for reveal_in_explorer."""
        return self.reveal_in_explorer(package_id, path, *args, **kwargs)

    def optimize_memory(self, *args, **kwargs) -> Dict[str, Any]:
        """ADR 012: measure available RAM before/after EmptyWorkingSet trims."""
        from core.memory_trim import optimize_ram_trim

        return optimize_ram_trim()

    def catalog_sources_refresh(self, *args, **kwargs) -> Dict[str, Any]:
        """31.1: everyday catalog repair — winget source update."""
        eng = self.manager.engines.get("winget")
        if not eng or not eng.is_available():
            return {"ok": False, "error": "WinGet is not available on this PC."}
        return eng.refresh_sources()

    def catalog_sources_reset(self, *args, **kwargs) -> Dict[str, Any]:
        """31.1: recovery only — winget source reset --force. Requires confirmed=true."""
        confirmed = bool(kwargs.get("confirmed", False))
        if args and not confirmed:
            confirmed = bool(args[0])
        eng = self.manager.engines.get("winget")
        if not eng or not eng.is_available():
            return {"ok": False, "error": "WinGet is not available on this PC."}
        return eng.reset_sources(confirmed=confirmed)

    def store_apps_list(self, *args, **kwargs) -> Dict[str, Any]:
        """31.8: list AppX packages installed for this Windows account."""
        from core.system_tools.store_apps import list_store_packages

        return list_store_packages()

    def store_apps_reregister(self, *args, **kwargs) -> Dict[str, Any]:
        """31.8: re-register one listed Store app. Requires confirmed=true."""
        from core.system_tools.store_apps import reregister_store_package

        package_full_name = str(kwargs.get("package_full_name") or "").strip()
        if not package_full_name and args:
            package_full_name = str(args[0] or "").strip()
        confirmed = bool(kwargs.get("confirmed", False))
        if len(args) > 1 and not confirmed:
            confirmed = bool(args[1])
        return reregister_store_package(package_full_name, confirmed=confirmed)

    def print_jobs_list(self, *args, **kwargs) -> Dict[str, Any]:
        """31.10: list jobs across local printers."""
        from core.system_tools.print_spooler import list_print_jobs

        return list_print_jobs()

    def print_job_cancel(self, *args, **kwargs) -> Dict[str, Any]:
        """31.10: cancel one listed job. Requires confirmed=true."""
        from core.system_tools.print_spooler import cancel_print_job

        printer = str(kwargs.get("printer") or "").strip()
        job_id = str(kwargs.get("job_id") or kwargs.get("id") or "").strip()
        if args:
            if not printer:
                printer = str(args[0] or "").strip()
            if len(args) > 1 and not job_id:
                job_id = str(args[1] or "").strip()
        confirmed = bool(kwargs.get("confirmed", False))
        if len(args) > 2 and not confirmed:
            confirmed = bool(args[2])
        return cancel_print_job(printer, job_id, confirmed=confirmed)

    def print_queue_clear(self, *args, **kwargs) -> Dict[str, Any]:
        """31.10: machine-wide spooler clear + restart. Requires confirmed=true."""
        from core.system_tools.print_spooler import clear_print_queue_and_restart_spooler

        confirmed = bool(kwargs.get("confirmed", False))
        if args and not confirmed:
            confirmed = bool(args[0])
        return clear_print_queue_and_restart_spooler(confirmed=confirmed)

    def get_app_icons(self, *args, **kwargs) -> Dict[str, Optional[str]]:
        """Return data URLs for package icons. Missing icons are null, never an error."""
        ids = kwargs.get("ids")
        if ids is None and args:
            ids = args[0]
        if isinstance(ids, str):
            ids = [ids]
        package_ids = [str(item) for item in (ids or []) if str(item).strip()]
        homepages = kwargs.get("homepages")
        if homepages is None and len(args) > 1:
            homepages = args[1]
        if not isinstance(homepages, dict):
            homepages = {}
        installed_meta = kwargs.get("installed_meta") or kwargs.get("meta")
        if installed_meta is None and len(args) > 2:
            installed_meta = args[2]
        if not isinstance(installed_meta, dict):
            installed_meta = {}
        if not package_ids:
            return {}

        installed_inventory = [
            {"id": p.id, "name": p.name}
            for p in (getattr(self.manager, "cached_installed", None) or [])
        ]

        result_box: Dict[str, Optional[str]] = {}
        done = threading.Event()

        def worker() -> None:
            try:
                from core.app_icons import resolve_icons_batch

                result_box.update(
                    resolve_icons_batch(
                        package_ids,
                        homepages=homepages,
                        installed_meta=installed_meta,
                        installed_inventory=installed_inventory,
                        network_workers=4,
                    )
                )
            except Exception:
                for pid in package_ids:
                    result_box.setdefault(pid, None)
            finally:
                done.set()

        threading.Thread(target=worker, daemon=True).start()
        done.wait(timeout=180)
        for pid in package_ids:
            result_box.setdefault(pid, None)
        return result_box

    def get_diagnostics(self, *args, **kwargs) -> Dict[str, Any]:
        os_sys = platform.system()

        engines: List[Dict[str, Any]] = []
        hardware: Dict[str, Any] = {}
        legacy_facts: Dict[str, Any] = {}
        engine_box: Dict[str, Any] = {}

        def load_engines() -> None:
            try:
                engine_box["engines"] = self.manager.get_available_engines()
            except Exception:
                engine_box["engines"] = []

        engine_thread = threading.Thread(target=load_engines, daemon=True)
        engine_thread.start()
        if os_sys == "Windows":
            from core.hardware_facts import collect_windows_hardware, flat_legacy_from_hardware

            hw_started = time.perf_counter()
            hardware = collect_windows_hardware(deadline=2.5)
            legacy_facts = flat_legacy_from_hardware(hardware)
            remaining = max(0.0, 2.9 - (time.perf_counter() - hw_started))
            engine_thread.join(timeout=remaining)
        else:
            engine_thread.join(timeout=2.8)
        engines = engine_box.get("engines") or []
        if not engines:
            engines = [
                {
                    "id": eng_id,
                    "name": eng.name,
                    "available": eng.is_available(),
                    "version": "",
                }
                for eng_id, eng in self.manager.engines.items()
            ]

        winget_eng = self.manager.engines.get("winget")
        choco_eng = self.manager.engines.get("choco")
        brew_eng = self.manager.engines.get("brew")
        apple_eng = self.manager.engines.get("apple")

        diag = {
            "is_admin": is_admin(),
            "os_system": os_sys,
            "active_engine": self.manager.active_engine_id,
            "engines": engines,
        }
        if os_sys == "Windows":
            diag.update(legacy_facts)
            diag["hardware"] = hardware
            diag["os_system"] = "Windows"
        else:
            diag.update(_empty_hardware_facts())
            if os_sys == "Darwin":
                diag["os_display"] = "macOS"
                diag["os_info"] = f"macOS {platform.release()} ({platform.machine()})"

        if os_sys == "Darwin":
            diag["brew_path"] = str(getattr(brew_eng, "_brew_path", "Not Located")) if brew_eng else "Not Installed"
            diag["brew_version"] = str(brew_eng.get_version() or "Not Available") if brew_eng else "Not Available"
            diag["apple_version"] = str(apple_eng.get_version() or "Current") if apple_eng else "Current"
        else:
            ver_lookup = {
                str(item.get("id") or ""): str(item.get("version") or "")
                for item in engines
            }
            diag["winget_path"] = str(getattr(winget_eng, "_executable", "Not Located")) if winget_eng else "Not Installed"
            diag["winget_version"] = ver_lookup.get("winget") or "Not Available"
            diag["choco_path"] = str(getattr(choco_eng, "_executable", "Not Located")) if choco_eng else "Not Installed"
            diag["choco_version"] = ver_lookup.get("choco") or "Not Available"

        return diag

    def check_self_update(self, *args, **kwargs) -> Dict[str, Any]:
        """Check GitHub for new Quantum Updater releases."""
        if hasattr(self.manager, "updater") and self.manager.updater:
            return self.manager.updater.check_for_updates()
        return {"has_update": False, "current_version": "2.0.0"}

    def apply_self_update(self, download_url: str = "", *args, **kwargs) -> bool:
        """Install the latest official release. The caller's URL is ignored."""

        def worker():
            updater = getattr(self.manager, "updater", None)
            if not updater:
                return
            info = updater.check_for_updates()
            url = (info or {}).get("download_url") or ""
            if not info.get("has_update"):
                self._on_log("[Quantum Auto-Updater]: This copy is already current.\n")
                return
            updater.download_and_apply(
                download_url=url,
                on_progress=self._on_progress,
                on_log=self._on_log,
            )

        threading.Thread(target=worker, daemon=True).start()
        return True


