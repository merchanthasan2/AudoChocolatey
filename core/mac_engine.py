import os
import re
import json
import shutil
import plistlib
import subprocess
import threading
from typing import List, Optional, Callable, Dict, Any
from core.models import PackageInfo, SearchResult, OperationStatus
from core.base_engine import BaseEngine

def find_brew_binary() -> Optional[str]:
    """Resolve brew binary across Apple Silicon (/opt/homebrew) and Intel (/usr/local)."""
    for candidate in [
        shutil.which("brew"),
        "/opt/homebrew/bin/brew",
        "/usr/local/bin/brew"
    ]:
        if candidate and os.path.isfile(candidate) and os.access(candidate, os.X_OK):
            return candidate
    return None

def find_mas_binary() -> Optional[str]:
    """Resolve Mac App Store (mas) binary."""
    candidate = shutil.which("mas")
    if candidate and os.path.isfile(candidate) and os.access(candidate, os.X_OK):
        return candidate
    for extra in ["/opt/homebrew/bin/mas", "/usr/local/bin/mas"]:
        if os.path.isfile(extra) and os.access(extra, os.X_OK):
            return extra
    return None

class HomebrewEngine(BaseEngine):
    """Homebrew Package Manager Engine for macOS (Casks & Formulae)."""

    def __init__(self):
        self._brew_path = find_brew_binary()
        self._active_proc: Optional[subprocess.Popen] = None

    @property
    def name(self) -> str:
        return "Homebrew (Casks & Formulae)"

    @property
    def identifier(self) -> str:
        return "brew"

    def is_available(self) -> bool:
        if not self._brew_path:
            self._brew_path = find_brew_binary()
        return self._brew_path is not None

    def get_version(self) -> Optional[str]:
        if not self.is_available():
            return None
        try:
            res = subprocess.run([self._brew_path, "--version"], capture_output=True, text=True, timeout=10)
            if res.returncode == 0:
                first_line = res.stdout.strip().split("\n")[0]
                return first_line.replace("Homebrew", "").strip()
        except Exception:
            pass
        return None

    def check_updates(self, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        if not self.is_available():
            if on_log:
                on_log("Homebrew is not installed. Download from https://brew.sh")
            return []

        if on_log:
            on_log("Checking Homebrew updates (formulae and GUI casks)...")

        try:
            # First fetch latest metadata
            subprocess.run([self._brew_path, "update"], capture_output=True, text=True, timeout=60)
            
            # Use json=v2 for clean programmatic output
            res = subprocess.run([self._brew_path, "outdated", "--json=v2"], capture_output=True, text=True, timeout=45)
            packages = []

            if res.returncode == 0 and res.stdout.strip():
                data = json.loads(res.stdout)
                # Parse formulae
                for f in data.get("formulae", []):
                    name = f.get("name", "")
                    inst = f.get("installed_versions", ["Unknown"])[0] if f.get("installed_versions") else "Unknown"
                    curr = f.get("current_version", "")
                    packages.append(PackageInfo(
                        id=name,
                        name=f"{name} (CLI Tool)",
                        installed_version=inst,
                        available_version=curr,
                        source="brew-formula"
                    ))
                # Parse casks (GUI apps)
                for c in data.get("casks", []):
                    name = c.get("name", "")
                    inst = c.get("installed_versions", ["Unknown"])[0] if c.get("installed_versions") else "Unknown"
                    curr = c.get("current_version", "")
                    packages.append(PackageInfo(
                        id=name,
                        name=f"{name} (App)",
                        installed_version=inst,
                        available_version=curr,
                        source="brew-cask"
                    ))
            return packages
        except Exception as e:
            if on_log:
                on_log(f"Error checking Homebrew updates: {e}")
            return []

    def list_installed(self, filter_query: Optional[str] = None, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        packages = []
        if self.is_available():
            try:
                # Get casks
                res_casks = subprocess.run([self._brew_path, "list", "--cask", "--versions"], capture_output=True, text=True, timeout=30)
                if res_casks.returncode == 0:
                    for line in res_casks.stdout.splitlines():
                        parts = line.strip().split()
                        if parts:
                            pkg_id = parts[0]
                            ver = parts[1] if len(parts) > 1 else "Unknown"
                            packages.append(PackageInfo(
                                id=pkg_id,
                                name=pkg_id,
                                installed_version=ver,
                                available_version="",
                                source="brew-cask"
                            ))
                # Get formulae
                res_forms = subprocess.run([self._brew_path, "list", "--formula", "--versions"], capture_output=True, text=True, timeout=30)
                if res_forms.returncode == 0:
                    for line in res_forms.stdout.splitlines():
                        parts = line.strip().split()
                        if parts:
                            pkg_id = parts[0]
                            ver = parts[1] if len(parts) > 1 else "Unknown"
                            packages.append(PackageInfo(
                                id=pkg_id,
                                name=pkg_id,
                                installed_version=ver,
                                available_version="",
                                source="brew-formula"
                            ))
            except Exception as e:
                if on_log:
                    on_log(f"Error enumerating Homebrew packages: {e}")

        # Also merge native /Applications apps
        app_engine = MacApplicationsEngine()
        native_apps = app_engine.list_installed(filter_query, on_log)
        seen_names = {p.name.lower() for p in packages}
        for na in native_apps:
            if na.name.lower() not in seen_names:
                packages.append(na)

        if filter_query:
            q = filter_query.lower()
            packages = [p for p in packages if q in p.name.lower() or q in p.id.lower()]

        return packages

    def search_packages(self, query: str, on_log: Optional[Callable[[str], None]] = None) -> List[SearchResult]:
        if not self.is_available():
            return []
        try:
            res = subprocess.run([self._brew_path, "search", query], capture_output=True, text=True, timeout=20)
            results = []
            if res.returncode == 0:
                is_cask_section = False
                for line in res.stdout.splitlines():
                    trimmed = line.strip()
                    if not trimmed:
                        continue
                    if "==> Casks" in trimmed:
                        is_cask_section = True
                        continue
                    elif "==> Formulae" in trimmed:
                        is_cask_section = False
                        continue
                    for token in trimmed.split():
                        results.append(SearchResult(
                            id=token,
                            name=token,
                            version="Latest",
                            source="brew-cask" if is_cask_section else "brew-formula",
                            description="Official Homebrew Catalog"
                        ))
            return results[:30]
        except Exception:
            return []

    def upgrade_package(
        self,
        package_id: str,
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        if not self.is_available():
            return OperationStatus(package_id=package_id, success=False, message="Homebrew not installed.")

        cmd = [self._brew_path, "upgrade", package_id]
        if on_log:
            on_log(f"Executing: {' '.join(cmd)}")
        if on_progress:
            on_progress(0.25, f"Upgrading {package_id} via Homebrew...")

        try:
            self._active_proc = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1
            )
            for line in self._active_proc.stdout:
                if on_log:
                    on_log(line.rstrip())

            self._active_proc.wait()
            success = (self._active_proc.returncode == 0)
            if on_progress:
                on_progress(1.0, f"Finished {package_id}")

            return OperationStatus(
                package_id=package_id,
                success=success,
                message=f"Upgrade {'succeeded' if success else 'failed'}"
            )
        except Exception as e:
            return OperationStatus(package_id=package_id, success=False, message=str(e))
        finally:
            self._active_proc = None

    def upgrade_all(
        self,
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        if not self.is_available():
            return OperationStatus(package_id="all", success=False, message="Homebrew not installed.")
        cmd = [self._brew_path, "upgrade"]
        try:
            self._active_proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            for line in self._active_proc.stdout:
                if on_log:
                    on_log(line.rstrip())
            self._active_proc.wait()
            success = self._active_proc.returncode == 0
            return OperationStatus(package_id="all", success=success, message="All Homebrew packages upgraded.")
        except Exception as e:
            return OperationStatus(package_id="all", success=False, message=str(e))
        finally:
            self._active_proc = None

    def install_package(
        self,
        package_id: str,
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        if not self.is_available():
            return OperationStatus(package_id=package_id, success=False, message="Homebrew not installed.")
        cmd = [self._brew_path, "install", package_id]
        try:
            self._active_proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            for line in self._active_proc.stdout:
                if on_log:
                    on_log(line.rstrip())
            self._active_proc.wait()
            success = self._active_proc.returncode == 0
            return OperationStatus(package_id=package_id, success=success, message=f"Installed {package_id}")
        except Exception as e:
            return OperationStatus(package_id=package_id, success=False, message=str(e))
        finally:
            self._active_proc = None

    def uninstall_package(self, package_id: str, on_log: Optional[Callable[[str], None]] = None) -> OperationStatus:
        if not self.is_available():
            return OperationStatus(package_id=package_id, success=False, message="Homebrew not installed.")
        cmd = [self._brew_path, "uninstall", package_id]
        try:
            res = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
            return OperationStatus(package_id=package_id, success=res.returncode == 0, message=res.stdout)
        except Exception as e:
            return OperationStatus(package_id=package_id, success=False, message=str(e))

    def abort(self):
        if self._active_proc and self._active_proc.poll() is None:
            try:
                self._active_proc.terminate()
            except Exception:
                pass


class AppleUpdateEngine(BaseEngine):
    """Official Apple System & macOS Software Update Engine (/usr/sbin/softwareupdate)."""

    def __init__(self):
        self._bin = "/usr/sbin/softwareupdate"
        self._active_proc: Optional[subprocess.Popen] = None

    @property
    def name(self) -> str:
        return "macOS System Software Update"

    @property
    def identifier(self) -> str:
        return "apple"

    def is_available(self) -> bool:
        return os.path.exists(self._bin) and os.access(self._bin, os.X_OK)

    def get_version(self) -> Optional[str]:
        try:
            res = subprocess.run(["sw_vers", "-productVersion"], capture_output=True, text=True)
            return res.stdout.strip()
        except Exception:
            return "macOS"

    def check_updates(self, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        if not self.is_available():
            return []
        if on_log:
            on_log("Connecting to Apple Software Update servers...")

        packages = []
        try:
            res = subprocess.run([self._bin, "-l"], capture_output=True, text=True, timeout=90)
            stdout = res.stdout + res.stderr
            current_pkg = {}
            for line in stdout.splitlines():
                trimmed = line.strip()
                if trimmed.startswith("* Label:"):
                    if current_pkg.get("id"):
                        packages.append(PackageInfo(
                            id=current_pkg["id"],
                            name=current_pkg.get("title", current_pkg["id"]),
                            installed_version="Current OS",
                            available_version=current_pkg.get("version", "Latest"),
                            source="apple-system"
                        ))
                    label = trimmed.replace("* Label:", "").strip()
                    current_pkg = {"id": label, "title": label}
                elif "Title:" in trimmed and "Version:" in trimmed:
                    # e.g.: Title: macOS Tahoe 26.7.1, Version: 26.7.1, Size: 880998KiB
                    parts = trimmed.split(",")
                    for part in parts:
                        sub = part.strip()
                        if sub.startswith("Title:"):
                            current_pkg["title"] = sub.replace("Title:", "").strip()
                        elif sub.startswith("Version:"):
                            current_pkg["version"] = sub.replace("Version:", "").strip()

            if current_pkg.get("id"):
                packages.append(PackageInfo(
                    id=current_pkg["id"],
                    name=current_pkg.get("title", current_pkg["id"]),
                    installed_version="Current OS",
                    available_version=current_pkg.get("version", "Latest"),
                    source="apple-system"
                ))
        except Exception as e:
            if on_log:
                on_log(f"Error querying Apple updates: {e}")

        return packages

    def list_installed(self, filter_query: Optional[str] = None, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        # Return base macOS build
        try:
            build = subprocess.run(["sw_vers", "-buildVersion"], capture_output=True, text=True).stdout.strip()
            ver = subprocess.run(["sw_vers", "-productVersion"], capture_output=True, text=True).stdout.strip()
            name = subprocess.run(["sw_vers", "-productName"], capture_output=True, text=True).stdout.strip()
            return [PackageInfo(
                id="com.apple.macos",
                name=f"{name} ({ver} Build {build})",
                installed_version=ver,
                available_version="",
                source="apple-system"
            )]
        except Exception:
            return []

    def search_packages(self, query: str, on_log: Optional[Callable[[str], None]] = None) -> List[SearchResult]:
        return []

    def upgrade_package(
        self,
        package_id: str,
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        if on_log:
            on_log(f"Triggering official Apple software download for {package_id}...")
        cmd = [self._bin, "-i", package_id]
        try:
            self._active_proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            for line in self._active_proc.stdout:
                if on_log:
                    on_log(line.rstrip())
            self._active_proc.wait()
            success = self._active_proc.returncode == 0
            return OperationStatus(package_id=package_id, success=success, message="Apple update finished")
        except Exception as e:
            return OperationStatus(package_id=package_id, success=False, message=str(e))
        finally:
            self._active_proc = None

    def upgrade_all(
        self,
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        cmd = [self._bin, "-ia", "--agree-to-license"]
        try:
            self._active_proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            for line in self._active_proc.stdout:
                if on_log:
                    on_log(line.rstrip())
            self._active_proc.wait()
            return OperationStatus(package_id="all", success=self._active_proc.returncode == 0, message="All Apple updates processed.")
        except Exception as e:
            return OperationStatus(package_id="all", success=False, message=str(e))
        finally:
            self._active_proc = None

    def install_package(self, package_id: str, *args, **kwargs) -> OperationStatus:
        return self.upgrade_package(package_id, *args, **kwargs)

    def uninstall_package(self, package_id: str, on_log: Optional[Callable[[str], None]] = None) -> OperationStatus:
        return OperationStatus(package_id=package_id, success=False, message="System software updates cannot be uninstalled directly.")

    def abort(self):
        if self._active_proc and self._active_proc.poll() is None:
            try:
                self._active_proc.terminate()
            except Exception:
                pass


class MacApplicationsEngine(BaseEngine):
    """Engine for auditing, searching, and managing desktop apps in /Applications."""

    @property
    def name(self) -> str:
        return "macOS Applications"

    @property
    def identifier(self) -> str:
        return "mac-apps"

    def is_available(self) -> bool:
        return os.path.exists("/Applications")

    def get_version(self) -> Optional[str]:
        return "macOS"

    def check_updates(self, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        # Handled in coordination with Homebrew and Apple engines
        return []

    def list_installed(self, filter_query: Optional[str] = None, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        packages = []
        target_dirs = ["/Applications", os.path.expanduser("~/Applications")]
        for app_dir in target_dirs:
            if not os.path.exists(app_dir):
                continue
            try:
                for item in os.listdir(app_dir):
                    if item.endswith(".app"):
                        full_path = os.path.join(app_dir, item)
                        plist_path = os.path.join(full_path, "Contents", "Info.plist")
                        version = "Unknown"
                        bundle_id = ""
                        display_name = item[:-4]
                        if os.path.exists(plist_path):
                            try:
                                with open(plist_path, "rb") as f:
                                    pl = plistlib.load(f)
                                    version = pl.get("CFBundleShortVersionString") or pl.get("CFBundleVersion") or "1.0"
                                    bundle_id = pl.get("CFBundleIdentifier", "")
                                    display_name = pl.get("CFBundleDisplayName") or pl.get("CFBundleName") or display_name
                            except Exception:
                                pass
                        pkg_id = bundle_id if bundle_id else item[:-4].lower().replace(" ", "-")
                        packages.append(PackageInfo(
                            id=pkg_id,
                            name=display_name,
                            installed_version=str(version),
                            available_version="",
                            source="mac-app"
                        ))
            except Exception as e:
                if on_log:
                    on_log(f"Error auditing {app_dir}: {e}")

        packages.sort(key=lambda p: p.name.lower())
        if filter_query:
            q = filter_query.lower()
            packages = [p for p in packages if q in p.name.lower() or q in p.id.lower()]
        return packages

    def search_packages(self, query: str, on_log: Optional[Callable[[str], None]] = None) -> List[SearchResult]:
        return []

    def upgrade_package(self, package_id: str, *args, **kwargs) -> OperationStatus:
        return OperationStatus(package_id=package_id, success=False, message="Direct app upgrade not supported directly without Homebrew.")

    def upgrade_all(self, *args, **kwargs) -> OperationStatus:
        return OperationStatus(package_id="all", success=False, message="Not supported")

    def install_package(self, package_id: str, *args, **kwargs) -> OperationStatus:
        return OperationStatus(package_id=package_id, success=False, message="Not supported")

    def uninstall_package(self, package_id: str, on_log: Optional[Callable[[str], None]] = None) -> OperationStatus:
        # Move corresponding app to Trash
        target_dirs = ["/Applications", os.path.expanduser("~/Applications")]
        for d in target_dirs:
            if not os.path.exists(d):
                continue
            for item in os.listdir(d):
                if item.endswith(".app"):
                    full_p = os.path.join(d, item)
                    # Check matching bundle id or name
                    plist_p = os.path.join(full_p, "Contents", "Info.plist")
                    match = False
                    if os.path.exists(plist_p):
                        try:
                            with open(plist_p, "rb") as f:
                                pl = plistlib.load(f)
                                if pl.get("CFBundleIdentifier") == package_id:
                                    match = True
                        except Exception:
                            pass
                    if not match and item[:-4].lower().replace(" ", "-") == package_id:
                        match = True

                    if match:
                        try:
                            trash_dest = os.path.join(os.path.expanduser("~/.Trash"), item)
                            shutil.move(full_p, trash_dest)
                            if on_log:
                                on_log(f"Moved {item} to Trash successfully.")
                            return OperationStatus(package_id=package_id, success=True, message=f"Moved {item} to Trash.")
                        except Exception as e:
                            return OperationStatus(package_id=package_id, success=False, message=str(e))

        return OperationStatus(package_id=package_id, success=False, message=f"Application {package_id} not found.")

    def abort(self):
        pass
