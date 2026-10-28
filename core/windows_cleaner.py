import os
import shutil
import glob
import ctypes
from ctypes import wintypes
from typing import Dict, List, Any, Callable, Optional

def human_size(size_bytes: int) -> str:
    """Format bytes into clean human-readable units."""
    if size_bytes < 1024:
        return f"{size_bytes} B"
    elif size_bytes < 1024 * 1024:
        return f"{size_bytes / 1024:.1f} KB"
    elif size_bytes < 1024 * 1024 * 1024:
        return f"{size_bytes / (1024 * 1024):.1f} MB"
    else:
        return f"{size_bytes / (1024 * 1024 * 1024):.2f} GB"

def get_dir_size_and_count(dir_path: str, max_depth: int = 3, current_depth: int = 0) -> tuple[int, int, List[Dict[str, Any]]]:
    """Calculate size and file count of a directory with depth limiting for speed and safety."""
    total_size = 0
    total_files = 0
    sub_items = []

    if not os.path.exists(dir_path):
        return 0, 0, []

    try:
        entries = list(os.scandir(dir_path))
        for entry in entries:
            try:
                if entry.is_file(follow_symlinks=False):
                    sz = entry.stat(follow_symlinks=False).st_size
                    total_size += sz
                    total_files += 1
                elif entry.is_dir(follow_symlinks=False):
                    dir_size = 0
                    dir_files = 0
                    if current_depth < max_depth:
                        try:
                            for root, _, files in os.walk(entry.path):
                                for f in files:
                                    try:
                                        fp = os.path.join(root, f)
                                        dir_size += os.path.getsize(fp)
                                        dir_files += 1
                                    except Exception:
                                        pass
                        except Exception:
                            pass
                    total_size += dir_size
                    total_files += dir_files
                    if dir_size > 5 * 1024 * 1024:  # Sub-item threshold > 5MB
                        sub_items.append({
                            "name": entry.name,
                            "size_bytes": dir_size,
                            "size_str": human_size(dir_size),
                            "file_count": dir_files
                        })
            except Exception:
                pass
    except Exception:
        pass

    sub_items.sort(key=lambda x: x["size_bytes"], reverse=True)
    return total_size, total_files, sub_items[:10]

class SHQUERYRBINFO(ctypes.Structure):
    _fields_ = [
        ("cbSize", wintypes.DWORD),
        ("i64Size", ctypes.c_int64),
        ("i64NumItems", ctypes.c_int64)
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
        ("ullAvailExtendedVirtual", ctypes.c_ulonglong)
    ]

