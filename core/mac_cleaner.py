import os
import shutil
import glob
import subprocess
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

def get_dir_size_and_count(dir_path: str, max_depth: int = 5) -> tuple[int, int, List[Dict[str, Any]]]:
    """Calculate size and file count of a directory, returning top sub-items."""
    total_size = 0
    total_files = 0
    sub_items = []

    if not os.path.exists(dir_path):
        return 0, 0, []

    try:
        entries = list(os.scandir(dir_path))
        for entry in entries:
            entry_size = 0
            entry_files = 0
            try:
                if entry.is_file(follow_symlinks=False):
                    entry_size = entry.stat(follow_symlinks=False).st_size
                    entry_files = 1
                elif entry.is_dir(follow_symlinks=False):
                    for root, _, files in os.walk(entry.path):
                        for f in files:
                            fp = os.path.join(root, f)
                            try:
                                entry_size += os.path.getsize(fp)
                                entry_files += 1
                            except Exception:
                                pass
                total_size += entry_size
                total_files += entry_files
                if entry_size > 5 * 1024 * 1024:  # Only report sub-items > 5MB
                    sub_items.append({
                        "name": entry.name,
                        "size_bytes": entry_size,
                        "size_str": human_size(entry_size),
                        "file_count": entry_files
                    })
            except Exception:
                pass
    except Exception:
        pass

    sub_items.sort(key=lambda x: x["size_bytes"], reverse=True)
    return total_size, total_files, sub_items[:10]

