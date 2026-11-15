"""Cleanup scan/plan/execute engine (ADR 010–011)."""

from __future__ import annotations

import concurrent.futures
import ctypes
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import string
import threading
import time
import uuid
import winreg
from collections import defaultdict
from ctypes import wintypes
from typing import Any, Callable, Dict, List, Optional, Set, Tuple

from core.system_tools import safety
from core.system_tools.drives import enumerate_cleanup_volumes

PLAN_TTL_SECONDS = 15 * 60
SCAN_WORKERS = 4
SCAN_BUDGET_S = 120

WINDOWS_DEFERRED_CATEGORY_IDS = frozenset({
    "system_temp",
    "windows_minidump",
    "windows_update_cache",
})

WINDOWS_ADMIN_CATEGORY_IDS = WINDOWS_DEFERRED_CATEGORY_IDS | frozenset({
    "windows_component_cleanup",
    "delivery_optimization",
})


def human_size(size_bytes: int) -> str:
    if size_bytes < 1024:
        return f"{size_bytes} B"
    if size_bytes < 1024 * 1024:
        return f"{size_bytes / 1024:.1f} KB"
    if size_bytes < 1024 * 1024 * 1024:
        return f"{size_bytes / (1024 * 1024):.1f} MB"
    return f"{size_bytes / (1024 * 1024 * 1024):.2f} GB"


class SHQUERYRBINFO(ctypes.Structure):
    _fields_ = [
        ("cbSize", wintypes.DWORD),
        ("i64Size", ctypes.c_int64),
        ("i64NumItems", ctypes.c_int64),
    ]


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


def _scandir_size(
    root: str,
    *,
    cancel: Optional[Callable[[], bool]] = None,
    on_folder: Optional[Callable[[str], None]] = None,
    min_mtime: Optional[float] = None,
    max_depth: int = 64,
) -> Tuple[int, int, List[str]]:
    total_size = 0
    total_files = 0
    file_paths: List[str] = []
    if not os.path.isdir(root):
        return 0, 0, []

    stack: List[Tuple[str, int]] = [(root, 0)]
    while stack:
        if cancel and cancel():
            break
        dir_path, depth = stack.pop()
        if on_folder:
            on_folder(dir_path)
        try:
            with os.scandir(dir_path) as it:
                entries = list(it)
        except OSError:
            continue
        for entry in entries:
            if cancel and cancel():
                break
            try:
                if entry.is_symlink():
                    continue
                if entry.is_file(follow_symlinks=False):
                    st = entry.stat(follow_symlinks=False)
                    if min_mtime is not None and st.st_mtime > min_mtime:
                        continue
                    total_size += st.st_size
                    total_files += 1
                    file_paths.append(entry.path)
                elif entry.is_dir(follow_symlinks=False) and depth < max_depth:
                    if safety.is_reparse_point(entry.path):
                        continue
                    stack.append((entry.path, depth + 1))
            except OSError:
                continue
    return total_size, total_files, file_paths


