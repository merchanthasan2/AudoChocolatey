"""Thin compatibility shim over core.system_tools.cleanup."""

from __future__ import annotations

from typing import Any, Callable, Dict, List, Optional

from core.system_tools.cleanup import WINDOWS_DEFERRED_CATEGORY_IDS, CleanupEngine, human_size
from core.system_tools import safety

# Re-export for legacy imports
__all__ = [
    "WINDOWS_DEFERRED_CATEGORY_IDS",
    "WindowsCleaner",
    "human_size",
    "get_dir_size_and_count",
]


def get_dir_size_and_count(dir_path: str, max_depth: int = 3, current_depth: int = 0):
    """Legacy helper retained for tests importing from windows_cleaner."""
    from core.system_tools.cleanup import _scandir_size

    size, files, _ = _scandir_size(dir_path, max_depth=max_depth)
    return size, files, []


class WindowsCleaner:
    """Delegates to CleanupEngine while preserving the legacy public surface."""

    def __init__(self):
        self._engine = CleanupEngine()
        self._mirror_env_attrs()

    def _mirror_env_attrs(self) -> None:
        self.home = self._engine.home
        self.local_appdata = self._engine.local_appdata
        self.appdata = self._engine.appdata
        self.temp_dir = self._engine.temp_dir
        self.system_root = self._engine.system_root
        self.system_drive = self._engine.system_drive
        self._category_deletable_files = self._engine._category_deletable_files
        self._category_items = self._engine._category_items

    def _is_forbidden_delete_path(self, path: str) -> bool:
        return safety.is_forbidden_legacy(path, self.system_root)

    def get_category_definitions(self) -> List[Dict[str, Any]]:
        defs = self._engine.get_category_definitions()
        out: List[Dict[str, Any]] = []
        for cat in defs:
            row = dict(cat)
            row["paths"] = list(cat.get("roots") or [])
            out.append(row)
        return out

    def inspect_category(self, category_id: str) -> Dict[str, Any]:
        return self._engine.inspect_category(category_id)

    def get_cleanup_plan(self, category_ids: Optional[List[str]] = None) -> Dict[str, Any]:
        return self._engine.get_cleanup_plan(category_ids)

    def scan(self) -> Dict[str, Any]:
        result = self._engine.scan()
        self._mirror_env_attrs()
        return result

    def clean(
        self,
        category_ids: List[str],
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_log: Optional[Callable[[str], None]] = None,
    ) -> Dict[str, Any]:
        result = self._engine.clean(category_ids, on_progress=on_progress, on_log=on_log)
        self._mirror_env_attrs()
        return result

    def optimize_memory(self) -> Dict[str, Any]:
        return self._engine.optimize_memory()

    # Async / token API passthrough
    def scan_start(self) -> Dict[str, Any]:
        return self._engine.scan_start()

    def scan_status(self) -> Dict[str, Any]:
        return self._engine.scan_status()

    def scan_cancel(self) -> Dict[str, Any]:
        return self._engine.scan_cancel()

    def cleanup_get_plan(self, category_ids: List[str]) -> Dict[str, Any]:
        return self._engine.cleanup_get_plan(category_ids)

    def cleanup_execute(self, token: str, category_ids: Optional[List[str]] = None) -> Dict[str, Any]:
        return self._engine.cleanup_execute(token, category_ids)