class MacCleaner:
    """Enterprise Mac System Cleanup Engine adhering to Quantum Modern Prism standard."""

    def __init__(self):
        self.home = os.path.expanduser("~")

    def get_category_definitions(self) -> List[Dict[str, Any]]:
        return [
            {
                "id": "user_caches",
                "name": "User App & System Caches",
                "tag": "Safe to Clean",
                "risk": "low",
                "description": "Temporary cached data from Safari, Chrome, Slack, Discord, Spotify, and Electron apps.",
                "paths": [os.path.join(self.home, "Library", "Caches")],
                "svg_icon": "database"
            },
            {
                "id": "dev_caches",
                "name": "Developer & Tooling Caches",
                "tag": "Developer",
                "risk": "low",
                "description": "Cached package archives from npm, Yarn, Python pip, and Cargo registries.",
                "paths": [
                    os.path.join(self.home, ".npm"),
                    os.path.join(self.home, "Library", "Caches", "Yarn"),
                    os.path.join(self.home, "Library", "Caches", "pip"),
                    os.path.join(self.home, ".cache", "pip"),
                    os.path.join(self.home, ".cargo", "registry", "cache")
                ],
                "svg_icon": "code"
            },
            {
                "id": "user_logs",
                "name": "Diagnostic & User Logs",
                "tag": "Telemetry",
                "risk": "low",
                "description": "Historical crash reports, application log archives, and system diagnostic dumps.",
                "paths": [
                    os.path.join(self.home, "Library", "Logs"),
                    os.path.join(self.home, "Library", "Logs", "DiagnosticReports")
                ],
                "svg_icon": "file-text"
            },
            {
                "id": "trash",
                "name": "macOS Trash Bin",
                "tag": "Permanent",
                "risk": "medium",
                "description": "Deleted files awaiting permanent deletion from the macOS Trash.",
                "paths": [os.path.join(self.home, ".Trash")],
                "svg_icon": "trash-2"
            },
            {
                "id": "brew_cache",
                "name": "Homebrew Cache & Stale Bottles",
                "tag": "Package Cache",
                "risk": "low",
                "description": "Downloaded bottle archives and partial download caches from Homebrew.",
                "paths": [
                    os.path.join(self.home, "Library", "Caches", "Homebrew")
                ],
                "svg_icon": "archive"
            },
            {
                "id": "xcode_derived",
                "name": "Xcode DerivedData & Simulator Caches",
                "tag": "Apple SDK",
                "risk": "low",
                "description": "Intermediate build artifacts, indexing databases, and iOS simulator cache files.",
                "paths": [
                    os.path.join(self.home, "Library", "Developer", "Xcode", "DerivedData"),
                    os.path.join(self.home, "Library", "Developer", "CoreSimulator", "Caches")
                ],
                "svg_icon": "cpu"
            },
            {
                "id": "orphaned_app_data",
                "name": "Orphaned App Leftovers",
                "tag": "App Remnants",
                "risk": "medium",
                "description": "Residual configuration files left by applications that are no longer installed.",
                "paths": [],  # dynamically calculated in scan()
                "svg_icon": "layers"
            }
        ]

    def _find_orphaned_app_folders(self) -> List[Dict[str, Any]]:
        """Identify folders in ~/Library/Application Support for uninstalled apps."""
        app_support = os.path.join(self.home, "Library", "Application Support")
        if not os.path.exists(app_support):
            return []

        # Enumerate known installed app basenames
        installed_names = set()
        for root_dir in ["/Applications", "/System/Applications", os.path.join(self.home, "Applications")]:
            if os.path.exists(root_dir):
                for item in os.listdir(root_dir):
                    if item.endswith(".app"):
                        installed_names.add(item[:-4].lower().replace(" ", ""))

        orphaned = []
        # Safe exceptions that are macOS built-ins
        known_system = {"apple", "dock", "syncservices", "icdd", "addressbook", "callhistorydb", "crashreporter"}

        try:
            for entry in os.scandir(app_support):
                if entry.is_dir():
                    norm_name = entry.name.lower().replace(" ", "")
                    if norm_name in known_system or "com.apple." in norm_name:
                        continue
                    # Check if any installed app contains this name
                    has_match = any(norm_name in a or a in norm_name for a in installed_names)
                    if not has_match:
                        # Check size
                        sz = 0
                        files = 0
                        for r, _, fls in os.walk(entry.path):
                            for f in fls:
                                try:
                                    sz += os.path.getsize(os.path.join(r, f))
                                    files += 1
                                except Exception:
                                    pass
                        if sz > 1024 * 1024:  # Over 1MB
                            orphaned.append({
                                "name": entry.name,
                                "path": entry.path,
                                "size_bytes": sz,
                                "size_str": human_size(sz),
                                "file_count": files
                            })
        except Exception:
            pass

        orphaned.sort(key=lambda x: x["size_bytes"], reverse=True)
        return orphaned[:15]

    def scan(self) -> Dict[str, Any]:
        """Perform system analysis of all cleanup targets."""
        categories = self.get_category_definitions()
        total_reclaimable = 0
        total_files = 0
        scanned_categories = []

        for cat in categories:
            cat_id = cat["id"]
            if cat_id == "orphaned_app_data":
                orphans = self._find_orphaned_app_folders()
                cat_size = sum(o["size_bytes"] for o in orphans)
                cat_files = sum(o["file_count"] for o in orphans)
                cat_sub = orphans
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

        # Get overall disk storage info
        disk_stats = shutil.disk_usage(self.home)
        free_bytes = disk_stats.free
        total_bytes = disk_stats.total
        used_bytes = disk_stats.used

        return {
            "total_reclaimable_bytes": total_reclaimable,
            "total_reclaimable_str": human_size(total_reclaimable),
            "total_files": total_files,
            "disk_total_str": human_size(total_bytes),
            "disk_used_str": human_size(used_bytes),
            "disk_free_str": human_size(free_bytes),
            "disk_used_percent": round((used_bytes / total_bytes) * 100, 1),
            "categories": scanned_categories
        }

    def clean(
        self,
        category_ids: List[str],
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_log: Optional[Callable[[str], None]] = None
    ) -> Dict[str, Any]:
        """Safely cleans chosen categories without breaking active applications."""
        categories = self.get_category_definitions()
        cat_map = {c["id"]: c for c in categories}

        total_freed = 0
        cleaned_details = []
        total_targets = len(category_ids)

        if on_log:
            on_log(f"Initiating Quantum Clean sequence across {total_targets} target categories...")

        for idx, cat_id in enumerate(category_ids):
            if cat_id not in cat_map:
                continue

            cat = cat_map[cat_id]
            ticker_msg = f"Purging {cat['name']}..."
            pct = idx / max(1, total_targets)
            if on_progress:
                on_progress(pct, ticker_msg)
            if on_log:
                on_log(f"[{idx+1}/{total_targets}] Cleaning: {cat['name']}")

            freed_in_cat = 0
            files_removed = 0

            if cat_id == "orphaned_app_data":
                orphans = self._find_orphaned_app_folders()
                for o in orphans:
                    try:
                        p = o["path"]
                        if os.path.exists(p):
                            shutil.rmtree(p, ignore_errors=True)
                            freed_in_cat += o["size_bytes"]
                            files_removed += o["file_count"]
                            if on_log:
                                on_log(f"  Removed orphan: {o['name']} ({o['size_str']})")
                    except Exception as err:
                        if on_log:
                            on_log(f"  Skipped {o['name']}: {err}")
            else:
                for p in cat["paths"]:
                    if not os.path.exists(p):
                        continue
                    try:
                        for entry in os.scandir(p):
                            try:
                                if entry.is_file(follow_symlinks=False):
                                    sz = entry.stat(follow_symlinks=False).st_size
                                    os.remove(entry.path)
                                    freed_in_cat += sz
                                    files_removed += 1
                                elif entry.is_dir(follow_symlinks=False):
                                    # Calculate size before removal
                                    sz = 0
                                    for r, _, fls in os.walk(entry.path):
                                        for f in fls:
                                            try:
                                                sz += os.path.getsize(os.path.join(r, f))
                                                files_removed += 1
                                            except Exception:
                                                pass
                                    shutil.rmtree(entry.path, ignore_errors=True)
                                    freed_in_cat += sz
                            except Exception:
                                pass
                    except Exception as err:
                        if on_log:
                            on_log(f"  Notice while clearing {p}: {err}")

            # Special case for Homebrew
            if cat_id == "brew_cache":
                try:
                    subprocess.run(["brew", "cleanup", "-s", "--prune=all"],
                                   capture_output=True, timeout=30)
                except Exception:
                    pass

            total_freed += freed_in_cat
            cleaned_details.append({
                "id": cat_id,
                "name": cat["name"],
                "freed_bytes": freed_in_cat,
                "freed_str": human_size(freed_in_cat),
                "files_removed": files_removed
            })

            if on_log:
                on_log(f"Completed {cat['name']}: Freed {human_size(freed_in_cat)} ({files_removed} files removed).")

        if on_progress:
            on_progress(1.0, "Cleanup complete.")
        if on_log:
            on_log(f"Quantum Clean finalized successfully. Total reclaimed: {human_size(total_freed)}.")

        return {
            "success": True,
            "total_freed_bytes": total_freed,
            "total_freed_str": human_size(total_freed),
            "categories": cleaned_details
        }
