import os
import sys
import json
import platform
import urllib.request
import urllib.error
import subprocess
import shutil
import tempfile
from typing import Dict, Any, Optional, Callable

CURRENT_VERSION = "2.0.0"
GITHUB_REPO = "merchanthasan2/AudoChocolatey"
API_URL = f"https://api.github.com/repos/{GITHUB_REPO}/releases/latest"

def parse_semver(v: str) -> tuple:
    """Parse version string like 'v2.0.1' or '2.0.1' into a tuple of ints."""
    cleaned = v.strip().lstrip("v").strip()
    parts = []
    for part in cleaned.split("."):
        try:
            parts.append(int(part))
        except ValueError:
            # Handle beta/rc tags like '1-rc'
            sub = ""
            for ch in part:
                if ch.isdigit():
                    sub += ch
                else:
                    break
            parts.append(int(sub) if sub else 0)
    while len(parts) < 3:
        parts.append(0)
    return tuple(parts[:3])

def get_human_size(bytes_num: int) -> str:
    if bytes_num < 1024:
        return f"{bytes_num} B"
    if bytes_num < 1024 * 1024:
        return f"{bytes_num / 1024.0:.1f} KB"
    if bytes_num < 1024 * 1024 * 1024:
        return f"{bytes_num / (1024.0 * 1024.0):.1f} MB"
    return f"{bytes_num / (1024.0 * 1024.0 * 1024.0):.2f} GB"

