import unittest
from unittest.mock import patch
import subprocess

from core.system_tools import app_repair


class AppRepairTests(unittest.TestCase):
    def test_repair_desktop_requires_confirm(self):
        result = app_repair.repair_desktop("Contoso.App")
        self.assertFalse(result["ok"])
        self.assertTrue(result.get("needs_confirm"))

    def test_repair_desktop_rejects_bad_id(self):
        result = app_repair.repair_desktop("bad;id", confirmed=True)
        self.assertFalse(result["ok"])
        self.assertIn("not allowed", result["error"].lower())

    def test_repair_desktop_ok_mocked(self):
        completed = subprocess.CompletedProcess(args=[], returncode=0, stdout="Repaired", stderr="")
        with patch.object(app_repair, "resolve_winget_executable", return_value=r"C:\winget.exe"), patch.object(
            app_repair, "_run_winget", return_value=completed
        ) as run:
            result = app_repair.repair_desktop("Microsoft.PowerToys", confirmed=True)
        self.assertTrue(result["ok"])
        args = run.call_args[0][0]
        self.assertIn("repair", args)
        self.assertIn("Microsoft.PowerToys", args)

    def test_reset_requires_confirm(self):
        result = app_repair.reset_packaged_app("Contoso.App_1.0.0.0_neutral__8wekyb3d8bbwe")
        self.assertFalse(result["ok"])
        self.assertTrue(result.get("needs_confirm"))

    def test_reset_rejects_unlisted_package(self):
        listed = {"ok": True, "packages": []}
        with patch.object(app_repair, "list_packaged_apps", return_value=listed):
            result = app_repair.reset_packaged_app(
                "Contoso.App_1.0.0.0_neutral__8wekyb3d8bbwe", confirmed=True
            )
        self.assertFalse(result["ok"])
        self.assertIn("not installed", result["error"].lower())

    def test_reset_listed_ok_mocked(self):
        full = "Contoso.App_1.0.0.0_neutral__8wekyb3d8bbwe"
        listed = {
            "ok": True,
            "packages": [{"name": "App", "package_full_name": full, "install_location": "C:\\Apps"}],
        }
        completed = subprocess.CompletedProcess(args=[], returncode=0, stdout="", stderr="")
        with patch.object(app_repair, "list_packaged_apps", return_value=listed), patch.object(
            app_repair.store_apps, "_ps_run", return_value=completed
        ) as run:
            result = app_repair.reset_packaged_app(full, confirmed=True)
        self.assertTrue(result["ok"])
        self.assertIn("data", result["message"].lower())
        self.assertIn("Reset-AppxPackage", run.call_args[0][0])


if __name__ == "__main__":
    unittest.main()