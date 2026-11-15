import unittest
from unittest.mock import patch
import subprocess

from core.system_tools import windows_health as wh


def _ok_proc(stdout="OK"):
    return subprocess.CompletedProcess(args=[], returncode=0, stdout=stdout, stderr="")


class WindowsHealthTests(unittest.TestCase):
    def test_uninstall_requires_confirm(self):
        result = wh.uninstall_windows_update("KB1234567")
        self.assertFalse(result["ok"])
        self.assertTrue(result.get("needs_confirm"))

    def test_uninstall_rejects_bad_id(self):
        result = wh.uninstall_windows_update("not-a-kb", confirmed=True)
        self.assertFalse(result["ok"])

    def test_bitlocker_strips_to_status_only(self):
        payload = (
            '[{"MountPoint":"C:","VolumeStatus":"FullyEncrypted",'
            '"ProtectionStatus":"On","EncryptionPercentage":100,'
            '"VolumeType":"OperatingSystem","RecoveryPassword":"SHOULD-NOT-APPEAR"}]'
        )
        with patch.object(wh, "_ps", return_value=_ok_proc(payload)):
            result = wh.bitlocker_status()
        self.assertTrue(result["ok"])
        self.assertEqual(result["volumes"][0]["mount"], "C:")
        blob = str(result)
        self.assertNotIn("SHOULD-NOT-APPEAR", blob)
        self.assertNotIn("RecoveryPassword", blob)

    def test_volume_letter_guard(self):
        result = wh.volume_scan("C:\\Windows", confirmed=True, repair=True)
        self.assertFalse(result["ok"])

    def test_dism_scan_only(self):
        with patch.object(wh, "_run", return_value=_ok_proc("done")) as run:
            result = wh.dism_restore_health(scan_only=True)
        self.assertTrue(result["ok"])
        self.assertIn("ScanHealth", " ".join(run.call_args[0][0]))

    def test_sfc_repair_needs_confirm(self):
        result = wh.sfc_scan(repair=True, confirmed=False)
        self.assertTrue(result.get("needs_confirm"))


if __name__ == "__main__":
    unittest.main()
