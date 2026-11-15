"""Detect winget upgrade/install failures caused by files locked by other processes."""

from __future__ import annotations

import ctypes
import os
import re
from dataclasses import dataclass
from typing import Callable, Dict, Iterable, List, Optional, Sequence, Set, Tuple

from core.arp_inventory import list_arp_entries

# WinGet APPINSTALLER_ERROR_* HRESULTs (signed int32)
WINGET_ERROR_APPLICATION_IN_USE = -1978334959  # 0x8A150111
WINGET_ERROR_APPLICATION_IN_USE_ALT = -1978334975  # 0x8A150101
WINGET_ERROR_APPLICATION_IN_USE_ALT2 = -1978334973  # 0x8A150103

_FILES_IN_USE_EXIT_CODES = frozenset(
    {
        WINGET_ERROR_APPLICATION_IN_USE,
        WINGET_ERROR_APPLICATION_IN_USE_ALT,
        WINGET_ERROR_APPLICATION_IN_USE_ALT2,
    }
)

_IN_USE_PHRASES = (
    "in use",
    "currently running",
    "being used",
    "currently in use",
)

_EXIT_SIX_RE = re.compile(
    r"(?:exit code|installer failed):\s*6\b",
    re.IGNORECASE,
)


@dataclass(frozen=True)
class ProcessModuleSnapshot:
    pid: int
    exe_name: str
    module_paths: Tuple[str, ...]


def is_winget_files_in_use(exit_code: int, output: str) -> bool:
    if exit_code in _FILES_IN_USE_EXIT_CODES:
        return True
    text = output or ""
    if not text.strip():
        return False
    lowered = text.casefold()
    if not any(phrase in lowered for phrase in _IN_USE_PHRASES):
        return False
    return _EXIT_SIX_RE.search(text) is not None


def resolve_install_dir_from_arp(package_id: str, display_name: str = "") -> str:
    if os.name != "nt":
        return ""
    needle_name = (display_name or "").casefold().strip()
    slug = (package_id or "").rsplit(".", 1)[-1].casefold().replace("_", "")
    best_loc = ""
    best_score = 0
    for entry in list_arp_entries():
        loc = (entry.install_location or "").strip()
        if not loc or not os.path.isdir(loc):
            continue
        display = entry.name.casefold()
        compact = display.replace(" ", "")
        score = 0
        if needle_name and display == needle_name:
            return os.path.normpath(loc)
        if needle_name and needle_name in display:
            score += 4
        if slug and slug in compact:
            score += 3
        if slug and slug in display.replace(" ", ""):
            score += 2
        if package_id and package_id.casefold() in display:
            score += 2
        if score > best_score:
            best_score = score
            best_loc = loc
    return os.path.normpath(best_loc) if best_loc else ""


def exe_to_display_name(exe_name: str) -> str:
    base = os.path.splitext((exe_name or "").strip())[0]
    if not base:
        return exe_name or "Unknown"
    return base[:1].upper() + base[1:]


def _join_program_names(names: Sequence[str]) -> str:
    items = [n for n in names if n]
    if not items:
        return ""
    if len(items) == 1:
        return items[0]
    if len(items) == 2:
        return f"{items[0]} and {items[1]}"
    return ", ".join(items[:-1]) + f", and {items[-1]}"


def build_files_in_use_message(holders: Sequence[Dict[str, object]]) -> str:
    names = _join_program_names([str(h.get("name", "")) for h in holders])
    if not names:
        return (
            "Another program is using this app's files. "
            "Close browsers, meeting apps, and camera apps, then retry."
        )
    if " and " in names or ", " in names:
        return (
            f"{names} are using files from this app. Close {names}, then retry."
        )
    return f"{names} is using files from this app. Close {names}, then retry."


def _path_under_root(module_path: str, install_root: str) -> bool:
    if not module_path or not install_root:
        return False
    try:
        mod = os.path.normcase(os.path.normpath(module_path))
        root = os.path.normcase(os.path.normpath(install_root))
        if not root.endswith(os.sep):
            root = root + os.sep
        return mod.startswith(root)
    except Exception:
        return False


def _default_process_snapshots() -> List[ProcessModuleSnapshot]:
    if os.name != "nt":
        return []
    return list(_enumerate_process_modules_windows())