class CleanupEngine:
    def __init__(
        self,
        *,
        temp_dir: Optional[str] = None,
        local_appdata: Optional[str] = None,
        appdata: Optional[str] = None,
        system_root: Optional[str] = None,
        system_drive: Optional[str] = None,
        home: Optional[str] = None,
        scan_drives: Optional[List[str]] = None,
        test_path_allowlist: Optional[Callable[[str], bool]] = None,
    ):
        self.home = home or os.path.expanduser("~")
        self.local_appdata = local_appdata or os.environ.get(
            "LOCALAPPDATA", os.path.join(self.home, "AppData", "Local")
        )
        self.appdata = appdata or os.environ.get(
            "APPDATA", os.path.join(self.home, "AppData", "Roaming")
        )
        self.temp_dir = temp_dir or os.environ.get("TEMP", os.path.join(self.local_appdata, "Temp"))
        self.system_root = system_root or os.environ.get("SystemRoot", r"C:\Windows")
        self.system_drive = system_drive or os.environ.get("SystemDrive", "C:")
        self.scan_drives = scan_drives
        self.test_path_allowlist = test_path_allowlist

        self._category_deletable_files: Dict[str, List[str]] = {}
        self._category_items: Dict[str, List[Dict[str, Any]]] = {}
        self._scan_id: Optional[str] = None
        self._scan_cancel = threading.Event()
        self._scan_lock = threading.Lock()
        self._scan_state: Dict[str, Any] = {"status": "idle"}
        self._last_scan_result: Optional[Dict[str, Any]] = None
        self._plans: Dict[str, Dict[str, Any]] = {}

    # ---- category model ----

    def _chromium_profile_cache_paths(self, user_data_parts: List[str]) -> List[str]:
        paths: List[str] = []
        user_data = os.path.join(self.local_appdata, *user_data_parts, "User Data")
        if not os.path.isdir(user_data):
            return paths
        try:
            for entry in os.scandir(user_data):
                if not entry.is_dir(follow_symlinks=False):
                    continue
                if entry.name != "Default" and not entry.name.startswith("Profile "):
                    continue
                for sub in ("Cache", "Code Cache", "GPUCache"):
                    paths.append(os.path.join(entry.path, sub))
        except OSError:
            pass
        return paths

    def _browser_cache_paths(self) -> List[str]:
        paths: List[str] = []
        for parts in (("Microsoft", "Edge"), ("Google", "Chrome"), ("BraveSoftware", "Brave-Browser")):
            paths.extend(self._chromium_profile_cache_paths(list(parts)))
        return paths

    def _chromium_profile_privacy_files(self, user_data_parts: List[str]) -> List[str]:
        """Per-profile Cookies / History files (not cache). Opt-in Wave 1 item."""
        paths: List[str] = []
        user_data = os.path.join(self.local_appdata, *user_data_parts, "User Data")
        if not os.path.isdir(user_data):
            return paths
        # History + cookies only (not saved passwords).
        names = ("Cookies", "History")
        try:
            for entry in os.scandir(user_data):
                if not entry.is_dir(follow_symlinks=False):
                    continue
                if entry.name != "Default" and not entry.name.startswith("Profile "):
                    continue
                for name in names:
                    candidate = os.path.join(entry.path, name)
                    if os.path.isfile(candidate):
                        paths.append(candidate)
                    nested = os.path.join(entry.path, "Network", name)
                    if os.path.isfile(nested):
                        paths.append(nested)
        except OSError:
            pass
        return paths

    def _browser_privacy_file_paths(self) -> List[str]:
        paths: List[str] = []
        for parts in (("Microsoft", "Edge"), ("Google", "Chrome"), ("BraveSoftware", "Brave-Browser")):
            paths.extend(self._chromium_profile_privacy_files(list(parts)))
        # Firefox profiles: cookies + history databases only
        ff_root = os.path.join(self.appdata, "Mozilla", "Firefox", "Profiles")
        if os.path.isdir(ff_root):
            try:
                for entry in os.scandir(ff_root):
                    if not entry.is_dir(follow_symlinks=False):
                        continue
                    for name in ("cookies.sqlite", "places.sqlite"):
                        candidate = os.path.join(entry.path, name)
                        if os.path.isfile(candidate):
                            paths.append(candidate)
            except OSError:
                pass
        return paths

    def _delivery_optimization_roots(self) -> List[str]:
        return [
            os.path.join(
                self.system_root,
                "ServiceProfiles",
                "NetworkService",
                "AppData",
                "Local",
                "Microsoft",
                "Windows",
                "DeliveryOptimization",
                "Cache",
            ),
            os.path.join(self.system_root, "SoftwareDistribution", "DeliveryOptimization"),
        ]

    def _store_cache_roots(self) -> List[str]:
        roots: List[str] = []
        packages = os.path.join(self.local_appdata, "Packages")
        if not os.path.isdir(packages):
            return roots
        try:
            for entry in os.scandir(packages):
                if not entry.is_dir(follow_symlinks=False):
                    continue
                name = entry.name.lower()
                if "windowsstore" not in name and "desktopappinstaller" not in name:
                    continue
                for sub in ("LocalCache", "TempState"):
                    roots.append(os.path.join(entry.path, sub))
        except OSError:
            pass
        return roots

    def _installer_scan_roots(self) -> List[str]:
        return [
            os.path.join(self.home, "Downloads"),
            os.path.join(self.local_appdata, "Downloaded Installations"),
        ]

    def get_category_definitions(self) -> List[Dict[str, Any]]:
        return [
            {
                "id": "user_temp",
                "name": "User Temporary Files",
                "tag": "Safe to Clean",
                "risk": "low",
                "description": "Temporary session files, installer staging data, and scratch buffers in %TEMP%.",
                "roots": [self.temp_dir],
                "svg_icon": "trash-2",
            },
            {
                "id": "system_temp",
                "name": "Windows System Temp (7+ days)",
                "tag": "Windows-owned",
                "risk": "low",
                "description": "Windows Temp files older than 7 days (%SystemRoot%\\Temp).",
                "roots": [os.path.join(self.system_root, "Temp")],
                "svg_icon": "cpu",
                "needs_windows_decision": True,
                "needs_admin": True,
                "min_age_days": 7,
            },
            {
                "id": "browser_caches",
                "name": "Web Browser Caches",
                "tag": "Browser Cache",
                "risk": "low",
                "description": "Cached web images, media buffers, and script artifacts from Microsoft Edge, Chrome, and Brave.",
                "roots": self._browser_cache_paths(),
                "svg_icon": "database",
            },
            {
                "id": "browser_history_cookies",
                "name": "Browser history and cookies",
                "tag": "Opt-in privacy",
                "risk": "medium",
                "description": "History and cookies per browser profile (Edge, Chrome, Brave, Firefox). Separate from cache — you must select this on purpose. Signed-in sites may ask you to log in again.",
                "roots": [],
                "svg_icon": "database",
                "opt_in": True,
                "privacy_files": True,
            },
            {
                "id": "dev_caches",
                "name": "Developer & Tooling Caches",
                "tag": "Developer",
                "risk": "low",
                "description": "Cached package archives from Python pip, npm, Yarn, and Cargo registries.",
                "roots": [
                    os.path.join(self.local_appdata, "pip", "cache"),
                    os.path.join(self.appdata, "npm-cache"),
                    os.path.join(self.local_appdata, "Yarn", "Cache"),
                    os.path.join(self.home, ".cargo", "registry", "cache"),
                ],
                "svg_icon": "code",
            },
            {
                "id": "diagnostic_logs",
                "name": "Crash Dumps & Error Logs",
                "tag": "Telemetry",
                "risk": "low",
                "description": "Application crash dumps and Windows Error Reporting archives.",
                "roots": [
                    os.path.join(self.local_appdata, "CrashDumps"),
                    os.path.join(self.local_appdata, "Microsoft", "Windows", "WER", "ReportArchive"),
                    os.path.join(self.local_appdata, "Microsoft", "Windows", "WER", "ReportQueue"),
                ],
                "svg_icon": "file-text",
            },
            {
                "id": "windows_minidump",
                "name": "Windows Memory Minidumps (7+ days)",
                "tag": "Windows-owned",
                "risk": "low",
                "description": "Kernel minidump files older than 7 days under %SystemRoot%\\Minidump.",
                "roots": [os.path.join(self.system_root, "Minidump")],
                "svg_icon": "file-text",
                "needs_windows_decision": True,
                "needs_admin": True,
                "min_age_days": 7,
            },
            {
                "id": "package_caches",
                "name": "Installer Download Caches",
                "tag": "Installer cache",
                "risk": "low",
                "description": "Downloaded installer archives kept after installs.",
                "roots": ["C:\\ProgramData\\chocolatey\\cache"],
                "svg_icon": "archive",
            },
            {
                "id": "app_caches",
                "name": "Desktop Application Caches",
                "tag": "App Cache",
                "risk": "low",
                "description": "Cached data from Discord, Slack, Microsoft Teams, VS Code, and Spotify.",
                "roots": [
                    os.path.join(self.appdata, "discord", "Cache"),
                    os.path.join(self.appdata, "discord", "Code Cache"),
                    os.path.join(self.appdata, "Slack", "Cache"),
                    os.path.join(self.appdata, "Slack", "Service Worker", "CacheStorage"),
                    os.path.join(self.appdata, "Microsoft", "Teams", "Cache"),
                    os.path.join(self.appdata, "Code", "Cache"),
                    os.path.join(self.appdata, "Code", "CachedData"),
                    os.path.join(self.local_appdata, "Spotify", "Storage"),
                ],
                "svg_icon": "grid",
            },
            {
                "id": "redundant_installers",
                "name": "Redundant Staging & DLL Buffers",
                "tag": "Redundant",
                "risk": "low",
                "description": "Leftover installer packages (.msi, .msp), temporary staging files, and orphan deployment artifacts.",
                "roots": [
                    os.path.join(self.local_appdata, "Downloaded Installations"),
                    os.path.join(self.local_appdata, "Microsoft", "Windows", "INetCache"),
                ],
                "svg_icon": "archive",
            },
            {
                "id": "thumbnail_cache",
                "name": "Windows Thumbnail & Icon Cache",
                "tag": "Explorer",
                "risk": "low",
                "description": "Explorer thumbnails (thumbcache_*.db) and icon cache databases. Icons may rebuild the next time Explorer opens.",
                "roots": [
                    os.path.join(self.local_appdata, "Microsoft", "Windows", "Explorer"),
                    os.path.join(self.local_appdata, "IconCache.db"),
                ],
                "svg_icon": "image",
                "rebuild_warning": True,
            },
            {
                "id": "directx_cache",
                "name": "DirectX Shader Caches",
                "tag": "GPU Cache",
                "risk": "low",
                "description": "Compiled graphics pipeline shaders from DirectX, NVIDIA, and AMD driver caches. Games may rebuild shaders on next launch (longer first load).",
                "roots": [
                    os.path.join(self.local_appdata, "D3DSCache"),
                    os.path.join(self.local_appdata, "NVIDIA", "DXCache"),
                    os.path.join(self.local_appdata, "AMD", "DxCache"),
                ],
                "svg_icon": "monitor",
                "rebuild_warning": True,
            },
            {
                "id": "delivery_optimization",
                "name": "Delivery Optimization cache",
                "tag": "Windows-owned",
                "risk": "low",
                "description": "Windows Delivery Optimization download cache. Skipped while Windows Update servicing is active.",
                "roots": self._delivery_optimization_roots(),
                "svg_icon": "refresh-cw",
                "needs_windows_decision": True,
                "needs_admin": True,
            },
            {
                "id": "store_cache",
                "name": "Microsoft Store cache",
                "tag": "Repair",
                "risk": "low",
                "description": "Local cache folders for the Microsoft Store app for this user. Does not uninstall Store apps.",
                "roots": self._store_cache_roots(),
                "svg_icon": "refresh-cw",
            },
            {
                "id": "windows_update_cache",
                "name": "Windows Update Download Cache",
                "tag": "Windows-owned",
                "risk": "low",
                "description": "Downloaded Windows Update files in SoftwareDistribution\\Download (services stopped briefly).",
                "roots": [os.path.join(self.system_root, "SoftwareDistribution", "Download")],
                "svg_icon": "refresh-cw",
                "needs_windows_decision": True,
                "needs_admin": True,
                "windows_service_cleanup": True,
            },
            {
                "id": "windows_component_cleanup",
                "name": "Windows Component Store Cleanup",
                "tag": "Windows-owned",
                "risk": "medium",
                "description": "Optional DISM component cleanup performed by Windows (StartComponentCleanup).",
                "roots": [],
                "svg_icon": "refresh-cw",
                "needs_windows_decision": True,
                "needs_admin": True,
                "dism_cleanup": True,
            },
            {
                "id": "recycle_bin",
                "name": "Windows Recycle Bin",
                "tag": "Permanent",
                "risk": "medium",
                "description": "Files and folders currently stored in the Windows Recycle Bin awaiting permanent removal.",
                "roots": [],
                "svg_icon": "trash",
            },
            {
                "id": "old_installers",
                "name": "Old Installer Packages",
                "tag": "Installers",
                "risk": "low",
                "description": "Installer files (.msi, .msp, .exe, .msix) older than 30 days in Downloads and downloaded installation staging folders.",
                "roots": [],
                "svg_icon": "archive",
            },
            {
                "id": "duplicate_installers",
                "name": "Duplicate Installer Copies",
                "tag": "Installers",
                "risk": "low",
                "description": "Duplicate installer filenames over 1 MB in Downloads and downloaded installation staging folders (newest copy kept).",
                "roots": [],
                "svg_icon": "archive",
            },
            {
                "id": "missing_folders",
                "name": "Missing Install Locations",
                "tag": "Report Only",
                "risk": "medium",
                "description": "Uninstall registry entries that point to folders which no longer exist (informational report).",
                "roots": [],
                "svg_icon": "file-text",
                "report_only": True,
            },
        ]

    def _paths_key(self, roots: List[str]) -> List[str]:
        if self.scan_drives:
            letters = {d.rstrip(":").upper() for d in self.scan_drives}
            filtered: List[str] = []
            for root in roots:
                if len(root) >= 2 and root[1] == ":":
                    if root[0].upper() in letters:
                        filtered.append(root)
                else:
                    filtered.append(root)
            return filtered
        if self.scan_drives is not None:
            return []
        return roots

    # ---- installer / registry helpers (ported) ----

    def _iter_installer_files(self):
        exts = {".msi", ".msp", ".exe", ".msix"}
        for root in self._installer_scan_roots():
            if not os.path.isdir(root):
                continue
            try:
                for dirpath, _, files in os.walk(root):
                    for fname in files:
                        ext = os.path.splitext(fname)[1].lower()
                        if ext not in exts:
                            continue
                        fp = os.path.join(dirpath, fname)
                        if safety.is_globally_denied(fp):
                            continue
                        try:
                            st = os.stat(fp, follow_symlinks=False)
                            yield {"path": fp, "name": fname, "size_bytes": st.st_size, "mtime": st.st_mtime}
                        except OSError:
                            pass
            except OSError:
                pass

    def _scan_old_installers(self) -> List[Dict[str, Any]]:
        cutoff = time.time() - (30 * 86400)
        items: List[Dict[str, Any]] = []
        for entry in self._iter_installer_files():
            if entry["mtime"] >= cutoff:
                continue
            mtime = datetime.datetime.fromtimestamp(entry["mtime"]).strftime("%Y-%m-%d %H:%M")
            items.append({
                "path": entry["path"],
                "name": entry["name"],
                "size_bytes": entry["size_bytes"],
                "size_str": human_size(entry["size_bytes"]),
                "modified": mtime,
                "ext": os.path.splitext(entry["name"])[1].lstrip(".").lower() or "file",
            })
        items.sort(key=lambda x: x["size_bytes"], reverse=True)
        return items

    def _scan_duplicate_installers(self) -> List[Dict[str, Any]]:
        by_name: Dict[str, List[Dict[str, Any]]] = defaultdict(list)
        for entry in self._iter_installer_files():
            if entry["size_bytes"] <= 1024 * 1024:
                continue
            by_name[entry["name"].lower()].append(entry)
        duplicates: List[Dict[str, Any]] = []
        for group in by_name.values():
            if len(group) < 2:
                continue
            group.sort(key=lambda x: x["mtime"], reverse=True)
            for stale in group[1:]:
                mtime = datetime.datetime.fromtimestamp(stale["mtime"]).strftime("%Y-%m-%d %H:%M")
                duplicates.append({
                    "path": stale["path"],
                    "name": stale["name"],
                    "size_bytes": stale["size_bytes"],
                    "size_str": human_size(stale["size_bytes"]),
                    "modified": mtime,
                    "ext": os.path.splitext(stale["name"])[1].lstrip(".").lower() or "file",
                })
        duplicates.sort(key=lambda x: x["size_bytes"], reverse=True)
        return duplicates

    def _scan_missing_folders(self) -> List[Dict[str, Any]]:
        rows: List[Dict[str, Any]] = []
        seen: Set[str] = set()
        hives = [
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        ]
        for hive, uninstall_path in hives:
            try:
                with winreg.OpenKey(hive, uninstall_path) as base:
                    sub_count = winreg.QueryInfoKey(base)[0]
                    for i in range(sub_count):
                        try:
                            sub_name = winreg.EnumKey(base, i)
                            with winreg.OpenKey(base, sub_name) as subkey:
                                try:
                                    system_component, _ = winreg.QueryValueEx(subkey, "SystemComponent")
                                    if int(system_component) == 1:
                                        continue
                                except OSError:
                                    pass
                                try:
                                    install_loc, _ = winreg.QueryValueEx(subkey, "InstallLocation")
                                except OSError:
                                    continue
                                install_loc = (install_loc or "").strip().strip('"')
                                if not install_loc:
                                    continue
                                norm_loc = os.path.normpath(install_loc)
                                key = norm_loc.lower()
                                if key in seen or os.path.isdir(norm_loc):
                                    continue
                                seen.add(key)
                                display_name = sub_name
                                try:
                                    display_name, _ = winreg.QueryValueEx(subkey, "DisplayName")
                                except OSError:
                                    pass
                                rows.append({
                                    "path": norm_loc,
                                    "name": str(display_name),
                                    "size_bytes": 0,
                                    "size_str": "0 B",
                                    "modified": datetime.datetime.now().strftime("%Y-%m-%d %H:%M"),
                                    "ext": "report",
                                })
                        except OSError:
                            pass
            except OSError:
                pass
        return rows

    def _get_recycle_bin_stats(self) -> Tuple[int, int]:
        try:
            rb = SHQUERYRBINFO()
            rb.cbSize = ctypes.sizeof(SHQUERYRBINFO)
            res = ctypes.windll.shell32.SHQueryRecycleBinW(None, ctypes.byref(rb))
            if res == 0:
                return max(0, rb.i64Size), max(0, rb.i64NumItems)
        except Exception:
            pass
        return 0, 0

    # ---- Windows maintenance (ADR 011) ----

    def reboot_pending(self) -> bool:
        try:
            with winreg.OpenKey(
                winreg.HKEY_LOCAL_MACHINE,
                r"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update",
            ) as key:
                try:
                    val, _ = winreg.QueryValueEx(key, "RebootRequired")
                    if val:
                        return True
                except OSError:
                    pass
        except OSError:
            pass
        try:
            with winreg.OpenKey(
                winreg.HKEY_LOCAL_MACHINE,
                r"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing",
            ) as key:
                try:
                    val, _ = winreg.QueryValueEx(key, "RebootPending")
                    if int(val) == 1:
                        return True
                except OSError:
                    pass
        except OSError:
            pass
        try:
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Control\Session Manager") as key:
                try:
                    val, _ = winreg.QueryValueEx(key, "PendingFileRenameOperations")
                    if val:
                        return True
                except OSError:
                    pass
        except OSError:
            pass
        return False

    def update_in_progress(self) -> bool:
        for proc in ("TrustedInstaller.exe",):
            try:
                out = subprocess.check_output(
                    ["tasklist", "/FI", f"IMAGENAME eq {proc}", "/NH"],
                    creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
                    timeout=3,
                )
                if proc.split(".")[0].lower() in out.decode("utf-8", errors="ignore").lower():
                    return True
            except Exception:
                pass
        try:
            out = subprocess.check_output(
                ["sc", "query", "wuauserv"],
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
                timeout=3,
            )
            text = out.decode("utf-8", errors="ignore").lower()
            if "running" in text and "stop_pending" not in text:
                return False
        except Exception:
            pass
        return False

    def _service_control(self, name: str, action: str) -> Tuple[bool, str]:
        try:
            res = subprocess.run(
                ["sc", action, name],
                capture_output=True,
                text=True,
                timeout=15,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            ok = res.returncode == 0
            return ok, (res.stderr or res.stdout or "").strip()
        except Exception as exc:
            return False, str(exc)

    def _clean_windows_update_downloads(self) -> Tuple[int, int, int, int]:
        if self.reboot_pending() or self.update_in_progress():
            return 0, 0, 0, 1
        download_root = os.path.join(self.system_root, "SoftwareDistribution", "Download")
        stop_bits, _ = self._service_control("bits", "stop")
        stop_wu, msg_wu = self._service_control("wuauserv", "stop")
        if not (stop_bits and stop_wu):
            self._service_control("bits", "start")
            self._service_control("wuauserv", "start")
            return 0, 0, 0, 1 if not stop_wu else 0

        freed = 0
        removed = 0
        blocked = 0
        try:
            if os.path.isdir(download_root):
                try:
                    for entry in os.scandir(download_root):
                        if entry.name in (".", ".."):
                            continue
                        target = entry.path
                        if entry.is_file(follow_symlinks=False):
                            outcome, sz = safety.try_delete_file(target, download_root)
                            if outcome == "removed":
                                freed += sz
                                removed += 1
                            elif outcome == "blocked":
                                blocked += 1
                        elif entry.is_dir(follow_symlinks=False):
                            for root, _, files in os.walk(entry.path, topdown=False):
                                for fname in files:
                                    fp = os.path.join(root, fname)
                                    outcome, sz = safety.try_delete_file(fp, download_root)
                                    if outcome == "removed":
                                        freed += sz
                                        removed += 1
                                    elif outcome == "blocked":
                                        blocked += 1
                except OSError:
                    blocked += 1
        finally:
            self._service_control("bits", "start")
            self._service_control("wuauserv", "start")
        return freed, removed, blocked, 0

    def _run_dism_component_cleanup(self) -> Tuple[int, int, int, int]:
        if self.reboot_pending() or self.update_in_progress():
            return 0, 0, 0, 1
        dism = os.path.join(self.system_root, "System32", "Dism.exe")
        if not os.path.isfile(dism):
            dism = "dism.exe"
        try:
            res = subprocess.run(
                [dism, "/Online", "/Cleanup-Image", "/StartComponentCleanup"],
                capture_output=True,
                text=True,
                timeout=3600,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            if res.returncode != 0:
                return 0, 0, 0, 1
            return 0, 0, 0, 0
        except Exception:
            return 0, 0, 0, 1

    # ---- scan (parallel) ----

    def _scan_one_category(
        self,
        cat: Dict[str, Any],
        progress_cb: Optional[Callable[[str, int, int, str], None]] = None,
    ) -> Dict[str, Any]:
        cat_id = cat["id"]
        items: List[Dict[str, Any]] = []
        if cat_id == "recycle_bin":
            rb_size, rb_count = self._get_recycle_bin_stats()
            return {"size_bytes": rb_size, "file_count": rb_count, "items": items, "deletable_paths": []}
        if cat.get("privacy_files") or cat_id == "browser_history_cookies":
            paths = [p for p in self._browser_privacy_file_paths() if os.path.isfile(p)]
            size_bytes = 0
            for p in paths:
                try:
                    size_bytes += os.path.getsize(p)
                except OSError:
                    pass
            return {
                "size_bytes": size_bytes,
                "file_count": len(paths),
                "items": [{"path": p, "size_bytes": 0, "name": os.path.basename(p)} for p in paths],
                "deletable_paths": paths,
            }
        if cat_id == "old_installers":
            items = self._scan_old_installers()
            paths = [i["path"] for i in items]
            return {
                "size_bytes": sum(i["size_bytes"] for i in items),
                "file_count": len(items),
                "items": items,
                "deletable_paths": paths,
            }
        if cat_id == "duplicate_installers":
            items = self._scan_duplicate_installers()
            paths = [i["path"] for i in items]
            return {
                "size_bytes": sum(i["size_bytes"] for i in items),
                "file_count": len(items),
                "items": items,
                "deletable_paths": paths,
            }
        if cat_id == "missing_folders":
            items = self._scan_missing_folders()
            return {"size_bytes": 0, "file_count": len(items), "items": items, "deletable_paths": []}
        if cat.get("dism_cleanup"):
            return {"size_bytes": 0, "file_count": 0, "items": [], "deletable_paths": []}

        min_mtime = None
        if cat.get("min_age_days"):
            min_mtime = time.time() - int(cat["min_age_days"]) * 86400

        size_bytes = 0
        file_count = 0
        deletable_paths: List[str] = []
        roots = self._paths_key(cat.get("roots") or [])

        def _report(folder: str) -> None:
            if progress_cb:
                progress_cb(cat_id, size_bytes, file_count, folder)

        for root in roots:
            if not os.path.exists(root):
                continue
            if os.path.isfile(root):
                try:
                    st = os.stat(root)
                    if min_mtime is not None and st.st_mtime > min_mtime:
                        continue
                    size_bytes += st.st_size
                    file_count += 1
                    deletable_paths.append(root)
                except OSError:
                    pass
                continue
            if safety.is_reparse_point(root):
                continue
            s, f, paths = _scandir_size(
                root,
                cancel=self._scan_cancel.is_set,
                on_folder=_report,
                min_mtime=min_mtime,
            )
            size_bytes += s
            file_count += f
            deletable_paths.extend(paths)

        return {
            "size_bytes": size_bytes,
            "file_count": file_count,
            "items": items,
            "deletable_paths": deletable_paths,
        }

    def scan(
        self,
        *,
        progress_cb: Optional[Callable[[str, int, int, str], None]] = None,
        budget_s: float = SCAN_BUDGET_S,
    ) -> Dict[str, Any]:
        started = time.time()
        self._scan_cancel.clear()
        self._category_deletable_files = {}
        self._category_items = {}
        categories = self.get_category_definitions()
        total_reclaimable = 0
        total_files = 0
        windows_owned_bytes = 0
        windows_owned_files = 0
        scanned_categories: List[Dict[str, Any]] = []
        timing: Dict[str, float] = {}

        def _progress(cat_id: str, bytes_so_far: int, files_so_far: int, folder: str) -> None:
            if progress_cb:
                progress_cb(cat_id, bytes_so_far, files_so_far, folder)
            with self._scan_lock:
                self._scan_state.setdefault("progress", {})[cat_id] = {
                    "bytes": bytes_so_far,
                    "files": files_so_far,
                    "folder": folder,
                }

        futures: Dict[str, concurrent.futures.Future] = {}
        start_times: Dict[str, float] = {}
        with concurrent.futures.ThreadPoolExecutor(max_workers=SCAN_WORKERS) as pool:
            for cat in categories:
                if time.time() - started > budget_s:
                    break
                if self._scan_cancel.is_set():
                    break
                start_times[cat["id"]] = time.time()
                futures[cat["id"]] = pool.submit(self._scan_one_category, cat, _progress)
            for cat in categories:
                cat_id = cat["id"]
                fut = futures.get(cat_id)
                if fut is None:
                    continue
                if time.time() - started > budget_s:
                    break
                try:
                    row = fut.result(timeout=max(1.0, budget_s - (time.time() - started)))
                except Exception:
                    row = {"size_bytes": 0, "file_count": 0, "items": [], "deletable_paths": []}
                timing[cat_id] = round(time.time() - start_times.get(cat_id, started), 3)

                items = row.get("items") or []
                self._category_items[cat_id] = items
                if row.get("deletable_paths"):
                    self._category_deletable_files[cat_id] = row["deletable_paths"]

                cat_size = int(row.get("size_bytes") or 0)
                cat_files = int(row.get("file_count") or 0)
                needs_windows = bool(
                    cat.get("needs_windows_decision") or cat_id in WINDOWS_DEFERRED_CATEGORY_IDS
                )
                if cat.get("report_only"):
                    total_files += cat_files
                elif needs_windows:
                    windows_owned_bytes += cat_size
                    windows_owned_files += cat_files
                    total_files += cat_files
                else:
                    total_reclaimable += cat_size
                    total_files += cat_files

                is_selected = (
                    cat_size > 0
                    and cat.get("risk") == "low"
                    and not cat.get("report_only")
                    and not cat.get("opt_in")
                    and not needs_windows
                )
                scanned_categories.append({
                    "id": cat_id,
                    "name": cat["name"],
                    "tag": cat["tag"],
                    "risk": cat["risk"],
                    "description": cat["description"],
                    "size_bytes": cat_size,
                    "size_str": human_size(cat_size),
                    "file_count": cat_files,
                    "item_count": cat_files,
                    "items": items[:50],
                    "selected": is_selected,
                    "report_only": bool(cat.get("report_only")),
                    "needs_windows_decision": needs_windows,
                    "needs_admin": bool(cat.get("needs_admin")),
                    "opt_in": bool(cat.get("opt_in")),
                    "rebuild_warning": bool(cat.get("rebuild_warning")),
                    "svg_icon": cat["svg_icon"],
                })

        volumes = enumerate_cleanup_volumes(include_external=False)
        if not volumes:
            volumes = []
        free_bytes = total_bytes = used_bytes = used_pct = 0
        primary = next(
            (d for d in volumes if d.get("letter") == self.system_drive.rstrip(":").upper()),
            volumes[0] if volumes else None,
        )
        if primary:
            total_bytes = primary["total_bytes"]
            used_bytes = primary["used_bytes"]
            free_bytes = primary["free_bytes"]
            used_pct = round((used_bytes / total_bytes) * 100, 1) if total_bytes else 0
        else:
            try:
                disk_stats = shutil.disk_usage(self.system_drive + "\\")
                free_bytes = disk_stats.free
                total_bytes = disk_stats.total
                used_bytes = disk_stats.used
                used_pct = round((used_bytes / total_bytes) * 100, 1) if total_bytes else 0
            except OSError:
                pass

        result = {
            "scan_id": self._scan_id or str(uuid.uuid4()),
            "total_reclaimable_bytes": total_reclaimable,
            "total_reclaimable_str": human_size(total_reclaimable),
            "windows_owned_bytes": windows_owned_bytes,
            "windows_owned_str": human_size(windows_owned_bytes),
            "windows_owned_files": windows_owned_files,
            "total_files": total_files,
            "disk_total_bytes": total_bytes,
            "disk_total_str": human_size(total_bytes),
            "disk_used_str": human_size(used_bytes),
            "disk_free_str": human_size(free_bytes),
            "disk_used_percent": used_pct,
            "disks": [
                {
                    "letter": d["letter"],
                    "total_bytes": d["total_bytes"],
                    "free_bytes": d["free_bytes"],
                    "used_bytes": d["used_bytes"],
                }
                for d in volumes
            ],
            "categories": scanned_categories,
            "category_timing_s": timing,
            "partial": self._scan_cancel.is_set() or (time.time() - started) > budget_s,
        }
        self._last_scan_result = result
        return result

    def scan_start(self) -> Dict[str, Any]:
        with self._scan_lock:
            if self._scan_state.get("status") == "running":
                return {"started": False, "reason": "already_running", "scan_id": self._scan_id}
            self._scan_id = str(uuid.uuid4())
            self._scan_cancel.clear()
            self._scan_state = {"status": "running", "scan_id": self._scan_id, "progress": {}}

        def _run() -> None:
            try:
                result = self.scan()
                with self._scan_lock:
                    self._scan_state = {
                        "status": "done",
                        "scan_id": self._scan_id,
                        "result": result,
                        "progress": result.get("category_timing_s", {}),
                    }
            except Exception as exc:
                with self._scan_lock:
                    self._scan_state = {"status": "error", "error": str(exc), "scan_id": self._scan_id}

        threading.Thread(target=_run, daemon=True).start()
        return {"started": True, "scan_id": self._scan_id}

    def scan_status(self) -> Dict[str, Any]:
        with self._scan_lock:
            state = dict(self._scan_state)
        return state

    def scan_cancel(self) -> Dict[str, Any]:
        self._scan_cancel.set()
        with self._scan_lock:
            self._scan_state["status"] = "cancelled"
        return {"cancelled": True}

    # ---- plan token ----

    def cleanup_get_plan(self, category_ids: List[str]) -> Dict[str, Any]:
        scan_res = self._last_scan_result or self.scan()
        scan_id = scan_res.get("scan_id") or str(uuid.uuid4())
        all_cats = {c["id"]: c for c in scan_res.get("categories", [])}
        selected = [all_cats[cid] for cid in category_ids if cid in all_cats]
        if not selected:
            return {"error": "No matching categories", "token": None}

        entries = [
            {
                "id": c["id"],
                "size_bytes": c.get("size_bytes", 0),
                "file_count": c.get("file_count", 0),
            }
            for c in selected
        ]
        created = time.time()
        payload = {
            "scan_id": scan_id,
            "created_at": created,
            "expires_at": created + PLAN_TTL_SECONDS,
            "categories": entries,
        }
        digest = hashlib.sha256(json.dumps(payload, sort_keys=True).encode("utf-8")).hexdigest()[:24]
        token = f"{scan_id}.{digest}"
        self._plans[token] = payload
        total_bytes = sum(e["size_bytes"] for e in entries)
        total_files = sum(e["file_count"] for e in entries)
        return {
            "token": token,
            "scan_id": scan_id,
            "expires_at": payload["expires_at"],
            "total_size_bytes": total_bytes,
            "total_size_str": human_size(total_bytes),
            "total_files": total_files,
            "categories": entries,
            "requires_consent": True,
        }

    def get_cleanup_plan(self, category_ids: Optional[List[str]] = None) -> Dict[str, Any]:
        scan_res = self._last_scan_result or self.scan()
        if category_ids:
            ids = category_ids
        else:
            ids = [c["id"] for c in scan_res.get("categories", []) if c.get("selected")]
        plan = self.cleanup_get_plan(ids)
        if plan.get("error"):
            return plan
        return {
            "requires_consent": True,
            "token": plan["token"],
            "total_size_bytes": plan["total_size_bytes"],
            "total_size_str": plan["total_size_str"],
            "total_files": plan["total_files"],
            "categories": plan["categories"],
            "safety_guarantees": [
                "Documents, Desktop, and Photos are not cleaned.",
                "Reparse points and junctions are never followed for deletion.",
                "Windows-owned items require administrator consent and supported service routes.",
            ],
        }

    # ---- execute ----

    def cleanup_execute(self, token: str, category_ids: Optional[List[str]] = None) -> Dict[str, Any]:
        plan = self._plans.get(token)
        if not plan:
            return {"error": "Unknown or expired cleanup token"}
        if time.time() > float(plan["expires_at"]):
            return {"error": "Cleanup token expired"}
        last = self._last_scan_result or {}
        if plan.get("scan_id") != last.get("scan_id"):
            return {"error": "Cleanup token is from a different scan"}

        token_ids = {e["id"] for e in plan.get("categories", [])}
        if category_ids:
            extra = set(category_ids) - token_ids
            if extra:
                return {"error": f"Category ids not in token: {sorted(extra)}"}

        ids = [e["id"] for e in plan.get("categories", [])]
        return self._execute_category_ids(ids)

    def clean(
        self,
        category_ids: List[str],
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_log: Optional[Callable[[str], None]] = None,
    ) -> Dict[str, Any]:
        if on_log:
            on_log(f"Starting cleanup for {len(category_ids)} categories...")
        res = self._execute_category_ids(category_ids, on_progress=on_progress, on_log=on_log)
        return {
            "total_freed_bytes": res.get("freed_bytes", 0),
            "total_freed_str": human_size(res.get("freed_bytes", 0)),
            "files_removed": res.get("removed_files", 0),
            "files_skipped": res.get("skipped_in_use", 0),
            "items_blocked": res.get("blocked", 0),
            **res,
        }

    def _disk_free(self) -> int:
        try:
            return shutil.disk_usage(self.system_drive + "\\").free
        except OSError:
            return 0

    def _execute_category_ids(
        self,
        category_ids: List[str],
        *,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_log: Optional[Callable[[str], None]] = None,
    ) -> Dict[str, Any]:
        disk_before = self._disk_free()
        cats = {c["id"]: c for c in self.get_category_definitions()}
        aggregate = safety.DeleteBatchResult()
        total = len(category_ids)

        for idx, cat_id in enumerate(category_ids):
            cat = cats.get(cat_id)
            if not cat:
                aggregate.blocked += 1
                if on_log:
                    on_log(f"Unknown category id: {cat_id}")
                continue
            if on_progress:
                on_progress(idx / max(1, total), f"Cleaning {cat.get('name', cat_id)}...")

            if cat.get("report_only"):
                aggregate.blocked += 1
                continue

            if cat_id in WINDOWS_ADMIN_CATEGORY_IDS or cat.get("needs_admin"):
                if self.reboot_pending() or self.update_in_progress():
                    aggregate.blocked += 1
                    aggregate.needs_admin += 1
                    aggregate.per_category.append({
                        "id": cat_id,
                        "freed_bytes": 0,
                        "blocked": 1,
                        "error": "Windows update or reboot pending",
                    })
                    continue

            if cat_id == "windows_update_cache":
                freed, removed, blocked, failed = self._clean_windows_update_downloads()
                aggregate.freed_bytes += freed
                aggregate.removed_files += removed
                aggregate.blocked += blocked + failed
                aggregate.per_category.append({"id": cat_id, "freed_bytes": freed, "removed_files": removed, "blocked": blocked + failed})
                continue

            if cat_id == "delivery_optimization":
                if self.reboot_pending() or self.update_in_progress():
                    aggregate.blocked += 1
                    aggregate.per_category.append({
                        "id": cat_id,
                        "freed_bytes": 0,
                        "blocked": 1,
                        "error": "Skipped while Windows Update servicing is active",
                    })
                    continue
                for root in self._paths_key(cat.get("roots") or []):
                    if not os.path.isdir(root):
                        continue
                    _, _, found = _scandir_size(root)
                    batch = safety.clean_paths_in_root(
                        found,
                        root,
                        cat_id,
                        allow_filter=self.test_path_allowlist,
                    )
                    aggregate.freed_bytes += batch.freed_bytes
                    aggregate.removed_files += batch.removed_files
                    aggregate.skipped_in_use += batch.skipped_in_use
                    aggregate.blocked += batch.blocked
                    aggregate.needs_admin += batch.needs_admin
                    aggregate.per_category.extend(batch.per_category)
                continue

            if cat_id == "browser_history_cookies" or cat.get("privacy_files"):
                paths = list(self._category_deletable_files.get(cat_id) or [])
                if not paths:
                    paths = [p for p in self._browser_privacy_file_paths() if os.path.isfile(p)]
                # Each file sits under its profile folder — delete file-by-file with its parent as root.
                for path in paths:
                    parent = os.path.dirname(path)
                    batch = safety.clean_paths_in_root(
                        [path],
                        parent,
                        cat_id,
                        allow_filter=self.test_path_allowlist,
                    )
                    aggregate.freed_bytes += batch.freed_bytes
                    aggregate.removed_files += batch.removed_files
                    aggregate.skipped_in_use += batch.skipped_in_use
                    aggregate.blocked += batch.blocked
                    aggregate.needs_admin += batch.needs_admin
                    aggregate.per_category.extend(batch.per_category)
                continue

            if cat.get("dism_cleanup"):
                _, _, blocked, failed = self._run_dism_component_cleanup()
                aggregate.blocked += blocked + failed
                aggregate.per_category.append({"id": cat_id, "freed_bytes": 0, "blocked": blocked + failed})
                continue

            if cat_id == "recycle_bin":
                rb_size, rb_count = self._get_recycle_bin_stats()
                try:
                    if ctypes.windll.shell32.SHEmptyRecycleBinW(None, None, 7) == 0:
                        aggregate.freed_bytes += rb_size
                        aggregate.removed_files += rb_count
                    else:
                        aggregate.blocked += 1
                except Exception:
                    aggregate.blocked += 1
                aggregate.per_category.append({"id": cat_id, "freed_bytes": rb_size, "removed_files": rb_count})
                continue

            paths = list(self._category_deletable_files.get(cat_id) or [])
            min_mtime = None
            if cat.get("min_age_days"):
                min_mtime = time.time() - int(cat["min_age_days"]) * 86400
            if not paths:
                for root in self._paths_key(cat.get("roots") or []):
                    if os.path.isfile(root):
                        paths.append(root)
                        continue
                    if not os.path.isdir(root):
                        continue
                    _, _, found = _scandir_size(root, min_mtime=min_mtime)
                    paths.extend(found)

            roots = cat.get("roots") or []
            if cat_id in ("old_installers", "duplicate_installers"):
                category_root = self._installer_scan_roots()[0]
                batch = safety.clean_paths_in_root(
                    paths,
                    category_root,
                    cat_id,
                    allow_filter=self.test_path_allowlist,
                )
                aggregate.freed_bytes += batch.freed_bytes
                aggregate.removed_files += batch.removed_files
                aggregate.skipped_in_use += batch.skipped_in_use
                aggregate.blocked += batch.blocked
                aggregate.needs_admin += batch.needs_admin
                aggregate.per_category.extend(batch.per_category)
                continue

            # Group by parent so file roots (e.g. IconCache.db) and folder roots both work.
            by_root: Dict[str, List[str]] = defaultdict(list)
            for path in paths:
                parent = os.path.dirname(path)
                by_root[parent].append(path)
            if not by_root and roots:
                for root in self._paths_key(roots):
                    by_root[root if os.path.isdir(root) else os.path.dirname(root)] = []
            for category_root, group in by_root.items():
                batch = safety.clean_paths_in_root(
                    group,
                    category_root,
                    cat_id,
                    allow_filter=self.test_path_allowlist,
                )
                aggregate.freed_bytes += batch.freed_bytes
                aggregate.removed_files += batch.removed_files
                aggregate.skipped_in_use += batch.skipped_in_use
                aggregate.blocked += batch.blocked
                aggregate.needs_admin += batch.needs_admin
                aggregate.per_category.extend(batch.per_category)

        disk_after = self._disk_free()
        out = aggregate.to_dict(disk_free_before=disk_before, disk_free_after=disk_after)
        if on_progress:
            on_progress(1.0, "Cleanup complete")
        return out

    def inspect_category(self, category_id: str) -> Dict[str, Any]:
        cats = {c["id"]: c for c in self.get_category_definitions()}
        cat = cats.get(category_id)
        if not cat:
            return {
                "category_id": category_id,
                "name": "Unknown",
                "files": [],
                "total_files": 0,
                "total_size_bytes": 0,
                "total_size_str": "0 B",
            }

        if category_id in ("old_installers", "duplicate_installers", "missing_folders"):
            items = self._category_items.get(category_id)
            if items is None:
                if category_id == "old_installers":
                    items = self._scan_old_installers()
                elif category_id == "duplicate_installers":
                    items = self._scan_duplicate_installers()
                else:
                    items = self._scan_missing_folders()
            total_size = sum(i.get("size_bytes", 0) for i in items)
            return {
                "category_id": category_id,
                "name": cat["name"],
                "files": items[:30],
                "total_files": len(items),
                "total_size_bytes": total_size,
                "total_size_str": human_size(total_size),
            }

        if category_id == "recycle_bin":
            rb_size, rb_count = self._get_recycle_bin_stats()
            return {
                "category_id": category_id,
                "name": cat["name"],
                "files": [{"path": "Recycle Bin", "size_bytes": rb_size}] if rb_size else [],
                "total_files": rb_count,
                "total_size_bytes": rb_size,
                "total_size_str": human_size(rb_size),
            }

        files: List[Dict[str, Any]] = []
        total_size = 0
        total_files = 0
        min_mtime = None
        if cat.get("min_age_days"):
            min_mtime = time.time() - int(cat["min_age_days"]) * 86400
        for root in self._paths_key(cat.get("roots") or []):
            if not os.path.isdir(root):
                continue
            s, f, paths = _scandir_size(root, min_mtime=min_mtime, max_depth=3)
            total_size += s
            total_files += f
            for fp in paths[:30]:
                try:
                    st = os.stat(fp, follow_symlinks=False)
                    files.append({
                        "path": fp,
                        "name": os.path.basename(fp),
                        "size_bytes": st.st_size,
                        "size_str": human_size(st.st_size),
                    })
                except OSError:
                    pass
        files.sort(key=lambda x: x["size_bytes"], reverse=True)
        return {
            "category_id": category_id,
            "name": cat["name"],
            "files": files[:30],
            "total_files": total_files,
            "total_size_bytes": total_size,
            "total_size_str": human_size(total_size),
        }

    def optimize_memory(self) -> Dict[str, Any]:
        try:
            ms_before = MEMORYSTATUSEX()
            ms_before.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
            ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(ms_before))
            avail_before = ms_before.ullAvailPhys

            h_proc = ctypes.windll.kernel32.OpenProcess(0x0100 | 0x0400, False, os.getpid())
            if h_proc:
                try:
                    ctypes.windll.psapi.EmptyWorkingSet(h_proc)
                finally:
                    ctypes.windll.kernel32.CloseHandle(h_proc)

            ms_after = MEMORYSTATUSEX()
            ms_after.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
            ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(ms_after))
            avail_after = ms_after.ullAvailPhys
            freed = max(0, avail_after - avail_before)
            return {
                "freed_bytes": freed,
                "freed_str": human_size(freed),
                "message": human_size(freed) + " of inactive RAM reclaimed" if freed > 0 else "No memory was freed",
            }
        except Exception as exc:
            return {"freed_bytes": 0, "freed_str": "0 B", "error": str(exc)}


