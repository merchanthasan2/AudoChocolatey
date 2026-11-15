"""WebAPI mixin for system utility tools (startup, large files, leftovers)."""

from __future__ import annotations

import os
import time
from typing import Any, Dict, List, Optional

class SystemToolsAPI:
    """Methods exposed to the web bridge for system utility pages."""

    def startup_list(self, *args, **kwargs) -> List[Dict[str, Any]]:
        from core.system_tools import startup as startup_mod

        return startup_mod.startup_list()

    def startup_set_enabled(self, entry_id: str = "", enabled: bool = True, *args, **kwargs) -> Dict[str, Any]:
        if not entry_id and args:
            entry_id = str(args[0])
        if kwargs.get("entry_id"):
            entry_id = str(kwargs["entry_id"])
        if "enabled" in kwargs:
            enabled = bool(kwargs["enabled"])
        if len(args) > 1:
            enabled = bool(args[1])
        from core.system_tools import startup as startup_mod

        return startup_mod.startup_set_enabled(entry_id, enabled)

    def startup_undo_last(self, *args, **kwargs) -> Dict[str, Any]:
        from core.system_tools import startup as startup_mod

        return startup_mod.startup_undo_last()

    def startup_history(self, *args, **kwargs) -> List[Dict[str, Any]]:
        from core.system_tools import startup as startup_mod

        return startup_mod.startup_history()

    def large_files_scan_start(
        self, root: str = "", min_mb: float = 500, older_than_days: int = 0, *args, **kwargs
    ) -> Dict[str, Any]:
        if not root and kwargs.get("root"):
            root = str(kwargs["root"])
        if kwargs.get("min_mb") is not None:
            min_mb = float(kwargs["min_mb"])
        if kwargs.get("older_than_days") is not None:
            older_than_days = int(kwargs["older_than_days"])
        from core.system_tools import large_files as lf

        job = lf.scan_start(root=root, min_mb=min_mb, older_than_days=older_than_days)
        return {"job_id": job}

    def large_files_status(self, job: str = "", *args, **kwargs) -> Dict[str, Any]:
        job_id = job or kwargs.get("job") or kwargs.get("job_id") or (args[0] if args else "")
        from core.system_tools import large_files as lf

        return lf.scan_status(str(job_id))

    def large_files_cancel(self, job: str = "", *args, **kwargs) -> Dict[str, Any]:
        job_id = job or kwargs.get("job") or kwargs.get("job_id") or (args[0] if args else "")
        from core.system_tools import large_files as lf

        return lf.scan_cancel(str(job_id))

    def large_files_recycle(self, paths: Optional[List[str]] = None, *args, **kwargs) -> Dict[str, Any]:
        if paths is None and args:
            paths = args[0] if isinstance(args[0], list) else list(args)
        paths = paths or kwargs.get("paths") or []
        job_id = kwargs.get("job") or kwargs.get("job_id") or ""
        from core.system_tools import large_files as lf

        return lf.move_to_recycle_bin(list(paths), job_id=str(job_id))

    def large_files_confirm(self, paths: Optional[List[str]] = None, *args, **kwargs) -> Dict[str, Any]:
        if paths is None and args:
            paths = args[0] if isinstance(args[0], list) else list(args)
        paths = paths or kwargs.get("paths") or []
        job_id = kwargs.get("job") or kwargs.get("job_id") or ""
        from core.system_tools import large_files as lf

        return lf.confirm_delete(list(paths), job_id=str(job_id))

    def large_files_delete(
        self, paths: Optional[List[str]] = None, token: str = "", *args, **kwargs
    ) -> Dict[str, Any]:
        if paths is None and args:
            paths = args[0] if isinstance(args[0], list) else []
        if not token and len(args) > 1:
            token = str(args[1])
        token = token or str(kwargs.get("token") or "")
        paths = paths or kwargs.get("paths") or []
        from core.system_tools import large_files as lf

        return lf.delete_permanently(list(paths), token)

    def large_files_reveal(self, path: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not path and args:
            path = str(args[0])
        path = path or str(kwargs.get("path") or "")
        from core.system_tools import large_files as lf

        return lf.reveal_in_explorer(path)

    def leftovers_scan(self, *args, **kwargs) -> Dict[str, Any]:
        from core.system_tools import leftovers as lo

        return lo.leftovers_scan()

    def leftovers_reveal(self, path: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not path and args:
            path = str(args[0])
        path = path or str(kwargs.get("path") or "")
        from core.system_tools import leftovers as lo

        return lo.leftovers_reveal(path)

    def leftovers_recycle(self, path: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not path and args:
            path = str(args[0])
        path = path or str(kwargs.get("path") or "")
        confirmed = bool(kwargs.get("confirmed", False))
        from core.system_tools import leftovers as lo

        return lo.leftovers_recycle(path, confirmed=confirmed)

    def leftovers_confirm_registry(self, entry_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not entry_id and args:
            entry_id = str(args[0])
        entry_id = entry_id or str(kwargs.get("entry_id") or "")
        from core.system_tools import leftovers as lo

        return lo.leftovers_confirm_registry(entry_id)

    def leftovers_delete_registry(self, entry_id: str = "", token: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not entry_id and args:
            entry_id = str(args[0])
        if not token and len(args) > 1:
            token = str(args[1])
        entry_id = entry_id or str(kwargs.get("entry_id") or "")
        token = token or str(kwargs.get("token") or "")
        from core.system_tools import leftovers as lo

        return lo.leftovers_delete_registry(entry_id, token)

    def leftovers_restore_registry(self, backup_id: str = "", *args, **kwargs) -> Dict[str, Any]:
        if not backup_id and args:
            backup_id = str(args[0])
        backup_id = backup_id or str(kwargs.get("backup_id") or "")
        from core.system_tools import leftovers as lo

        return lo.leftovers_restore_registry(backup_id)


def register_system_tool_scan_jobs(orchestrator: Any) -> None:
    """Register startup and leftovers scan jobs on the orchestrator (local lane)."""
    from core.system_tools import leftovers as lo
    from core.system_tools import startup as startup_mod

    def startup_job(ctx: Any) -> List[Dict[str, Any]]:
        if ctx.cancelled.is_set():
            raise RuntimeError("cancelled")
        return startup_mod.startup_list()

    def leftovers_job(ctx: Any) -> Dict[str, Any]:
        if ctx.cancelled.is_set():
            raise RuntimeError("cancelled")

        def progress_cb(*, percent: Optional[int] = None, current_item: str = "") -> None:
            if ctx.cancelled.is_set():
                raise RuntimeError("cancelled")
            update = getattr(orchestrator, "update_job_progress", None)
            if update:
                if percent is not None:
                    update(ctx.job_id, percent=percent, current_item=current_item)
                else:
                    update(ctx.job_id, current_item=current_item)

        return lo.leftovers_scan(progress_cb=progress_cb)

    orchestrator.register_job("startup", "local", startup_job, budget_s=60.0)
    orchestrator.register_job("leftovers", "local", leftovers_job, budget_s=90.0)
