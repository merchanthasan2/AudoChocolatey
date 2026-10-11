import subprocess
import shutil
import os
import re
from typing import List, Callable, Optional
from core.base_engine import BaseEngine
from core.models import PackageInfo, SearchResult, OperationStatus

class WingetEngine(BaseEngine):
    """Windows Package Manager (winget) engine implementation."""

    def __init__(self):
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

    def get_version(self) -> Optional[str]:
        if not self.is_available():
            return None
        try:
            flags = 0x08000000 if os.name == 'nt' else 0
            res = subprocess.run(
                [self._executable, "--version"],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=5,
                creationflags=flags
            )
            return res.stdout.strip()
        except Exception:
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
        on_progress: Optional[Callable[[Optional[float], str], None]] = None
    ) -> int:
        """
        Run a command while streaming output in real-time completely hidden.
        Handles carriage-return (\\r) updates used by Winget progress bars.
        """
        cmd = [self._executable] + args
        if on_log:
            on_log(f"> {' '.join(cmd)}\n")

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

            percent_re = re.compile(r'(\d{1,3})\s*%')
            ansi_escape = re.compile(r'[\x1b\x9b][\[()#;?]*(?:[0-9]{1,4}(?:;[0-9]{0,4})*)?[0-9A-ORZcf-nqry=><]')

            buffer = []
            if proc.stdout:
                while True:
                    char = proc.stdout.read(1)
                    if not char:
                        break

                    if char in ('\r', '\n'):
                        raw_line = "".join(buffer).strip()
                        buffer = []

                        if raw_line:
                            clean_line = ansi_escape.sub('', raw_line).strip()
                            clean_line = re.sub(r'[\x00-\x1f\x7f-\x9f]', ' ', clean_line).strip()

                            if clean_line:
                                if on_log:
                                    on_log(clean_line + "\n")

                                if on_progress:
                                    pct_match = percent_re.search(clean_line)
                                    pct_val = float(pct_match.group(1)) / 100.0 if pct_match else None
                                    on_progress(pct_val, clean_line)
                    else:
                        buffer.append(char)

                # Flush any remainder
                if buffer:
                    raw_line = "".join(buffer).strip()
                    if raw_line:
                        clean_line = ansi_escape.sub('', raw_line).strip()
                        if on_log:
                            on_log(clean_line + "\n")

                proc.stdout.close()

            proc.wait()
            return proc.returncode
        except Exception as e:
            if on_log:
                on_log(f"\n[Error executing command]: {str(e)}\n")
            if on_progress:
                on_progress(None, f"Error: {str(e)}")
            return -1
        finally:
            self.active_proc = None

    def _parse_table(self, text: str) -> List[dict]:
        """Generic column-based parser for winget tabular outputs."""
        lines = text.splitlines()
        header_idx = -1
        for i, line in enumerate(lines):
            if "Name" in line and "Id" in line:
                header_idx = i
                break

        if header_idx == -1 or header_idx + 1 >= len(lines):
            return []

        header_line = lines[header_idx]
        id_pos = header_line.find("Id")
        ver_pos = header_line.find("Version")
        avail_pos = header_line.find("Available") if "Available" in header_line else -1
        match_pos = header_line.find("Match") if "Match" in header_line else -1
        src_pos = header_line.find("Source") if "Source" in header_line else -1

        rows = []
        for line in lines[header_idx + 2:]:
            clean = line.strip()
            if not clean:
                continue
            if clean.startswith("-") or "upgrade available" in clean or "upgrades available" in clean:
                continue

            name = line[:id_pos].strip()
            if ver_pos != -1:
                pkg_id = line[id_pos:ver_pos].strip()
                if avail_pos != -1:
                    installed_ver = line[ver_pos:avail_pos].strip()
                    if src_pos != -1:
                        available_ver = line[avail_pos:src_pos].strip()
                        source = line[src_pos:].strip()
                    else:
                        available_ver = line[avail_pos:].strip()
                        source = "winget"
                elif match_pos != -1:
                    installed_ver = line[ver_pos:match_pos].strip()
                    available_ver = installed_ver
                    source = line[src_pos:].strip() if src_pos != -1 else "winget"
                else:
                    installed_ver = line[ver_pos:].strip()
                    available_ver = installed_ver
                    source = "winget"
            else:
                pkg_id = line[id_pos:].strip()
                installed_ver = ""
                available_ver = ""
                source = "winget"

            if pkg_id:
                rows.append({
                    "name": name or pkg_id,
                    "id": pkg_id,
                    "installed_ver": installed_ver,
                    "available_ver": available_ver,
                    "source": source or "winget"
                })
        return rows

    def check_updates(self, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        if not self.is_available():
            if on_log:
                on_log("[Winget]: Executable not found.\n")
            return []

        if on_log:
            on_log("[Winget]: Scanning system for available application updates...\n")

        try:
            startupinfo = None
            flags = 0
            if os.name == 'nt':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = 0
                flags = 0x08000000

            res = subprocess.run(
                [
                    self._executable,
                    "upgrade",
                    "--include-unknown",
                    "--disable-interactivity",
                    "--accept-source-agreements"
                ],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                startupinfo=startupinfo,
                creationflags=flags,
                timeout=120
            )

            if on_log:
                on_log(res.stdout)

            sections = res.stdout.split("The following packages have an upgrade available, but require explicit targeting for upgrade:")
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
                        explicit_upgrade_required=False
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
                        explicit_upgrade_required=True
                    ))

            if on_log:
                on_log(f"\n[Winget]: Scan complete. Found {len(packages)} update(s).\n")

            return packages
        except Exception as e:
            if on_log:
                on_log(f"[Winget Error]: {str(e)}\n")
            return []

    def list_installed(self, filter_query: Optional[str] = None, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        if not self.is_available():
            return []

        cmd = [self._executable, "list", "--disable-interactivity"]
        if filter_query:
            cmd.extend(["-q", filter_query])

        try:
            startupinfo = None
            flags = 0
            if os.name == 'nt':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = 0
                flags = 0x08000000

            res = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                startupinfo=startupinfo,
                creationflags=flags,
                timeout=45
            )

            rows = self._parse_table(res.stdout)
            packages = []
            for r in rows:
                packages.append(PackageInfo(
                    name=r["name"],
                    id=r["id"],
                    installed_version=r["installed_ver"],
                    available_version=r["installed_ver"],
                    source=r["source"],
                    engine="winget"
                ))
            return packages
        except Exception as e:
            if on_log:
                on_log(f"[Winget List Error]: {str(e)}\n")
            return []

    def search_packages(self, query: str, on_log: Optional[Callable[[str], None]] = None) -> List[SearchResult]:
        if not self.is_available() or not query.strip():
            return []

        if on_log:
            on_log(f"[Winget]: Searching repository for '{query}'...\n")

        try:
            startupinfo = None
            flags = 0
            if os.name == 'nt':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = 0
                flags = 0x08000000

            res = subprocess.run(
                [self._executable, "search", query, "--count", "30", "--disable-interactivity"],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                startupinfo=startupinfo,
                creationflags=flags,
                timeout=45
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
            if on_log:
                on_log(f"[Winget Search Error]: {str(e)}\n")
            return []

    def upgrade_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        args = ["upgrade", "--id", package_id, "--exact", "--disable-interactivity", "--force"]
        if silent:
            args.append("--silent")
        if accept_agreements:
            args.extend(["--accept-package-agreements", "--accept-source-agreements"])

        code = self._run_streaming_cmd(args, on_log=on_log, on_progress=on_progress)
        success = (code == 0)
        msg = "Upgrade completed successfully." if success else f"Upgrade exited with code {code}."
        return OperationStatus(package_id=package_id, success=success, message=msg, exit_code=code)

    def upgrade_all(
        self,
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        args = ["upgrade", "--all", "--include-unknown", "--disable-interactivity", "--force"]
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
        accept_agreements: bool = True
    ) -> OperationStatus:
        args = ["install", "--id", package_id, "--exact", "--disable-interactivity", "--force"]
        if silent:
            args.append("--silent")
        if accept_agreements:
            args.extend(["--accept-package-agreements", "--accept-source-agreements"])

        code = self._run_streaming_cmd(args, on_log=on_log, on_progress=on_progress)
        success = (code == 0)
        msg = "Package installed successfully." if success else f"Installation exited with code {code}."
        return OperationStatus(package_id=package_id, success=success, message=msg, exit_code=code)

    def uninstall_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None
    ) -> OperationStatus:
        args = ["uninstall", "--id", package_id, "--exact", "--disable-interactivity", "--force"]
        code = self._run_streaming_cmd(args, on_log=on_log)
        success = (code == 0)
        msg = "Package uninstalled successfully." if success else f"Uninstall exited with code {code}."
        return OperationStatus(package_id=package_id, success=success, message=msg, exit_code=code)
