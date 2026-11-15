"""Run winget installs under the signed-in interactive user when elevation blocks setup."""

from __future__ import annotations

import ctypes
import os
import re
import subprocess
from ctypes import wintypes
from typing import Callable, List, Optional, Tuple

from core.winget_stream import stream_winget_stdout

# WinGet APPINSTALLER_ERROR_* (signed int32)
WINGET_ERROR_CANNOT_RUN_AS_ADMIN = -1978335146  # HRESULT 0x8A150056

_ADMIN_CONTEXT_PHRASES = (
    "cannot be run from an administrator context",
    "installer cannot be run from an administrator",
    "cannot run from an administrator context",
)

_USER_CONTEXT_EXIT_CODES = frozenset({WINGET_ERROR_CANNOT_RUN_AS_ADMIN})


def winget_hresult_to_exit_code(hresult_hex: str) -> Optional[int]:
    try:
        value = int(hresult_hex, 16)
        if value > 0x7FFFFFFF:
            value -= 0x100000000
        return int(value)
    except (TypeError, ValueError):
        return None


def is_needs_user_context_failure(exit_code: int, output: str) -> bool:
    if exit_code in _USER_CONTEXT_EXIT_CODES:
        return True
    if winget_hresult_to_exit_code("0x8A150056") == exit_code:
        return True
    text = (output or "").casefold()
    if any(phrase in text for phrase in _ADMIN_CONTEXT_PHRASES):
        return True
    if "0x8a150056" in text:
        return True
    return False


def build_winget_install_args(
    package_id: str,
    *,
    silent: bool = True,
    accept_agreements: bool = True,
    force: bool = False,
) -> List[str]:
    args = ["install", "--id", package_id, "--exact", "--disable-interactivity"]
    if force:
        args.append("--force")
    if silent:
        args.append("--silent")
    if accept_agreements:
        args.extend(["--accept-package-agreements", "--accept-source-agreements"])
    return args


def build_user_context_install_command(
    winget_executable: str,
    package_id: str,
    *,
    silent: bool = True,
    accept_agreements: bool = True,
    force: bool = False,
) -> List[str]:
    """Argv for an unelevated winget install retry (tested with Spotify.Spotify)."""
    return [winget_executable] + build_winget_install_args(
        package_id,
        silent=silent,
        accept_agreements=accept_agreements,
        force=force,
    )


def short_winget_failure_reason(log_text: str) -> str:
    lines = [ln.strip() for ln in (log_text or "").splitlines() if ln.strip()]
    for line in reversed(lines):
        lowered = line.casefold()
        if lowered.startswith("failed:") or "error" in lowered or "exited" in lowered:
            return line[:240]
    for line in reversed(lines):
        if not line.startswith(">"):
            return line[:240]
    return ""


def user_context_failure_message(log_text: str) -> str:
    short = short_winget_failure_reason(log_text)
    base = (
        "The installer cannot run as administrator and the retry as the signed-in user failed"
    )
    if short:
        return f"{base}: {short}"
    return f"{base}."


def _find_explorer_pid() -> Optional[int]:
    if os.name != "nt":
        return None

    class PROCESSENTRY32W(ctypes.Structure):
        _fields_ = [
            ("dwSize", wintypes.DWORD),
            ("cntUsage", wintypes.DWORD),
            ("th32ProcessID", wintypes.DWORD),
            ("th32DefaultHeapID", ctypes.POINTER(wintypes.ULONG)),
            ("th32ModuleID", wintypes.DWORD),
            ("cntThreads", wintypes.DWORD),
            ("th32ParentProcessID", wintypes.DWORD),
            ("pcPriClassBase", wintypes.LONG),
            ("dwFlags", wintypes.DWORD),
            ("szExeFile", wintypes.WCHAR * 260),
        ]

    TH32CS_SNAPPROCESS = 0x00000002
    kernel32 = ctypes.windll.kernel32
    snap = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    if snap in (0, -1):
        return None
    try:
        entry = PROCESSENTRY32W()
        entry.dwSize = ctypes.sizeof(PROCESSENTRY32W)
        if not kernel32.Process32FirstW(snap, ctypes.byref(entry)):
            return None
        while True:
            name = (entry.szExeFile or "").casefold()
            if name == "explorer.exe":
                return int(entry.th32ProcessID)
            if not kernel32.Process32NextW(snap, ctypes.byref(entry)):
                break
    finally:
        kernel32.CloseHandle(snap)
    return None


def _get_primary_token_for_pid(pid: int):
    kernel32 = ctypes.windll.kernel32
    advapi32 = ctypes.windll.advapi32
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    TOKEN_DUPLICATE = 0x0002
    TOKEN_QUERY = 0x0008
    TOKEN_ASSIGN_PRIMARY = 0x0001
    SecurityImpersonation = 2
    TokenPrimary = 1

    proc = kernel32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not proc:
        return None
    token = wintypes.HANDLE()
    try:
        if not advapi32.OpenProcessToken(proc, TOKEN_DUPLICATE | TOKEN_QUERY, ctypes.byref(token)):
            return None
        dup = wintypes.HANDLE()
        if not advapi32.DuplicateTokenEx(
            token,
            TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY,
            None,
            SecurityImpersonation,
            TokenPrimary,
            ctypes.byref(dup),
        ):
            return None
        return dup
    finally:
        kernel32.CloseHandle(proc)
        if token:
            kernel32.CloseHandle(token)


