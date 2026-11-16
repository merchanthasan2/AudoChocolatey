"""ADR 012: trim working sets across accessible processes."""

from __future__ import annotations

import ctypes
import os
import time
from ctypes import wintypes
from typing import Dict, Set

_PROTECTED = frozenset(
    {
        "system",
        "registry",
        "csrss.exe",
        "wininit.exe",
        "winlogon.exe",
        "smss.exe",
        "lsass.exe",
        "services.exe",
        "msmpeng.exe",
        "quantumupdater.exe",
    }
)

PROCESS_QUERY_INFORMATION = 0x0400
PROCESS_SET_QUOTA = 0x0100

try:
    _TH32CS_SNAPPROCESS = 0x00000002
    _INVALID_HANDLE = ctypes.c_void_p(-1).value

    class PROCESSENTRY32W(ctypes.Structure):
        _fields_ = [
            ("dwSize", wintypes.DWORD),
            ("cntUsage", wintypes.DWORD),
            ("th32ProcessID", wintypes.DWORD),
            ("th32DefaultHeapID", ctypes.POINTER(ctypes.c_ulong)),
            ("th32ModuleID", wintypes.DWORD),
            ("cntThreads", wintypes.DWORD),
            ("th32ParentProcessID", wintypes.DWORD),
            ("pcPriClassBase", wintypes.LONG),
            ("dwFlags", wintypes.DWORD),
            ("szExeFile", wintypes.WCHAR * 260),
        ]
except Exception:
    PROCESSENTRY32W = None  # type: ignore


def _avail_phys() -> int:
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
    if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)):
        return int(status.ullAvailPhys)
    return 0


def _enum_pids() -> Set[int]:
    pids: Set[int] = {0, 4}
    if os.name != "nt" or PROCESSENTRY32W is None:
        return pids
    kernel32 = ctypes.windll.kernel32
    snap = kernel32.CreateToolhelp32Snapshot(_TH32CS_SNAPPROCESS, 0)
    if snap == _INVALID_HANDLE:
        return pids
    try:
        entry = PROCESSENTRY32W()
        entry.dwSize = ctypes.sizeof(PROCESSENTRY32W)
        if not kernel32.Process32FirstW(snap, ctypes.byref(entry)):
            return pids
        while True:
            pids.add(int(entry.th32ProcessID))
            if not kernel32.Process32NextW(snap, ctypes.byref(entry)):
                break
    finally:
        kernel32.CloseHandle(snap)
    return pids


def _process_name(pid: int) -> str:
    if os.name != "nt" or PROCESSENTRY32W is None:
        return ""
    kernel32 = ctypes.windll.kernel32
    snap = kernel32.CreateToolhelp32Snapshot(_TH32CS_SNAPPROCESS, 0)
    if snap == _INVALID_HANDLE:
        return ""
    try:
        entry = PROCESSENTRY32W()
        entry.dwSize = ctypes.sizeof(PROCESSENTRY32W)
        if not kernel32.Process32FirstW(snap, ctypes.byref(entry)):
            return ""
        while True:
            if entry.th32ProcessID == pid:
                return str(entry.szExeFile).casefold()
            if not kernel32.Process32NextW(snap, ctypes.byref(entry)):
                break
    finally:
        kernel32.CloseHandle(snap)
    return ""


def optimize_ram_trim() -> Dict[str, int]:
    if os.name != "nt":
        return {
            "freed_bytes": 0,
            "before": 0,
            "after": 0,
            "processes_trimmed": 0,
            "processes_skipped": 0,
        }
    before = _avail_phys()
    trimmed = 0
    skipped = 0
    own_pid = os.getpid()
    psapi = ctypes.windll.psapi
    kernel32 = ctypes.windll.kernel32
    for pid in _enum_pids():
        if pid in (0, 4, own_pid):
            skipped += 1
            continue
        name = _process_name(pid)
        if name in _PROTECTED:
            skipped += 1
            continue
        access = PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA
        handle = kernel32.OpenProcess(access, False, pid)
        if not handle:
            skipped += 1
            continue
        try:
            if psapi.EmptyWorkingSet(handle):
                trimmed += 1
            else:
                skipped += 1
        finally:
            kernel32.CloseHandle(handle)
    time.sleep(1.5)
    after = _avail_phys()
    return {
        "freed_bytes": after - before,
        "before": before,
        "after": after,
        "processes_trimmed": trimmed,
        "processes_skipped": skipped,
    }
