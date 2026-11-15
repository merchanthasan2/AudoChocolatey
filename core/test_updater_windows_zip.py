"""Windows release zip is preferred over a bare onedir bootloader exe."""

from __future__ import annotations

import os
import tempfile
import unittest
from unittest.mock import patch

from core.updater import AppUpdater, find_windows_onedir_root


class TestUpdaterWindowsZip(unittest.TestCase):
    def test_select_prefers_windows_zip_over_bare_exe(self):
        updater = AppUpdater(current_version="2.0.0")
        updater.is_windows = True
        updater.is_mac = False
        assets = [
            {"name": "QuantumUpdater.exe", "browser_download_url": "https://x/e"},
            {
                "name": "QuantumUpdater-Windows.zip",
                "browser_download_url": "https://x/z",
            },
        ]
        chosen = updater._select_best_asset(assets)
        self.assertEqual(chosen["name"], "QuantumUpdater-Windows.zip")

    def test_select_prefers_setup_exe_first(self):
        updater = AppUpdater(current_version="2.0.0")
        updater.is_windows = True
        updater.is_mac = False
        assets = [
            {"name": "QuantumUpdater-Windows.zip", "browser_download_url": "https://x/z"},
            {"name": "Quantum.Updater_2.0.2_x64-setup.exe", "browser_download_url": "https://x/s"},
        ]
        chosen = updater._select_best_asset(assets)
        self.assertIn("setup", chosen["name"].lower())

    def test_find_onedir_root_flat_and_nested(self):
        with tempfile.TemporaryDirectory() as tmp:
            flat = os.path.join(tmp, "flat")
            os.makedirs(flat)
            open(os.path.join(flat, "QuantumUpdater.exe"), "wb").close()
            os.makedirs(os.path.join(flat, "_internal"))
            self.assertEqual(find_windows_onedir_root(flat), flat)

            nested = os.path.join(tmp, "nested", "QuantumUpdater")
            os.makedirs(nested)
            open(os.path.join(nested, "QuantumUpdater.exe"), "wb").close()
            self.assertEqual(
                find_windows_onedir_root(os.path.join(tmp, "nested")),
                nested,
            )

    def test_zip_apply_refuses_unfrozen(self):
        updater = AppUpdater(current_version="2.0.0")
        with patch("core.updater.sys") as fake_sys:
            fake_sys.frozen = False
            fake_sys.executable = r"C:\Python\python.exe"
            ok = updater._apply_windows_zip(r"C:\tmp\QuantumUpdater-Windows.zip", on_log=None)
        self.assertFalse(ok)


if __name__ == "__main__":
    unittest.main()
