import subprocess
import shutil
import os
from typing import List, Callable, Optional
from core.base_engine import BaseEngine
from core.models import PackageInfo, SearchResult, OperationStatus

class ChocolateyEngine(BaseEngine):
    """Chocolatey package manager engine implementation."""

    def __init__(self):
        self._executable = shutil.which("choco.exe") or shutil.which("choco")
        if not self._executable:
            candidate = r"C:\ProgramData\chocolatey\bin\choco.exe"
            if os.path.exists(candidate):
                self._executable = candidate
        self.active_proc: Optional[subprocess.Popen] = None

    @property
    def name(self) -> str:
        return "Chocolatey"

    @property
    def identifier(self) -> str:
        return "choco"

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
        """Run a choco command while streaming output in real-time completely hidden."""
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
                flags = 0x08000000  # CREATE_NO_WINDOW

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

            if proc.stdout:
                for line in iter(proc.stdout.readline, ''):
                    clean = line.strip()
                    if clean:
                        if on_log:
                            on_log(clean + "\n")
                        if on_progress:
                            on_progress(None, clean)
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

    def check_updates(self, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        if not self.is_available():
            if on_log:
                on_log("[Chocolatey]: Executable not found.\n")
            return []

        if on_log:
            on_log("[Chocolatey]: Scanning system for outdated packages...\n")

        try:
            startupinfo = None
            flags = 0
            if os.name == 'nt':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = 0
                flags = 0x08000000

            res = subprocess.run(
                [self._executable, "outdated", "-r"],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                startupinfo=startupinfo,
                creationflags=flags,
                timeout=60
            )

            if on_log:
                on_log(res.stdout)

            packages: List[PackageInfo] = []
            for line in res.stdout.splitlines():
                line = line.strip()
                if not line or "|" not in line:
                    continue
                parts = line.split("|")
                if len(parts) >= 3:
                    pkg_id = parts[0].strip()
                    curr_ver = parts[1].strip()
                    avail_ver = parts[2].strip()
                    pinned = parts[3].strip().lower() == "true" if len(parts) > 3 else False
                    
                    if pkg_id.lower() in ("id", "outdated packages", "warning", "error"):
                        continue

                    packages.append(PackageInfo(
                        name=pkg_id,
                        id=pkg_id,
                        installed_version=curr_ver,
                        available_version=avail_ver,
                        source="chocolatey",
                        engine="choco",
                        is_pinned=pinned
                    ))

            if on_log:
                on_log(f"\n[Chocolatey]: Scan complete. Found {len(packages)} outdated package(s).\n")

            return packages
        except Exception as e:
            if on_log:
                on_log(f"[Chocolatey Error]: {str(e)}\n")
            return []

    def list_installed(self, filter_query: Optional[str] = None, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        if not self.is_available():
            return []

        cmd = [self._executable, "list", "--local-only", "-r"]
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

            packages = []
            for line in res.stdout.splitlines():
                line = line.strip()
                if not line or "|" not in line:
                    continue
                parts = line.split("|")
                if len(parts) >= 2:
                    pkg_id = parts[0].strip()
                    version = parts[1].strip()
                    if filter_query and filter_query.lower() not in pkg_id.lower():
                        continue
                    packages.append(PackageInfo(
                        name=pkg_id,
                        id=pkg_id,
                        installed_version=version,
                        available_version=version,
                        source="chocolatey",
                        engine="choco"
                    ))
            return packages
        except Exception as e:
            if on_log:
                on_log(f"[Chocolatey List Error]: {str(e)}\n")
            return []

    def search_packages(self, query: str, on_log: Optional[Callable[[str], None]] = None) -> List[SearchResult]:
        if not self.is_available() or not query.strip():
            return []

        if on_log:
            on_log(f"[Chocolatey]: Searching community feed for '{query}'...\n")

        try:
            startupinfo = None
            flags = 0
            if os.name == 'nt':
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = 0
                flags = 0x08000000

            res = subprocess.run(
                [self._executable, "search", query, "-r", "--limit-output"],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                startupinfo=startupinfo,
                creationflags=flags,
                timeout=45
            )

            results = []
            for line in res.stdout.splitlines():
                line = line.strip()
                if not line or "|" not in line:
                    continue
                parts = line.split("|")
                if len(parts) >= 2:
                    pkg_id = parts[0].strip()
                    version = parts[1].strip()
                    results.append(SearchResult(
                        name=pkg_id,
                        id=pkg_id,
                        version=version,
                        source="chocolatey",
                        engine="choco"
                    ))
            if on_log:
                on_log(f"[Chocolatey]: Found {len(results)} package(s) matching '{query}'.\n")
            return results
        except Exception as e:
            if on_log:
                on_log(f"[Chocolatey Search Error]: {str(e)}\n")
            return []

    def upgrade_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        args = ["upgrade", package_id, "-y"]
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
        args = ["upgrade", "all", "-y"]
        code = self._run_streaming_cmd(args, on_log=on_log, on_progress=on_progress)
        success = (code == 0)
        msg = "All packages upgraded successfully." if success else f"Upgrade all finished with code {code}."
        return OperationStatus(package_id="ALL", success=success, message=msg, exit_code=code)

    def install_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        args = ["install", package_id, "-y"]
        code = self._run_streaming_cmd(args, on_log=on_log, on_progress=on_progress)
        success = (code == 0)
        msg = "Package installed successfully." if success else f"Installation exited with code {code}."
        return OperationStatus(package_id=package_id, success=success, message=msg, exit_code=code)

    def uninstall_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None
    ) -> OperationStatus:
        args = ["uninstall", package_id, "-y"]
        code = self._run_streaming_cmd(args, on_log=on_log)
        success = (code == 0)
        msg = "Package uninstalled successfully." if success else f"Uninstall exited with code {code}."
        return OperationStatus(package_id=package_id, success=success, message=msg, exit_code=code)
