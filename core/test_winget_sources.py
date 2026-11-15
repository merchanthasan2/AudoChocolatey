import unittest
from unittest.mock import patch
import subprocess

from core.winget_engine import WingetEngine


class WingetSourcesTests(unittest.TestCase):
    def setUp(self):
        self.engine = WingetEngine.__new__(WingetEngine)
        self.engine._executable = r"C:\fake\winget.exe"
        self.engine.last_error = ""

    def test_refresh_sources_ok(self):
        completed = subprocess.CompletedProcess(
            args=[], returncode=0, stdout="Done\n", stderr=""
        )
        with patch.object(self.engine, "is_available", return_value=True), patch.object(
            self.engine, "_run_capture", return_value=completed
        ) as run:
            result = self.engine.refresh_sources()
        self.assertTrue(result["ok"])
        self.assertIn("refreshed", result["message"].lower())
        run.assert_called_once()
        args = run.call_args[0][0]
        self.assertEqual(args[1:3], ["source", "update"])

    def test_reset_sources_requires_confirm(self):
        result = self.engine.reset_sources(confirmed=False)
        self.assertFalse(result["ok"])
        self.assertTrue(result.get("needs_confirm"))

    def test_reset_sources_ok(self):
        completed = subprocess.CompletedProcess(
            args=[], returncode=0, stdout="Reset\n", stderr=""
        )
        with patch.object(self.engine, "is_available", return_value=True), patch.object(
            self.engine, "_run_capture", return_value=completed
        ) as run:
            result = self.engine.reset_sources(confirmed=True)
        self.assertTrue(result["ok"])
        self.assertTrue(result.get("recovery"))
        args = run.call_args[0][0]
        self.assertEqual(args[1:4], ["source", "reset", "--force"])

    def test_refresh_when_winget_missing(self):
        self.engine._executable = None
        result = self.engine.refresh_sources()
        self.assertFalse(result["ok"])
        self.assertIn("not available", result["error"].lower())


if __name__ == "__main__":
    unittest.main()
