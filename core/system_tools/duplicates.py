"""Duplicate file finder: scan by size + sha256, recycle extras to Recycle Bin."""

from __future__ import annotations

import hashlib
import os
from collections import defaultdict
from typing import Any, Dict, List, Set, Tuple

from core.system_tools.large_files import human_size

MAX_FILES_SCANNED = 5000
MAX_GROUPS = 200

_last_scan_groups: List[Dict[str, Any]] = []
_last_scan_index: Dict[str, Dict[str, Any]] = {}


def _norm(path: str) -> str:
    return os.path.normcase(os.path.abspath(path))


def _file_sha256(path: str, chunk: int = 1024 * 1024) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        while True:
            block = fh.read(chunk)
            if not block:
                break
            h.update(block)
    return h.hexdigest()


def _move_files_to_recycle(paths: List[str]) -> Tuple[bool, str]:
    if os.name != "nt":
        return False, "Not supported on this platform."
    existing = [p for p in paths if os.path.isfile(p)]
    if not existing:
        return False, "No matching files found."
    try:
        import ctypes
        from ctypes import wintypes

        FO_DELETE = 0x0003
        FOF_ALLOWUNDO = 0x0040
        FOF_NOCONFIRMATION = 0x0010
        FOF_SILENT = 0x0004
        FOF_NOERRORUI = 0x0400

        class SHFILEOPSTRUCTW(ctypes.Structure):
            _fields_ = [
                ("hwnd", wintypes.HWND),
                ("wFunc", wintypes.UINT),
                ("pFrom", wintypes.LPCWSTR),
                ("pTo", wintypes.LPCWSTR),
                ("fFlags", wintypes.WORD),
                ("fAnyOperationsAborted", wintypes.BOOL),
                ("hNameMappings", wintypes.LPVOID),
                ("lpszProgressTitle", wintypes.LPCWSTR),
            ]

        joined = "\0".join(os.path.abspath(p) for p in existing) + "\0\0"
        op = SHFILEOPSTRUCTW()
        op.wFunc = FO_DELETE
        op.pFrom = joined
        op.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
        code = ctypes.windll.shell32.SHFileOperationW(ctypes.byref(op))
        if code != 0 or op.fAnyOperationsAborted:
            return False, "Could not move files to the Recycle Bin."
        return True, "ok"
    except Exception as exc:
        return False, str(exc)


def scan_duplicates(root: str, min_bytes: int = 1024 * 1024) -> Dict[str, Any]:
    global _last_scan_groups, _last_scan_index
    if not root or not os.path.isdir(root):
        return {"groups": [], "count": 0, "files_scanned": 0, "error": "Root folder not found."}

    by_size: Dict[int, List[str]] = defaultdict(list)
    files_scanned = 0
    stack = [os.path.abspath(root)]

    while stack and files_scanned < MAX_FILES_SCANNED:
        folder = stack.pop()
        try:
            with os.scandir(folder) as it:
                entries = list(it)
        except OSError:
            continue
        for entry in entries:
            if files_scanned >= MAX_FILES_SCANNED:
                break
            try:
                if entry.is_symlink():
                    continue
                if entry.is_file(follow_symlinks=False):
                    st = entry.stat(follow_symlinks=False)
                    if st.st_size < min_bytes:
                        continue
                    by_size[st.st_size].append(entry.path)
                    files_scanned += 1
                elif entry.is_dir(follow_symlinks=False):
                    stack.append(entry.path)
            except OSError:
                continue

    groups: List[Dict[str, Any]] = []
    _last_scan_index = {}
    for size, paths in by_size.items():
        if len(paths) < 2:
            continue
        by_hash: Dict[str, List[str]] = defaultdict(list)
        for path in paths:
            try:
                digest = _file_sha256(path)
            except OSError:
                continue
            by_hash[digest].append(path)
        for digest, dup_paths in by_hash.items():
            if len(dup_paths) < 2:
                continue
            dup_paths = sorted(dup_paths)
            group = {
                "size_bytes": size,
                "size_str": human_size(size),
                "hash": digest,
                "paths": dup_paths,
                "count": len(dup_paths),
            }
            groups.append(group)
            for p in dup_paths:
                _last_scan_index[_norm(p)] = group
            if len(groups) >= MAX_GROUPS:
                break
        if len(groups) >= MAX_GROUPS:
            break

    groups.sort(key=lambda g: (-g["size_bytes"] * g["count"], g["hash"]))
    _last_scan_groups = groups
    return {
        "groups": groups,
        "count": len(groups),
        "files_scanned": files_scanned,
        "capped": files_scanned >= MAX_FILES_SCANNED or len(groups) >= MAX_GROUPS,
    }


def _paths_in_last_scan(paths: List[str]) -> Tuple[bool, str, Dict[str, Dict[str, Any]]]:
    if not _last_scan_groups:
        return False, "Run a duplicate scan first.", {}
    index: Dict[str, Dict[str, Any]] = {}
    for p in paths:
        key = _norm(p)
        group = _last_scan_index.get(key)
        if not group:
            return False, "A path was not part of the latest duplicate scan.", {}
        index[key] = group
    return True, "ok", index


def recycle_duplicates(paths: List[str], *, confirmed: bool = False) -> Dict[str, Any]:
    if not confirmed:
        return {"success": False, "message": "Confirmation is required to recycle duplicate files."}
    if not paths:
        return {"success": False, "message": "No paths were given."}

    ok, msg, index = _paths_in_last_scan(paths)
    if not ok:
        return {"success": False, "message": msg}

    per_group: Dict[str, Set[str]] = defaultdict(set)
    for p in paths:
        group = index[_norm(p)]
        per_group[group["hash"]].add(_norm(p))

    allowed: List[str] = []
    skipped: List[str] = []
    for group in _last_scan_groups:
        digest = group["hash"]
        selected = per_group.get(digest)
        if not selected:
            continue
        group_paths = {_norm(x) for x in group["paths"]}
        if not selected.issubset(group_paths):
            return {"success": False, "message": "A path was not part of the latest duplicate scan."}
        remaining = [gp for gp in group["paths"] if _norm(gp) not in selected]
        if not remaining:
            skipped.extend(sorted(selected))
            continue
        for gp in group["paths"]:
            if _norm(gp) in selected and os.path.isfile(gp):
                allowed.append(gp)

    if not allowed:
        return {
            "success": False,
            "message": "Each duplicate group must keep at least one file.",
            "skipped": skipped,
        }

    recycle_ok, recycle_msg = _move_files_to_recycle(allowed)
    if not recycle_ok:
        return {"success": False, "message": recycle_msg}
    return {
        "success": True,
        "recycled": len(allowed),
        "skipped": skipped,
        "message": f"Moved {len(allowed)} duplicate file(s) to the Recycle Bin.",
    }