"""Allow-list delete guard for cleanup categories."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from typing import Any, Callable, Dict, List, Optional, Set, Tuple

FILE_ATTRIBUTE_REPARSE_POINT = 0x400

GLOBAL_DENY_SUFFIXES = (
    "winsxs",
    "system32",
    "syswow64",
    "installer",
)


def norm_path(path: str) -> str:
    return os.path.normcase(os.path.normpath(os.path.abspath(path)))


def _env_roots() -> Dict[str, str]:
    home = os.path.expanduser("~")
    system_root = os.environ.get("SystemRoot", r"C:\Windows")
    return {
        "system_root": norm_path(system_root),
        "program_files": norm_path(os.environ.get("ProgramFiles", r"C:\Program Files")),
        "program_files_x86": norm_path(
            os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")
        ),
        "documents": norm_path(os.path.join(home, "Documents")),
        "desktop": norm_path(os.path.join(home, "Desktop")),
        "pictures": norm_path(os.path.join(home, "Pictures")),
        "videos": norm_path(os.path.join(home, "Videos")),
        "music": norm_path(os.path.join(home, "Music")),
    }


def global_deny_roots(extra: Optional[Dict[str, str]] = None) -> List[str]:
    env = _env_roots()
    if extra:
        env.update(extra)
    roots = [
        os.path.join(env["system_root"], "WinSxS"),
        os.path.join(env["system_root"], "System32"),
        os.path.join(env["system_root"], "SysWOW64"),
        os.path.join(env["system_root"], "Installer"),
        env["program_files"],
        env["program_files_x86"],
        env["documents"],
        env["desktop"],
        env["pictures"],
        env["videos"],
        env["music"],
    ]
    return [norm_path(r) for r in roots]


def is_globally_denied(path: str, deny_roots: Optional[List[str]] = None) -> bool:
    """Permanent denylist: protected Windows and user library roots."""
    p = norm_path(os.path.realpath(path) if os.path.exists(path) else path)
    roots = deny_roots if deny_roots is not None else global_deny_roots()
    for root in roots:
        if p == root or p.startswith(root + os.sep):
            return True
    return False


def is_forbidden_legacy(path: str, system_root: Optional[str] = None) -> bool:
    """Backward-compatible guard used by Track 1 tests (blocks entire SystemRoot tree)."""
    sr = norm_path(system_root or os.environ.get("SystemRoot", r"C:\Windows"))
    p = norm_path(path)
    forbidden = [
        sr,
        os.path.join(sr, "WinSxS"),
        os.path.join(sr, "System32"),
        os.path.join(sr, "SysWOW64"),
        os.path.join(sr, "Installer"),
    ]
    for root in forbidden:
        if p == root or p.startswith(root + os.sep):
            return True
    return False


def is_reparse_point(path: str) -> bool:
    try:
        attrs = os.stat(path, follow_symlinks=False).st_file_attributes
        return bool(attrs & FILE_ATTRIBUTE_REPARSE_POINT)
    except (AttributeError, OSError, ValueError):
        try:
            import ctypes
            from ctypes import wintypes

            attrs = wintypes.DWORD()
            if ctypes.windll.kernel32.GetFileAttributesW(path) == 0xFFFFFFFF:
                return False
            raw = ctypes.windll.kernel32.GetFileAttributesW(path)
            return bool(raw & FILE_ATTRIBUTE_REPARSE_POINT)
        except Exception:
            return False


def is_under_category_root(resolved: str, category_root: str) -> bool:
    root = norm_path(os.path.realpath(category_root))
    child = norm_path(resolved)
    if child == root:
        return True
    return child.startswith(root + os.sep)


def path_traversal_outside_root(declared: str, resolved: str, category_root: str) -> bool:
    if ".." in declared.replace("\\", "/").split("/"):
        return True
    root = norm_path(os.path.realpath(category_root))
    child = norm_path(resolved)
    return not is_under_category_root(child, root)


def deletion_allowed(
    path: str,
    category_root: str,
    *,
    deny_roots: Optional[List[str]] = None,
) -> Tuple[bool, str]:
    if not path or not category_root:
        return False, "blocked"
    if not os.path.lexists(path):
        return False, "missing"
    if norm_path(path) == norm_path(category_root):
        return False, "category_root"
    if is_reparse_point(path):
        return False, "reparse_point"
    try:
        resolved = os.path.realpath(path)
    except OSError:
        return False, "blocked"
    if path_traversal_outside_root(path, resolved, category_root):
        return False, "traversal"
    if not is_under_category_root(resolved, category_root):
        return False, "outside_root"
    if is_globally_denied(resolved, deny_roots):
        return False, "denylist"
    return True, "ok"


@dataclass
class DeleteBatchResult:
    freed_bytes: int = 0
    removed_files: int = 0
    skipped_in_use: int = 0
    blocked: int = 0
    needs_admin: int = 0
    per_category: List[Dict[str, Any]] = field(default_factory=list)

    def to_dict(
        self,
        *,
        disk_free_before: int = 0,
        disk_free_after: int = 0,
    ) -> Dict[str, Any]:
        return {
            "freed_bytes": self.freed_bytes,
            "removed_files": self.removed_files,
            "skipped_in_use": self.skipped_in_use,
            "blocked": self.blocked,
            "needs_admin": self.needs_admin,
            "disk_free_before": disk_free_before,
            "disk_free_after": disk_free_after,
            "per_category": self.per_category,
        }


def try_delete_file(
    path: str,
    category_root: str,
    *,
    deny_roots: Optional[List[str]] = None,
    allow_filter: Optional[Callable[[str], bool]] = None,
) -> Tuple[str, int]:
    """Returns (outcome, freed_bytes) where outcome is removed|skipped_in_use|blocked|needs_admin."""
    if allow_filter is not None and not allow_filter(path):
        return "blocked", 0
    ok, reason = deletion_allowed(path, category_root, deny_roots=deny_roots)
    if not ok:
        if reason == "category_root":
            return "blocked", 0
        if reason in ("reparse_point", "traversal", "outside_root", "denylist", "blocked"):
            return "blocked", 0
        return "blocked", 0
    try:
        size = os.path.getsize(path)
        os.remove(path)
        return "removed", size
    except PermissionError:
        return "needs_admin", 0
    except OSError as exc:
        winerr = getattr(exc, "winerror", None)
        if winerr in (5, 32):
            return "skipped_in_use", 0
        if winerr == 5:
            return "needs_admin", 0
        return "skipped_in_use", 0
    except Exception:
        return "skipped_in_use", 0


def clean_paths_in_root(
    paths: List[str],
    category_root: str,
    category_id: str,
    *,
    deny_roots: Optional[List[str]] = None,
    allow_filter: Optional[Callable[[str], bool]] = None,
) -> DeleteBatchResult:
    batch = DeleteBatchResult()
    cat_stats = {
        "id": category_id,
        "freed_bytes": 0,
        "removed_files": 0,
        "skipped_in_use": 0,
        "blocked": 0,
        "needs_admin": 0,
    }
    for path in paths:
        outcome, freed = try_delete_file(
            path,
            category_root,
            deny_roots=deny_roots,
            allow_filter=allow_filter,
        )
        if outcome == "removed":
            batch.freed_bytes += freed
            batch.removed_files += 1
            cat_stats["freed_bytes"] += freed
            cat_stats["removed_files"] += 1
        elif outcome == "skipped_in_use":
            batch.skipped_in_use += 1
            cat_stats["skipped_in_use"] += 1
        elif outcome == "needs_admin":
            batch.needs_admin += 1
            cat_stats["needs_admin"] += 1
        else:
            batch.blocked += 1
            cat_stats["blocked"] += 1
    batch.per_category.append(cat_stats)
    return batch
