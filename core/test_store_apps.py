import os
import tempfile
import unittest
from unittest.mock import patch
import subprocess

from core.system_tools import store_apps


class StoreAppsTests(unittest.TestCase):
    def test_reregister_requires_confirm(self):
        result = store_apps.reregister_store_package("Contoso.App_1.0.0.0_neutral__8wekyb3d8bbwe")
        self.assertFalse(result["ok"])
        self.assertTrue(result.get("needs_confirm"))

    def test_reregister_rejects_bad_id(self):
        result = store_apps.reregister_store_package("bad; id", confirmed=True)
        self.assertFalse(result["ok"])
        self.assertIn("not allowed", result["error"].lower())

    def test_list_parses_json(self):
        payload = (
            '[{"Name":"Calc","PackageFullName":"Microsoft.WindowsCalculator_8wekyb3d8bbwe",'
            '"InstallLocation":"C:\\\\Apps\\\\Calc","Publisher":"CN=Microsoft"}]'
        )
        completed = subprocess.CompletedProcess(
            args=[], returncode=0, stdout=payload, stderr=""
        )
        with patch.object(store_apps, "_ps_run", return_value=completed):
            result = store_apps.list_store_packages()
        self.assertTrue(result["ok"])
        self.assertEqual(result["count"], 1)
        self.assertEqual(
            result["packages"][0]["package_full_name"],
            "Microsoft.WindowsCalculator_8wekyb3d8bbwe",
        )

    def test_reregister_uses_listed_manifest_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest = os.path.join(tmp, "AppxManifest.xml")
            with open(manifest, "w", encoding="utf-8") as handle:
                handle.write("<Package/>")
            full = "Contoso.Demo_1.0.0.0_neutral__8wekyb3d8bbwe"
            listed = {
                "ok": True,
                "packages": [
                    {
                        "name": "Demo",
                        "package_full_name": full,
                        "install_location": tmp,
                        "publisher": "CN=Contoso",
                    }
                ],
            }
            completed = subprocess.CompletedProcess(args=[], returncode=0, stdout="", stderr="")
            with patch.object(store_apps, "list_store_packages", return_value=listed), patch.object(
                store_apps, "_ps_run", return_value=completed
            ) as run:
                result = store_apps.reregister_store_package(full, confirmed=True)
            self.assertTrue(result["ok"])
            cmd = run.call_args[0][0]
            self.assertIn("Add-AppxPackage", cmd)
            self.assertIn("AppxManifest.xml", cmd)


if __name__ == "__main__":
    unittest.main()
