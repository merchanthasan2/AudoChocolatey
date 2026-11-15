import unittest
from unittest.mock import patch
import subprocess

from core.system_tools import advanced_tools as at


def _ok(stdout="OK"):
    return subprocess.CompletedProcess(args=[], returncode=0, stdout=stdout, stderr="")


class AdvancedToolsTests(unittest.TestCase):
    def test_service_denylist(self):
        result = at.set_service_state("WinDefend", "stop", confirmed=True)
        self.assertFalse(result["ok"])
        self.assertIn("protected", result["error"].lower())

    def test_format_blocks_system_drive(self):
        result = at.format_removable("C:", confirmed=True)
        self.assertFalse(result["ok"])
        self.assertIn("system", result["error"].lower())

    def test_firewall_export_path_guard(self):
        result = at.firewall_export(r"C:\Windows\Temp\fw.wfw")
        self.assertFalse(result["ok"])

    def test_reg_preview_requires_reg(self):
        result = at.preview_reg_file("note.txt")
        self.assertFalse(result["ok"])

    def test_keep_awake_needs_confirm(self):
        self.assertTrue(at.keep_awake().get("needs_confirm"))

    def test_optional_feature_name_guard(self):
        result = at.set_optional_feature("bad name!", True, confirmed=True)
        self.assertFalse(result["ok"])


if __name__ == "__main__":
    unittest.main()