class _UserContextProcess:
    def __init__(self, process_handle, thread_handle, stdout_reader):
        self._process = process_handle
        self._thread = thread_handle
        self.stdout = stdout_reader
        self.returncode: Optional[int] = None

    def wait(self) -> int:
        kernel32 = ctypes.windll.kernel32
        kernel32.WaitForSingleObject(self._process, 0xFFFFFFFF)
        code = wintypes.DWORD()
        kernel32.GetExitCodeProcess(self._process, ctypes.byref(code))
        self.returncode = int(code.value)
        kernel32.CloseHandle(self._process)
        if self._thread:
            kernel32.CloseHandle(self._thread)
        return self.returncode


def _create_process_with_token(command_line: str, primary_token) -> Optional[_UserContextProcess]:
    import msvcrt

    kernel32 = ctypes.windll.kernel32
    advapi32 = ctypes.windll.advapi32

    sa = wintypes.SECURITY_ATTRIBUTES()
    sa.nLength = ctypes.sizeof(sa)
    sa.bInheritHandle = True

    read_h = wintypes.HANDLE()
    write_h = wintypes.HANDLE()
    if not kernel32.CreatePipe(ctypes.byref(read_h), ctypes.byref(write_h), ctypes.byref(sa), 0):
        return None

    kernel32.SetHandleInformation(read_h, 1, 0)

    class STARTUPINFOW(ctypes.Structure):
        _fields_ = [
            ("cb", wintypes.DWORD),
            ("lpReserved", wintypes.LPWSTR),
            ("lpDesktop", wintypes.LPWSTR),
            ("lpTitle", wintypes.LPWSTR),
            ("dwX", wintypes.DWORD),
            ("dwY", wintypes.DWORD),
            ("dwXSize", wintypes.DWORD),
            ("dwYSize", wintypes.DWORD),
            ("dwXCountChars", wintypes.DWORD),
            ("dwYCountChars", wintypes.DWORD),
            ("dwFillAttribute", wintypes.DWORD),
            ("dwFlags", wintypes.DWORD),
            ("wShowWindow", wintypes.WORD),
            ("cbReserved2", wintypes.WORD),
            ("lpReserved2", ctypes.POINTER(wintypes.BYTE)),
            ("hStdInput", wintypes.HANDLE),
            ("hStdOutput", wintypes.HANDLE),
            ("hStdError", wintypes.HANDLE),
        ]

    try:
        si = STARTUPINFOW()
        si.cb = ctypes.sizeof(STARTUPINFOW)
        si.dwFlags = 0x00000001 | 0x00000100  # STARTF_USESHOWWINDOW | STARTF_USESTDHANDLES
        si.wShowWindow = 0  # SW_HIDE
        si.hStdOutput = write_h
        si.hStdError = write_h

        pi = wintypes.PROCESS_INFORMATION()
        cmd_buf = ctypes.create_unicode_buffer(command_line)
        CREATE_UNICODE_ENVIRONMENT = 0x00000400

        ok = advapi32.CreateProcessWithTokenW(
            primary_token,
            0,
            None,
            cmd_buf,
            CREATE_UNICODE_ENVIRONMENT,
            None,
            None,
            ctypes.byref(si),
            ctypes.byref(pi),
        )
        kernel32.CloseHandle(write_h)
        if not ok:
            kernel32.CloseHandle(read_h)
            return None

        reader = os.fdopen(
            msvcrt.open_osfhandle(read_h, os.O_RDONLY),
            "r",
            encoding="utf-8",
            errors="replace",
        )
        return _UserContextProcess(pi.hProcess, pi.hThread, reader)
    except Exception:
        try:
            kernel32.CloseHandle(write_h)
        except Exception:
            pass
        try:
            kernel32.CloseHandle(read_h)
        except Exception:
            pass
        return None


def run_winget_as_interactive_user(
    winget_executable: str,
    args: List[str],
    on_log: Optional[Callable[[str], None]] = None,
    on_progress: Optional[Callable[[Optional[float], str], None]] = None,
) -> Tuple[int, str]:
    """
    Start winget under the interactive user's token (explorer.exe).
    Returns (exit_code, combined_log_capture).
    """
    if os.name != "nt":
        return -1, ""

    pid = _find_explorer_pid()
    if not pid:
        return -1, "Could not find explorer.exe for interactive user token."

    token = _get_primary_token_for_pid(pid)
    if not token:
        return -1, "Could not duplicate interactive user token."

    kernel32 = ctypes.windll.kernel32
    try:
        quoted = []
        for part in [winget_executable] + args:
            if re.search(r"\s", part):
                quoted.append(f'"{part}"')
            else:
                quoted.append(part)
        command_line = " ".join(quoted)

        proc = _create_process_with_token(command_line, token)
        if proc is None:
            return -1, "CreateProcessWithTokenW failed for winget retry."

        captured: List[str] = []

        def merge_log(line: str) -> None:
            captured.append(line)
            if on_log:
                on_log(line)

        if proc.stdout:
            stream_winget_stdout(proc.stdout, on_log=merge_log, on_progress=on_progress)
            proc.stdout.close()

        code = proc.wait()
        return code, "".join(captured)
    finally:
        kernel32.CloseHandle(token)
