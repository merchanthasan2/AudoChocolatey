import os
import tempfile
import unittest
from unittest.mock import patch
import subprocess

from core.system_tools import catalog_export


class CatalogExportTests(unittest.TestCase):
    def test_export_rejects_path_outside_profile(self):
        result = catalog_export.export_catalog(r"C:\Windows\Temp\evil.json")
        self.assertFalse(result["ok"])
        self.assertIn("profile", result["error"].lower())

    def test_export_rejects_non_json(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "list.txt")
            result = catalog_export.export_catalog(path)
        self.assertFalse(result["ok"])
        self.assertIn(".json", result["error"].lower())

    def test_import_requires_confirm(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "apps.json")
            with open(path, "w", encoding="utf-8") as handle:
                handle.write("{}")
            result = catalog_export.import_catalog(path)
        self.assertFalse(result["ok"])
        self.assertTrue(result.get("needs_confirm"))

    def test_export_ok_mocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = os.path.join(tmp, "quantum-updater-apps.json")
            completed = subprocess.CompletedProcess(args=[], returncode=0, stdout="Exported", stderr="")

            def fake_run(args, **kwargs):
                idx = args.index("-o") + 1 if "-o" in args else -1
                if idx > 0:
                    with open(args[idx], "w", encoding="utf-8") as handle:
                        handle.write('{"Sources":[]} ')
                return completed

            with patch.object(catalog_export, "resolve_winget_executable", return_value=r"C:\winget.exe"), patch.object(
                catalog_export, "_run_winget", side_effect=fake_run
            ) as run:
                result = catalog_export.export_catalog(out)
            self.assertTrue(result["ok"])
            run.assert_called_once()
            cmd = run.call_args[0][0]
            self.assertEqual(cmd[1:4], ["export", "-o", out])

    def test_import_ok_mocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "apps.json")
            with open(path, "w", encoding="utf-8") as handle:
                handle.write('{"Sources":[]} ')
            completed = subprocess.CompletedProcess(args=[], returncode=0, stdout="Imported", stderr="")
            with patch.object(catalog_export, "resolve_winget_executable", return_value=r"C:\winget.exe"), patch.object(
                catalog_export, "_run_winget", return_value=completed
            ) as run:
                result = catalog_export.import_catalog(path, confirmed=True)
            self.assertTrue(result["ok"])
            args = run.call_args[0][0]
            self.assertEqual(args[1:3], ["import", "-i"])

    def test_export_when_winget_missing(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "apps.json")
            with patch.object(catalog_export, "resolve_winget_executable", return_value=None):
                result = catalog_export.export_catalog(path)
        self.assertFalse(result["ok"])
        self.assertIn("not available", result["error"].lower())


if __name__ == "__main__":
    unittest.main()