class AppUpdater:
    """Cross-platform auto-updater engine for macOS and Windows.
    Enables in-place updates without forcing users to re-download setup files manually.
    """

    def __init__(self, current_version: str = CURRENT_VERSION):
        self.current_version = current_version
        self.is_mac = (platform.system() == "Darwin")
        self.is_windows = (platform.system() == "Windows")

    def check_for_updates(self) -> Dict[str, Any]:
        """Queries GitHub Releases API for the latest version and matching asset."""
        req = urllib.request.Request(
            API_URL,
            headers={
                "User-Agent": f"QuantumUpdater/{self.current_version} ({platform.system()}; {platform.machine()})",
                "Accept": "application/vnd.github.v3+json"
            }
        )

        try:
            with urllib.request.urlopen(req, timeout=12) as response:
                if response.status != 200:
                    return {
                        "has_update": False,
                        "current_version": self.current_version,
                        "error": f"HTTP {response.status}"
                    }
                data = json.loads(response.read().decode("utf-8"))
        except urllib.error.URLError as e:
            return {
                "has_update": False,
                "current_version": self.current_version,
                "error": f"Connection failed: {e.reason}"
            }
        except Exception as e:
            return {
                "has_update": False,
                "current_version": self.current_version,
                "error": str(e)
            }

        tag_name = data.get("tag_name", "")
        latest_ver_str = tag_name.lstrip("v").strip()
        has_update = parse_semver(latest_ver_str) > parse_semver(self.current_version)

        assets = data.get("assets", [])
        matched_asset = self._select_best_asset(assets)

        return {
            "has_update": has_update,
            "current_version": self.current_version,
            "latest_version": latest_ver_str,
            "tag_name": tag_name,
            "release_name": data.get("name") or f"Quantum Updater {tag_name}",
            "release_notes": data.get("body", "Performance and stability improvements."),
            "published_at": data.get("published_at", ""),
            "download_url": matched_asset.get("browser_download_url") if matched_asset else "",
            "asset_name": matched_asset.get("name") if matched_asset else "",
            "asset_size": matched_asset.get("size", 0) if matched_asset else 0,
            "asset_size_str": get_human_size(matched_asset.get("size", 0)) if matched_asset else "0 B"
        }

    def _select_best_asset(self, assets: list) -> Optional[dict]:
        """Finds the most suitable release binary for the current operating system."""
        if self.is_mac:
            # 1. Look for update zip package first (fastest in-place extraction)
            for a in assets:
                name = a.get("name", "").lower()
                if "macos" in name and name.endswith(".zip"):
                    return a
            for a in assets:
                name = a.get("name", "").lower()
                if name.endswith(".zip"):
                    return a
            # 2. Look for DMG
            for a in assets:
                name = a.get("name", "").lower()
                if name.endswith(".dmg"):
                    return a

        elif self.is_windows:
            # 1. Look for NSIS setup installer (supports /S silent in-place update)
            for a in assets:
                name = a.get("name", "").lower()
                if "setup" in name and name.endswith(".exe"):
                    return a
            # 2. Look for portable or general exe
            for a in assets:
                name = a.get("name", "").lower()
                if name.endswith(".exe"):
                    return a
            # 3. Look for zip
            for a in assets:
                name = a.get("name", "").lower()
                if name.endswith(".zip"):
                    return a

        return assets[0] if assets else None

    def download_and_apply(
        self,
        download_url: str,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        on_log: Optional[Callable[[str], None]] = None
    ) -> bool:
        """Downloads the update package with progress feedback and executes in-place swap."""
        if not download_url:
            if on_log:
                on_log("[Quantum Auto-Updater]: No valid download URL provided.")
            return False

        if on_log:
            on_log(f"[Quantum Auto-Updater]: Downloading update payload from {download_url}...")

        tmp_dir = tempfile.mkdtemp(prefix="quantum_updater_")
        local_filename = os.path.basename(download_url.split("?")[0])
        tmp_file = os.path.join(tmp_dir, local_filename)

        try:
            req = urllib.request.Request(
                download_url,
                headers={"User-Agent": f"QuantumUpdater/{self.current_version}"}
            )

            with urllib.request.urlopen(req, timeout=60) as resp, open(tmp_file, "wb") as out_f:
                total_bytes = int(resp.headers.get("Content-Length", 0))
                downloaded = 0
                chunk_size = 64 * 1024

                while True:
                    chunk = resp.read(chunk_size)
                    if not chunk:
                        break
                    out_f.write(chunk)
                    downloaded += len(chunk)

                    if total_bytes > 0:
                        pct = downloaded / total_bytes
                        ticker = f"Downloading update: {get_human_size(downloaded)} / {get_human_size(total_bytes)} ({int(pct * 100)}%)"
                    else:
                        pct = None
                        ticker = f"Downloading update: {get_human_size(downloaded)}"

                    if on_progress:
                        on_progress(pct, ticker)

            if on_log:
                on_log("[Quantum Auto-Updater]: Payload verified. Applying in-place update...")

            if self.is_mac:
                return self._apply_macos_update(tmp_file, tmp_dir, on_log)
            elif self.is_windows:
                return self._apply_windows_update(tmp_file, on_log)
            else:
                if on_log:
                    on_log("[Quantum Auto-Updater]: Unsupported OS for automatic restart.")
                return False

        except Exception as e:
            if on_log:
                on_log(f"[Quantum Auto-Updater Error]: Failed to apply update: {str(e)}")
            return False

    def _apply_macos_update(self, downloaded_file: str, tmp_dir: str, on_log: Optional[Callable]) -> bool:
        """Applies macOS in-place update and restarts application bundle."""
        dest_app = "/Applications/Quantum Updater.app"
        # Determine if running from an active app bundle
        running_bundle = None
        main_exec = sys.argv[0]
        if ".app" in main_exec:
            running_bundle = main_exec[:main_exec.find(".app") + 4]
            dest_app = running_bundle

        if on_log:
            on_log(f"[Quantum Auto-Updater]: Staging update for destination: {dest_app}")

        if downloaded_file.endswith(".zip"):
            extract_dir = os.path.join(tmp_dir, "extracted")
            os.makedirs(extract_dir, exist_ok=True)
            subprocess.run(["/usr/bin/ditto", "-xk", downloaded_file, extract_dir], check=True)

            # Locate .app inside extracted folder
            staged_app = None
            for root, dirs, _ in os.walk(extract_dir):
                for d in dirs:
                    if d.endswith(".app"):
                        staged_app = os.path.join(root, d)
                        break
                if staged_app:
                    break

            if not staged_app:
                if on_log:
                    on_log("[Quantum Auto-Updater]: Could not locate .app in extracted update zip.")
                return False

            # Launch detached replacement script
            swap_cmd = (
                f"sleep 1.0; "
                f"rm -rf '{dest_app}'; "
                f"/usr/bin/ditto '{staged_app}' '{dest_app}'; "
                f"/usr/bin/open '{dest_app}'"
            )
            subprocess.Popen(["/bin/sh", "-c", swap_cmd], start_new_session=True)
            if on_log:
                on_log("[Quantum Auto-Updater]: Launching updated app and terminating current instance...")
            os._exit(0)

        elif downloaded_file.endswith(".dmg"):
            # Attach DMG, copy app, detach and restart
            swap_cmd = (
                f"sleep 1.0; "
                f"MOUNT=$(hdiutil attach -nobrowse -readonly '{downloaded_file}' | grep /Volumes | awk '{{print $3}}'); "
                f"rm -rf '{dest_app}'; "
                f"/usr/bin/ditto \"$MOUNT/Quantum Updater.app\" '{dest_app}'; "
                f"hdiutil detach \"$MOUNT\" -force; "
                f"/usr/bin/open '{dest_app}'"
            )
            subprocess.Popen(["/bin/sh", "-c", swap_cmd], start_new_session=True)
            if on_log:
                on_log("[Quantum Auto-Updater]: Launching updated app from DMG mount...")
            os._exit(0)

        return False

    def _apply_windows_update(self, downloaded_file: str, on_log: Optional[Callable]) -> bool:
        """Applies Windows in-place update using silent installer /S switch."""
        if downloaded_file.endswith(".exe"):
            if on_log:
                on_log("[Quantum Auto-Updater]: Executing silent NSIS in-place update (/S)...")
            # Run installer with /S switch for zero-prompt background upgrade
            subprocess.Popen([downloaded_file, "/S"], start_new_session=True)
            os._exit(0)
        return False