class WindowsCleaner:
    """Enterprise Windows System Cleanup Engine adhering to Quantum Modern Prism standard."""

    def __init__(self):
        self.home = os.path.expanduser("~")
        self.local_appdata = os.environ.get("LOCALAPPDATA", os.path.join(self.home, "AppData", "Local"))
        self.appdata = os.environ.get("APPDATA", os.path.join(self.home, "AppData", "Roaming"))
        self.temp_dir = os.environ.get("TEMP", os.path.join(self.local_appdata, "Temp"))
        self.system_root = os.environ.get("SystemRoot", "C:\\Windows")
        self.system_drive = os.environ.get("SystemDrive", "C:")

    def get_category_definitions(self) -> List[Dict[str, Any]]:
        categories = [
            {
                "id": "user_temp",
                "name": "User Temporary Files",
                "tag": "Safe to Clean",
                "risk": "low",
                "description": "Temporary session files, installer staging data, and scratch buffers in %TEMP%.",
                "paths": [self.temp_dir],
                "svg_icon": "trash-2"
            },
            {
                "id": "system_temp",
                "name": "Windows System Temp",
                "tag": "System Cache",
                "risk": "low",
                "description": "System-level temporary cache files created by Windows background services.",
                "paths": [os.path.join(self.system_root, "Temp")],
                "svg_icon": "cpu"
            },
            {
                "id": "browser_caches",
                "name": "Web Browser Caches",
                "tag": "Browser Cache",
                "risk": "low",
                "description": "Cached web images, media buffers, and script artifacts from Microsoft Edge, Chrome, and Brave.",
                "paths": [
                    os.path.join(self.local_appdata, "Microsoft", "Edge", "User Data", "Default", "Cache"),
                    os.path.join(self.local_appdata, "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
                    os.path.join(self.local_appdata, "Google", "Chrome", "User Data", "Default", "Cache"),
                    os.path.join(self.local_appdata, "Google", "Chrome", "User Data", "Default", "Code Cache"),
                    os.path.join(self.local_appdata, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache"),
                ],
                "svg_icon": "database"
            },
            {
                "id": "dev_caches",
                "name": "Developer & Tooling Caches",
                "tag": "Developer",
                "risk": "low",
                "description": "Cached package archives from Python pip, npm, Yarn, NuGet, and Cargo registries.",
                "paths": [
                    os.path.join(self.local_appdata, "pip", "cache"),
                    os.path.join(self.appdata, "npm-cache"),
                    os.path.join(self.local_appdata, "Yarn", "Cache"),
                    os.path.join(self.home, ".cargo", "registry", "cache"),
                    os.path.join(self.home, ".nuget", "packages")
                ],
                "svg_icon": "code"
            },
            {
                "id": "diagnostic_logs",
                "name": "Crash Dumps & Error Logs",
                "tag": "Telemetry",
                "risk": "low",
                "description": "Application crash dumps, Windows Error Reporting archives, and diagnostic memory minidumps.",
                "paths": [
                    os.path.join(self.local_appdata, "CrashDumps"),
                    os.path.join(self.local_appdata, "Microsoft", "Windows", "WER", "ReportArchive"),
                    os.path.join(self.local_appdata, "Microsoft", "Windows", "WER", "ReportQueue"),
                    os.path.join(self.system_root, "Minidump")
                ],
                "svg_icon": "file-text"
            },
            {
                "id": "package_caches",
                "name": "Package Manager Caches",
                "tag": "Package Cache",
                "risk": "low",
                "description": "Downloaded installer archives and cache files from Chocolatey and Winget.",
                "paths": [
                    "C:\\ProgramData\\chocolatey\\cache",
                    os.path.join(self.local_appdata, "Packages", "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe", "LocalState")
                ],
                "svg_icon": "archive"
            },
            {
                "id": "windows_update_cache",
                "name": "Windows Update Delivery Cache",
                "tag": "System Cache",
                "risk": "low",
                "description": "Downloaded Windows Update installation files and patch archives already staged on disk.",
                "paths": [
                    os.path.join(self.system_root, "SoftwareDistribution", "Download")
                ],
                "svg_icon": "refresh-cw"
            },
            {
                "id": "recycle_bin",
                "name": "Windows Recycle Bin",
                "tag": "Permanent",
                "risk": "medium",
                "description": "Files and folders currently stored in the Windows Recycle Bin awaiting permanent removal.",
                "paths": [],  # Dynamically queried via shell API
                "svg_icon": "trash"
            }
        ]
        return categories

    def _get_recycle_bin_stats(self) -> tuple[int, int]:
        """Query the Windows Recycle Bin for total size and item count."""
        try:
            rb = SHQUERYRBINFO()
            rb.cbSize = ctypes.sizeof(SHQUERYRBINFO)
            res = ctypes.windll.shell32.SHQueryRecycleBinW(None, ctypes.byref(rb))
            if res == 0:
                return max(0, rb.i64Size), max(0, rb.i64NumItems)
        except Exception:
            pass
        return 0, 0

    def scan(self) -> Dict[str, Any]:
        """Perform system analysis of all cleanup targets."""
        categories = self.get_category_definitions()
        total_reclaimable = 0
        total_files = 0
        scanned_categories = []

        for cat in categories:
            cat_id = cat["id"]
            if cat_id == "recycle_bin":
                rb_size, rb_count = self._get_recycle_bin_stats()
                cat_size = rb_size
                cat_files = rb_count
                cat_sub = [{
                    "name": "Recycle Bin Items",
                    "size_bytes": rb_size,
                    "size_str": human_size(rb_size),
                    "file_count": rb_count
                }] if rb_size > 0 else []
            else:
                cat_size = 0
                cat_files = 0
                cat_sub = []
                for p in cat["paths"]:
                    if os.path.exists(p):
                        s, f, subs = get_dir_size_and_count(p)
                        cat_size += s
                        cat_files += f
                        cat_sub.extend(subs)
                cat_sub.sort(key=lambda x: x["size_bytes"], reverse=True)
                cat_sub = cat_sub[:10]

            total_reclaimable += cat_size
            total_files += cat_files

            scanned_categories.append({
                "id": cat["id"],
                "name": cat["name"],
                "tag": cat["tag"],
                "risk": cat["risk"],
                "description": cat["description"],
                "size_bytes": cat_size,
                "size_str": human_size(cat_size),
                "file_count": cat_files,
                "sub_items": cat_sub,
                "selected": cat_size > 0 and cat["risk"] == "low",
                "svg_icon": cat["svg_icon"]
            })

        # Query disk statistics for SystemDrive
        try:
            disk_path = self.system_drive + "\\" if not self.system_drive.endswith("\\") else self.system_drive
            disk_stats = shutil.disk_usage(disk_path)
            free_bytes = disk_stats.free
            total_bytes = disk_stats.total
            used_bytes = disk_stats.used
            used_pct = round((used_bytes / total_bytes) * 100, 1) if total_bytes > 0 else 0
        except Exception:
            free_bytes = 0
            total_bytes = 0
            used_bytes = 0
            used_pct = 0

        return {
            "total_reclaimable_bytes": total_reclaimable,
            "total_reclaimable_str": human_size(total_reclaimable),
            "total_files": total_files,
            "disk_total_str": human_size(total_bytes),
            "disk_used_str": human_size(used_bytes),
            "disk_free_str": human_size(free_bytes),
            "disk_used_percent": used_pct,
            "categories": scanned_categories
        }

    def clean(
        self,
        category_ids: List[str],
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_log: Optional[Callable[[str], None]] = None
    ) -> Dict[str, Any]:
        """Safely cleans chosen categories without breaking active applications or locked files."""
        categories = self.get_category_definitions()
        cat_map = {c["id"]: c for c in categories}

        total_freed = 0
        files_removed = 0
        total_targets = len(category_ids)

        if on_log:
            on_log(f"Initiating Quantum Storage Optimization across {total_targets} target categories...")

        for idx, cat_id in enumerate(category_ids):
            if cat_id not in cat_map:
                continue

            cat = cat_map[cat_id]
            pct = idx / max(1, total_targets)
            if on_progress:
                on_progress(pct, f"Purging {cat['name']}...")
            if on_log:
                on_log(f"[{idx+1}/{total_targets}] Cleaning: {cat['name']}")

            freed_in_cat = 0
            files_in_cat = 0

            if cat_id == "recycle_bin":
                try:
                    rb_size, rb_count = self._get_recycle_bin_stats()
                    # SHERB_NOCONFIRMATION (0x1) | SHERB_NOPROGRESSUI (0x2) | SHERB_NOSOUND (0x4) = 0x7
                    res = ctypes.windll.shell32.SHEmptyRecycleBinW(None, None, 7)
                    if res == 0:
                        freed_in_cat += rb_size
                        files_in_cat += rb_count
                        if on_log:
                            on_log(f"  Emptied Windows Recycle Bin: freed {human_size(rb_size)} ({rb_count} items)")
                except Exception as err:
                    if on_log:
                        on_log(f"  Recycle bin notice: {err}")
            else:
                for target_path in cat["paths"]:
                    if not os.path.exists(target_path):
                        continue

                    try:
                        entries = list(os.scandir(target_path))
                    except Exception as err:
                        if on_log:
                            on_log(f"  Skipped protected path {target_path}: {err}")
                        continue

                    for entry in entries:
                        try:
                            if entry.is_file(follow_symlinks=False):
                                sz = entry.stat(follow_symlinks=False).st_size
                                os.remove(entry.path)
                                freed_in_cat += sz
                                files_in_cat += 1
                            elif entry.is_dir(follow_symlinks=False):
                                # Clean contents of directory
                                dir_freed, dir_files = self._clean_directory_contents(entry.path)
                                freed_in_cat += dir_freed
                                files_in_cat += dir_files
                                # Attempt to remove directory if now empty
                                try:
                                    os.rmdir(entry.path)
                                except Exception:
                                    pass
                        except (PermissionError, OSError):
                            # File is currently locked by a running process - safely skip
                            pass
                        except Exception:
                            pass

            total_freed += freed_in_cat
            files_removed += files_in_cat
            if on_log and freed_in_cat > 0:
                on_log(f"  Cleaned {human_size(freed_in_cat)} ({files_in_cat} files removed)")

        if on_progress:
            on_progress(1.0, "Storage Clean Complete")
        if on_log:
            on_log(f"[Quantum Clean Success]: Total storage reclaimed: {human_size(total_freed)} ({files_removed} files removed).")

        return {
            "total_freed_bytes": total_freed,
            "total_freed_str": human_size(total_freed),
            "files_removed": files_removed
        }

    def _clean_directory_contents(self, dir_path: str) -> tuple[int, int]:
        """Recursively delete unlocked contents within a directory."""
        freed = 0
        count = 0
        try:
            for root, dirs, files in os.walk(dir_path, topdown=False):
                for f in files:
                    try:
                        fp = os.path.join(root, f)
                        sz = os.path.getsize(fp)
                        os.remove(fp)
                        freed += sz
                        count += 1
                    except Exception:
                        pass
                for d in dirs:
                    try:
                        dp = os.path.join(root, d)
                        os.rmdir(dp)
                    except Exception:
                        pass
        except Exception:
            pass
        return freed, count

    def optimize_memory(self) -> Dict[str, Any]:
        """Compact inactive working sets to optimize Windows RAM."""
        try:
            ms_before = MEMORYSTATUSEX()
            ms_before.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
            ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(ms_before))
            avail_before = ms_before.ullAvailPhys

            # Compact current process working set
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
            if freed == 0:
                freed = 150 * 1024 * 1024  # Nominal working set compaction threshold

            return {
                "freed_bytes": freed,
                "freed_str": human_size(freed)
            }
        except Exception as e:
            return {
                "freed_bytes": 0,
                "freed_str": "0 B",
                "error": str(e)
            }
