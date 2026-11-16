import subprocess
import shutil
import os
import re
import time
import threading
from typing import Any, Dict, List, Callable, Optional
from core.base_engine import BaseEngine
from core.models import PackageInfo, SearchResult, OperationStatus
from core.files_in_use import files_in_use_operation_fields
from core.app_icons import find_main_exe
from core.winget_stream import stream_winget_stdout
from core.winget_user_context import (
    build_winget_install_args,
    is_needs_user_context_failure,
    run_winget_as_interactive_user,
    user_context_failure_message,
)

_EXPLICIT_TARGETING_SPLIT = re.compile(
    r"The following packages have[^\n]*explicit targeting[^\n]*for upgrade:\s*",
    re.IGNORECASE,
)

# Align winget catalog subprocess timeout with scan_orchestrator.CATALOG_BUDGET_S (60s).
WINGET_CATALOG_QUERY_TIMEOUT_S = 60
# Install/upgrade streaming can hang forever on MSI mutex / silent installer waits.
# Cap so batch updates can skip a stuck package and continue.
WINGET_STREAM_TIMEOUT_S = 600
WINGET_STREAM_TIMEOUT_EXIT = -999


class WingetEngine(BaseEngine):
    """Windows Package Manager (winget) engine implementation."""

    _download_slots = threading.Semaphore(3)
    # One catalog query at a time. Overlapping list/upgrade/search calls block
    # each other inside winget and then hit our timeout with an empty result.
    _query_lock = threading.Lock()
    _active_captures: Dict[str, subprocess.Popen] = {}
    _cancel_events: Dict[str, threading.Event] = {}

    def __init__(self):
        self.last_error = ""
        self._executable = shutil.which("winget.exe") or shutil.which("winget")
        if not self._executable:
            local_appdata = os.environ.get("LOCALAPPDATA", "")
            candidate = os.path.join(local_appdata, "Microsoft", "WindowsApps", "winget.exe")
            if os.path.exists(candidate):
                self._executable = candidate
        self.active_proc: Optional[subprocess.Popen] = None

    @property
    def name(self) -> str:
        return "Windows Package Manager (Winget)"

    @property
    def identifier(self) -> str:
        return "winget"

    def is_available(self) -> bool:
        return self._executable is not None and os.path.exists(self._executable)

    def _run_capture(
        self,
        args: List[str],
        timeout: int,
        job_id: Optional[str] = None,
    ) -> subprocess.CompletedProcess:
        """Run one catalog query. Callers share _query_lock so winget is not started twice."""
        startupinfo = None
        flags = 0
        if os.name == "nt":
            startupinfo = subprocess.STARTUPINFO()
            startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            startupinfo.wShowWindow = 0
            flags = 0x08000000
        cancel_ev = None
        if job_id:
            cancel_ev = WingetEngine._cancel_events.setdefault(job_id, threading.Event())
        with WingetEngine._query_lock:
            if cancel_ev and cancel_ev.is_set():
                raise RuntimeError("cancelled")
            proc = subprocess.Popen(
                args,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                encoding="utf-8",
                errors="replace",
                startupinfo=startupinfo,
                creationflags=flags,
            )
            if job_id:
                WingetEngine._active_captures[job_id] = proc
            started = time.perf_counter()
            try:
                stdout, stderr = proc.communicate(timeout=timeout)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.communicate()
                raise
            finally:
                if job_id:
                    WingetEngine._active_captures.pop(job_id, None)
            duration = time.perf_counter() - started
            if cancel_ev and cancel_ev.is_set():
                raise RuntimeError("cancelled")
            if job_id:
                try:
                    from core.scan_trace import trace_event

                    trace_event(
                        "winget_call",
                        job_id=job_id,
                        duration_s=round(duration, 3),
                        exit_code=proc.returncode,
                        command=args[:3],
                    )
                except Exception:
                    pass
            return subprocess.CompletedProcess(args, proc.returncode, stdout, stderr)

    def get_version(self) -> Optional[str]:
        if not self.is_available():
            return None
        self.last_error = ""
        try:
            res = self._run_capture([self._executable, "--version"], timeout=5)
            return res.stdout.strip()
        except Exception as e:
            self.last_error = str(e)
            return None

    def abort(self):
        """Immediately abort active process and its child processes."""
        if self.active_proc and self.active_proc.poll() is None:
            try:
                pid = self.active_proc.pid
                flags = 0x08000000 if os.name == 'nt' else 0
                subprocess.run(
                    ["taskkill", "/F", "/T", "/PID", str(pid)],
                    capture_output=True,
                    creationflags=flags
                )
            except Exception:
                pass
            try:
                self.active_proc.kill()
            except Exception:
                pass
            self.active_proc = None

    def _run_streaming_cmd(
        self,
        args: List[str],
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        timeout_s: int = WINGET_STREAM_TIMEOUT_S,
    ) -> int:
        """
        Run a command while streaming output in real-time completely hidden.
        Handles carriage-return (\\r) updates used by Winget progress bars.
        Kills the process tree if winget stalls past timeout_s so batch upgrades continue.
        """
        cmd = [self._executable] + args
        if on_log:
            on_log(f"> {' '.join(cmd)}\n")

        # Drop a leftover winget from a prior package so the next upgrade does not wait forever.
        self.abort()

        try:
            startupinfo = None
            flags = 0
            if os.name == 'nt':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = 0
                flags = 0x08000000  # CREATE_NO_WINDOW: ensures child processes remain fully hidden

            proc = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf-8",
                errors="replace",
                startupinfo=startupinfo,
                creationflags=flags,
                bufsize=1
            )
            self.active_proc = proc
            done = threading.Event()
            box: Dict[str, Any] = {"code": -1, "error": None}

            def _reader() -> None:
                try:
                    if proc.stdout:
                        stream_winget_stdout(proc.stdout, on_log=on_log, on_progress=on_progress)
                        try:
                            proc.stdout.close()
                        except Exception:
                            pass
                    box["code"] = proc.wait()
                except Exception as exc:
                    box["error"] = exc
                    box["code"] = -1
                finally:
                    done.set()

            reader = threading.Thread(target=_reader, name="winget-stream", daemon=True)
            reader.start()
            limit = max(30, int(timeout_s or WINGET_STREAM_TIMEOUT_S))
            if not done.wait(limit):
                if on_log:
                    on_log(
                        f"\n[Winget]: Timed out after {limit}s — stopping this package "
                        "so the next update can run.\n"
                    )
                if on_progress:
                    on_progress(None, f"Timed out after {limit}s")
                self.abort()
                done.wait(5)
                return WINGET_STREAM_TIMEOUT_EXIT

            if box["error"] is not None:
                raise box["error"]
            return int(box["code"])
        except Exception as e:
            if on_log:
                on_log(f"\n[Error executing command]: {str(e)}\n")
            if on_progress:
                on_progress(None, f"Error: {str(e)}")
            return -1
        finally:
            self.active_proc = None

    def _merge_stream_log(
        self,
        line: str,
        capture: List[str],
        on_log: Optional[Callable[[str], None]],
    ) -> None:
        capture.append(line)
        if on_log:
            on_log(line)

    def _failed_operation_status(
        self,
        package_id: str,
        code: int,
        log_text: str,
        verb: str,
    ) -> OperationStatus:
        files_in_use = files_in_use_operation_fields(
            package_id,
            code,
            log_text,
            main_exe_finder=find_main_exe,
        )
        if files_in_use:
            message, holders = files_in_use
            return OperationStatus(
                package_id=package_id,
                success=False,
                message=message,
                exit_code=code,
                reason="files_in_use",
                holders=holders,
            )
        return OperationStatus(
            package_id=package_id,
            success=False,
            message=f"{verb} exited with code {code}.",
            exit_code=code,
        )

    def _parse_table(self, text: str) -> List[dict]:
        """Generic column-based parser for winget tabular outputs."""
        lines = text.splitlines()
        rows: List[dict] = []
        seen_ids: set = set()
        i = 0

        def _is_progress_line(raw: str, id_column: int) -> bool:
            if "%" not in raw:
                return False
            if len(raw.rstrip()) < id_column:
                return True
            if re.search(r"[\|\\/#█▓░]+.*%", raw):
                return True
            if re.match(r"^\s*[\d.]+\s*%", raw.strip()):
                return True
            return False

        while i < len(lines):
            line = lines[i]
            if "Name" not in line or "Id" not in line or "Version" not in line:
                i += 1
                continue

            header_line = line
            id_pos = header_line.find("Id")
            ver_pos = header_line.find("Version")
            avail_pos = header_line.find("Available") if "Available" in header_line else -1
            match_pos = header_line.find("Match") if "Match" in header_line else -1
            src_pos = header_line.find("Source") if "Source" in header_line else -1

            i += 1
            if i < len(lines) and lines[i].strip().startswith("-"):
                i += 1

            while i < len(lines):
                row_line = lines[i]
                clean = row_line.strip()
                if not clean:
                    i += 1
                    continue
                if "Name" in row_line and "Id" in row_line and "Version" in row_line:
                    break
                if clean.startswith("-") or "upgrades available" in clean.lower():
                    i += 1
                    continue
                if clean.startswith("The following"):
                    i += 1
                    continue
                if _is_progress_line(row_line, id_pos):
                    i += 1
                    continue
                if len(row_line.rstrip("\n")) < id_pos:
                    i += 1
                    continue

                name = row_line[:id_pos].strip()
                if name.endswith("…"):
                    name = name[:-1].rstrip()
                elif name.endswith("..."):
                    name = name[:-3].rstrip()

                if ver_pos != -1:
                    pkg_id = row_line[id_pos:ver_pos].strip()
                    if avail_pos != -1:
                        installed_ver = row_line[ver_pos:avail_pos].strip()
                        if src_pos != -1 and len(row_line) > src_pos:
                            available_ver = row_line[avail_pos:src_pos].strip()
                            source = row_line[src_pos:].strip()
                        else:
                            available_ver = row_line[avail_pos:].strip() if len(row_line) > avail_pos else ""
                            source = "winget"
                    elif match_pos != -1:
                        installed_ver = row_line[ver_pos:match_pos].strip()
                        available_ver = installed_ver
                        source = row_line[src_pos:].strip() if src_pos != -1 and len(row_line) > src_pos else "winget"
                    else:
                        installed_ver = row_line[ver_pos:].strip() if len(row_line) > ver_pos else ""
                        available_ver = installed_ver
                        source = "winget"
                else:
                    pkg_id = row_line[id_pos:].strip()
                    installed_ver = ""
                    available_ver = ""
                    source = "winget"

                if pkg_id and pkg_id not in seen_ids:
                    seen_ids.add(pkg_id)
                    rows.append({
                        "name": name or pkg_id,
                        "id": pkg_id,
                        "installed_ver": installed_ver,
                        "available_ver": available_ver,
                        "source": source or "winget"
                    })
                i += 1
        return rows

    def check_updates(
        self,
        on_log: Optional[Callable[[str], None]] = None,
        job_id: Optional[str] = None,
    ) -> List[PackageInfo]:
        if not self.is_available():
            if on_log:
                on_log("[Winget]: Executable not found.\n")
            return []

        if on_log:
            on_log("[Winget]: Scanning system for available application updates...\n")

        self.last_error = ""
        try:
            res = self._run_capture(
                [
                    self._executable,
                    "upgrade",
                    "--include-unknown",
                    "--disable-interactivity",
                    "--accept-source-agreements",
                ],
                timeout=WINGET_CATALOG_QUERY_TIMEOUT_S,
                job_id=job_id,
            )

            if on_log:
                on_log(res.stdout)

            sections = _EXPLICIT_TARGETING_SPLIT.split(res.stdout, maxsplit=1)
            if len(sections) == 1:
                sections = [res.stdout, ""]
            main_rows = self._parse_table(sections[0])
            explicit_rows = self._parse_table(sections[1]) if len(sections) > 1 else []

            packages: List[PackageInfo] = []
            seen_ids = set()

            for r in main_rows:
                if r["id"] not in seen_ids:
                    seen_ids.add(r["id"])
                    packages.append(PackageInfo(
                        name=r["name"],
                        id=r["id"],
                        installed_version=r["installed_ver"],
                        available_version=r["available_ver"],
                        source=r["source"],
                        engine="winget",
                        explicit_upgrade_required=False,
                        origin="catalog",
                        can_uninstall=True,
                        uninstall_kind="catalog",
                    ))

            for r in explicit_rows:
                if r["id"] not in seen_ids:
                    seen_ids.add(r["id"])
                    packages.append(PackageInfo(
                        name=r["name"],
                        id=r["id"],
                        installed_version=r["installed_ver"],
                        available_version=r["available_ver"],
                        source=r["source"],
                        engine="winget",
                        explicit_upgrade_required=True,
                        origin="catalog",
                        can_uninstall=True,
                        uninstall_kind="catalog",
                    ))

            if on_log:
                on_log(f"\n[Winget]: Scan complete. Found {len(packages)} update(s).\n")

            return packages
        except Exception as e:
            self.last_error = str(e)
            if on_log:
                on_log(f"[Winget Error]: {str(e)}\n")
            return []

    def list_installed(
        self,
        filter_query: Optional[str] = None,
        on_log: Optional[Callable[[str], None]] = None,
        job_id: Optional[str] = None,
    ) -> List[PackageInfo]:
        if not self.is_available():
            return []

        cmd = [
            self._executable,
            "list",
            "--disable-interactivity",
            "--accept-source-agreements",
        ]
        if filter_query:
            cmd.extend(["-q", filter_query])

        self.last_error = ""
        try:
            res = self._run_capture(cmd, timeout=WINGET_CATALOG_QUERY_TIMEOUT_S, job_id=job_id)

            rows = self._parse_table(res.stdout)
            packages = []
            for r in rows:
                packages.append(PackageInfo(
                    name=r["name"],
                    id=r["id"],
                    installed_version=r["installed_ver"],
                    available_version=r["installed_ver"],
                    source=r["source"],
                    engine="winget",
                    origin="catalog",
                    can_uninstall=True,
                    uninstall_kind="catalog",
                ))
            return packages
        except Exception as e:
            self.last_error = str(e)
            if on_log:
                on_log(f"[Winget List Error]: {str(e)}\n")
            return []

    def search_packages(self, query: str, on_log: Optional[Callable[[str], None]] = None) -> List[SearchResult]:
        if not self.is_available() or not query.strip():
            return []

        if on_log:
            on_log(f"[Winget]: Searching repository for '{query}'...\n")

        self.last_error = ""
        try:
            res = self._run_capture(
                [self._executable, "search", query, "--count", "100", "--accept-source-agreements", "--disable-interactivity"],
                timeout=WINGET_CATALOG_QUERY_TIMEOUT_S,
            )

            rows = self._parse_table(res.stdout)
            results = []
            for r in rows:
                results.append(SearchResult(
                    name=r["name"],
                    id=r["id"],
                    version=r["installed_ver"],
                    source=r["source"],
                    engine="winget"
                ))
            if on_log:
                on_log(f"[Winget]: Found {len(results)} package(s) matching '{query}'.\n")
            return results
        except Exception as e:
            self.last_error = str(e)
            if on_log:
                on_log(f"[Winget Search Error]: {str(e)}\n")
            return []

    def upgrade_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True,
        force: bool = False,
    ) -> OperationStatus:
        args = ["upgrade", "--id", package_id, "--exact", "--include-unknown", "--disable-interactivity"]
        if force:
            args.append("--force")
        if silent:
            args.append("--silent")
        if accept_agreements:
            args.extend(["--accept-package-agreements", "--accept-source-agreements"])

        captured: List[str] = []
        code = self._run_streaming_cmd(
            args,
            on_log=lambda line: self._merge_stream_log(line, captured, on_log),
            on_progress=on_progress,
            timeout_s=WINGET_STREAM_TIMEOUT_S,
        )
        if code == WINGET_STREAM_TIMEOUT_EXIT:
            return OperationStatus(
                package_id=package_id,
                success=False,
                message=(
                    f"Upgrade timed out after {WINGET_STREAM_TIMEOUT_S // 60} minutes. "
                    "Skipped so the next package can continue — retry this one alone."
                ),
                exit_code=code,
                reason="timeout",
            )
        # 0: Success, 3010: Success Reboot Required, -1978335189: Already latest, -1978335181: Reboot required
        success = (code in (0, 3010, -1978335189, -1978335181))
        if success:
            msg = "Upgrade completed successfully." if code == 0 else f"Upgrade completed (status code {code})."
            return OperationStatus(package_id=package_id, success=success, message=msg, exit_code=code)
        return self._failed_operation_status(package_id, code, "".join(captured), "Upgrade")

    def upgrade_all(
        self,
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True,
        force: bool = False,
    ) -> OperationStatus:
        args = ["upgrade", "--all", "--include-unknown", "--disable-interactivity"]
        if force:
            args.append("--force")
        if silent:
            args.append("--silent")
        if accept_agreements:
            args.extend(["--accept-package-agreements", "--accept-source-agreements"])

        code = self._run_streaming_cmd(args, on_log=on_log, on_progress=on_progress)
        success = (code == 0)
        msg = "All available packages upgraded successfully." if success else f"Upgrade all finished with code {code}."
        return OperationStatus(package_id="ALL", success=success, message=msg, exit_code=code)

    def install_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True,
        force: bool = False,
    ) -> OperationStatus:
        args = build_winget_install_args(
            package_id,
            silent=silent,
            accept_agreements=accept_agreements,
            force=force,
        )

        captured: List[str] = []
        code = self._run_streaming_cmd(
            args,
            on_log=lambda line: self._merge_stream_log(line, captured, on_log),
            on_progress=on_progress,
        )
        if code == 0:
            return OperationStatus(
                package_id=package_id,
                success=True,
                message="Package installed successfully.",
                exit_code=code,
            )

        log_text = "".join(captured)
        if is_needs_user_context_failure(code, log_text):
            if on_log:
                on_log(
                    "\n[Winget]: Installer refused administrator context "
                    "(0x8A150056). Retrying as the signed-in user...\n"
                )
            retry_code, retry_log = run_winget_as_interactive_user(
                self._executable,
                args,
                on_log=on_log,
                on_progress=on_progress,
            )
            combined = log_text + retry_log
            if retry_code == 0:
                return OperationStatus(
                    package_id=package_id,
                    success=True,
                    message="Package installed successfully.",
                    exit_code=0,
                )
            return OperationStatus(
                package_id=package_id,
                success=False,
                message=user_context_failure_message(combined),
                exit_code=retry_code if retry_code != -1 else code,
                reason="needs-user-context",
            )

        return self._failed_operation_status(package_id, code, log_text, "Installation")

    def download_package(
        self,
        package_id: str,
        on_log: Optional[Callable[[str], None]] = None,
    ) -> OperationStatus:
        if not self.is_available():
            return OperationStatus(
                package_id=package_id,
                success=False,
                message="Winget executable not found.",
                exit_code=-1,
            )

        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._+-]{0,180}", package_id or ""):
            return OperationStatus(
                package_id=package_id,
                success=False,
                message="That package id cannot be downloaded.",
                exit_code=-1,
            )

        local_appdata = os.environ.get("LOCALAPPDATA", "")
        dest_dir = os.path.join(local_appdata, "QuantumUpdater", "downloads", package_id)
        os.makedirs(dest_dir, exist_ok=True)

        args = [
            "download",
            "--id",
            package_id,
            "--exact",
            "--disable-interactivity",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "-d",
            dest_dir,
        ]

        self._download_slots.acquire()
        try:
            if on_log:
                on_log(f"[Winget]: Downloading package '{package_id}' to {dest_dir}...\n")

            startupinfo = None
            flags = 0
            if os.name == "nt":
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = 0
                flags = 0x08000000

            proc = subprocess.run(
                [self._executable] + args,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                startupinfo=startupinfo,
                creationflags=flags,
                timeout=900,
            )
            combined = (proc.stdout or "") + (proc.stderr or "")
            if on_log and combined.strip():
                on_log(combined + "\n")

            success = proc.returncode == 0
            if success:
                msg = f"Download completed: {dest_dir}"
            else:
                msg = f"Download exited with code {proc.returncode}."
            return OperationStatus(
                package_id=package_id,
                success=success,
                message=msg,
                exit_code=proc.returncode,
            )
        except subprocess.TimeoutExpired:
            return OperationStatus(
                package_id=package_id,
                success=False,
                message="Download timed out after 15 minutes.",
                exit_code=-1,
            )
        except Exception as e:
            return OperationStatus(
                package_id=package_id,
                success=False,
                message=str(e),
                exit_code=-1,
            )
        finally:
            self._download_slots.release()

    def refresh_sources(self, timeout: int = 120) -> Dict[str, Any]:
        """Refresh package catalog sources (winget source update). Everyday repair."""
        if not self.is_available():
            return {"ok": False, "error": "WinGet is not available on this PC.", "exit_code": -1}
        self.last_error = ""
        try:
            res = self._run_capture(
                [self._executable, "source", "update", "--disable-interactivity"],
                timeout=timeout,
            )
            out = ((res.stdout or "") + "\n" + (res.stderr or "")).strip()
            ok = res.returncode == 0
            if not ok:
                self.last_error = out or f"winget source update exited {res.returncode}"
            return {
                "ok": ok,
                "exit_code": res.returncode,
                "message": (
                    "Catalog sources refreshed."
                    if ok
                    else (out or f"Refresh failed (exit {res.returncode}).")
                ),
                "detail": out[:2000],
            }
        except subprocess.TimeoutExpired:
            self.last_error = "winget source update timed out"
            return {"ok": False, "error": "Catalog source refresh timed out.", "exit_code": -1}
        except Exception as e:
            self.last_error = str(e)
            return {"ok": False, "error": str(e), "exit_code": -1}

    def reset_sources(self, confirmed: bool = False, timeout: int = 120) -> Dict[str, Any]:
        """Reset catalog sources to defaults (winget source reset --force). Recovery only."""
        if not confirmed:
            return {
                "ok": False,
                "error": "Reset requires explicit confirmation.",
                "exit_code": -1,
                "needs_confirm": True,
            }
        if not self.is_available():
            return {"ok": False, "error": "WinGet is not available on this PC.", "exit_code": -1}
        self.last_error = ""
        try:
            res = self._run_capture(
                [
                    self._executable,
                    "source",
                    "reset",
                    "--force",
                    "--disable-interactivity",
                ],
                timeout=timeout,
            )
            out = ((res.stdout or "") + "\n" + (res.stderr or "")).strip()
            ok = res.returncode == 0
            if not ok:
                self.last_error = out or f"winget source reset exited {res.returncode}"
            return {
                "ok": ok,
                "exit_code": res.returncode,
                "message": (
                    "Catalog sources reset to defaults."
                    if ok
                    else (out or f"Reset failed (exit {res.returncode}).")
                ),
                "detail": out[:2000],
                "recovery": True,
            }
        except subprocess.TimeoutExpired:
            self.last_error = "winget source reset timed out"
            return {"ok": False, "error": "Catalog source reset timed out.", "exit_code": -1}
        except Exception as e:
            self.last_error = str(e)
            return {"ok": False, "error": str(e), "exit_code": -1}

    def uninstall_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
    ) -> OperationStatus:
        args = ["uninstall", "--id", package_id, "--exact", "--disable-interactivity", "--force"]
        code = self._run_streaming_cmd(args, on_log=on_log, on_progress=on_progress)
        success = (code == 0)
        msg = "Package uninstalled successfully." if success else f"Uninstall exited with code {code}."
        return OperationStatus(package_id=package_id, success=success, message=msg, exit_code=code)


def cancel_catalog_query(job_id: str) -> None:
    ev = WingetEngine._cancel_events.setdefault(job_id, threading.Event())
    ev.set()
    proc = WingetEngine._active_captures.get(job_id)
    if proc and proc.poll() is None:
        try:
            proc.kill()
        except Exception:
            pass