def register_cleanup_scan_job(orchestrator: Any, engine: CleanupEngine) -> None:
    """Register disk-lane cleanup scan (not auto-started at launch).

    The scan must run on the same ``CleanupEngine`` instance used for ``clean()``.
    """

    def cleanup_job(ctx: Any) -> Dict[str, Any]:
        if ctx.cancelled.is_set():
            raise RuntimeError("cancelled")
        engine._scan_cancel.clear()
        category_ids = [c["id"] for c in engine.get_category_definitions()]
        total = max(1, len(category_ids))
        seen: set[str] = set()

        def progress_cb(cat_id: str, _bytes_so_far: int, _files_so_far: int, folder: str) -> None:
            if ctx.cancelled.is_set():
                engine._scan_cancel.set()
            seen.add(cat_id)
            pct = int(round(100 * len(seen) / total))
            update = getattr(orchestrator, "update_job_progress", None)
            if update:
                update(ctx.job_id, percent=pct, current_item=folder or cat_id)

        budget = min(float(getattr(ctx, "budget_s", SCAN_BUDGET_S) or SCAN_BUDGET_S), SCAN_BUDGET_S)
        return engine.scan(progress_cb=progress_cb, budget_s=budget)

    orchestrator.register_job("cleanup_scan", "disk", cleanup_job, SCAN_BUDGET_S)