def _enumerate_process_snapshots_via_toolhelp() -> Iterable[Tuple[int, str]]:
    if os.name != "nt":
        return
    try:
        TH32CS_SNAPPROCESS = 0x00000002
        INVALID = ctypes.c_void_p(-1).value

        class PROCESSENTRY32W(ctypes.Structure):
            _fields_ = [
                ("dwSize", ctypes.c_uint32),
                ("cntUsage", ctypes.c_uint32),
                ("th32ProcessID", ctypes.c_uint32),
                ("th32DefaultHeapID", ctypes.POINTER(ctypes.c_ulong)),
                ("th32ModuleID", ctypes.c_uint32),
                ("cntThreads", ctypes.c_uint32),
                ("th32ParentProcessID", ctypes.c_uint32),
                ("pcPriClassBase", ctypes.c_long),
                ("dwFlags", ctypes.c_uint32),
                ("szExeFile", ctypes.c_wchar * 260),
            ]

        kernel32 = ctypes.windll.kernel32
        snap = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
        if snap == INVALID:
            return
        try:
            entry = PROCESSENTRY32W()
            entry.dwSize = ctypes.sizeof(PROCESSENTRY32W)
            if not kernel32.Process32FirstW(snap, ctypes.byref(entry)):
                return
            while True:
                yield int(entry.th32ProcessID), str(entry.szExeFile)
                if not kernel32.Process32NextW(snap, ctypes.byref(entry)):
                    break
        finally:
            kernel32.CloseHandle(snap)
    except Exception:
        return


def _module_paths_for_pid(pid: int) -> List[str]:
    if os.name != "nt":
        return []
    PROCESS_QUERY_INFORMATION = 0x0400
    PROCESS_VM_READ = 0x0010
    kernel32 = ctypes.windll.kernel32
    psapi = ctypes.windll.psapi
    handle = kernel32.OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, False, pid)
    if not handle:
        return []
    paths: List[str] = []
    try:
        hmods = (ctypes.c_void_p * 2048)()
        needed = ctypes.c_uint32()
        if not psapi.EnumProcessModules(
            handle,
            ctypes.byref(hmods),
            ctypes.sizeof(hmods),
            ctypes.byref(needed),
        ):
            return []
        count = needed.value // ctypes.sizeof(ctypes.c_void_p)
        buf = ctypes.create_unicode_buffer(32768)
        for i in range(count):
            if psapi.GetModuleFileNameExW(handle, hmods[i], buf, ctypes.sizeof(buf)):
                paths.append(buf.value)
    finally:
        kernel32.CloseHandle(handle)
    return paths


def _enumerate_process_modules_windows() -> Iterable[ProcessModuleSnapshot]:
    own_pid = os.getpid()
    for pid, exe_name in _enumerate_process_snapshots_via_toolhelp() or []:
        if pid in (0, 4, own_pid):
            continue
        modules = tuple(_module_paths_for_pid(pid))
        if not modules:
            continue
        yield ProcessModuleSnapshot(pid=pid, exe_name=exe_name, module_paths=modules)


def find_process_holders(
    install_root: str,
    *,
    skip_exe_names: Optional[Set[str]] = None,
    processes: Optional[Sequence[ProcessModuleSnapshot]] = None,
) -> List[Dict[str, object]]:
    if not install_root:
        return []
    skip = {n.casefold() for n in (skip_exe_names or set())}
    snapshots = list(processes) if processes is not None else _default_process_snapshots()
    holders: List[Dict[str, object]] = []
    seen_pids: Set[int] = set()
    for snap in snapshots:
        if snap.pid in seen_pids:
            continue
        if snap.exe_name.casefold() in skip:
            continue
        if not any(_path_under_root(m, install_root) for m in snap.module_paths):
            continue
        seen_pids.add(snap.pid)
        holders.append(
            {
                "name": exe_to_display_name(snap.exe_name),
                "pid": snap.pid,
            }
        )
    holders.sort(key=lambda h: (str(h.get("name", "")).casefold(), int(h.get("pid", 0))))
    return holders


def files_in_use_operation_fields(
    package_id: str,
    exit_code: int,
    output: str,
    *,
    display_name: str = "",
    processes: Optional[Sequence[ProcessModuleSnapshot]] = None,
    install_dir_resolver: Callable[[str, str], str] = resolve_install_dir_from_arp,
    main_exe_finder: Optional[Callable[[str, str], str]] = None,
) -> Optional[Tuple[str, List[Dict[str, object]]]]:
    if not is_winget_files_in_use(exit_code, output):
        return None
    install_root = install_dir_resolver(package_id, display_name)
    skip_exe: Set[str] = set()
    if install_root and main_exe_finder:
        main_exe = main_exe_finder(install_root, display_name)
        if main_exe:
            skip_exe.add(os.path.basename(main_exe).casefold())
    holders = find_process_holders(install_root, skip_exe_names=skip_exe, processes=processes)
    message = build_files_in_use_message(holders)
    return message, holders
