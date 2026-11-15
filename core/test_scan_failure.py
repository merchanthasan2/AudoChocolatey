import unittest

from web_api import WebAPI, _normalize_serial
from core.manager import PackageManager


class ScanFailureMessageTests(unittest.TestCase):
    def setUp(self):
        self.api = WebAPI(PackageManager())

    def test_timeout_message_is_user_facing(self):
        msg = self.api._scan_failure_message("Command timed out after 90 seconds")
        self.assertIn("did not answer in time", msg)
        self.assertNotIn("winget", msg.lower())

    def test_generic_failure_message(self):
        msg = self.api._scan_failure_message("access denied")
        self.assertIn("update scan failed", msg.lower())
        self.assertNotIn("traceback", msg.lower())

    def test_oem_serial_treated_as_empty(self):
        self.assertEqual(_normalize_serial("To be filled by O.E.M."), "")


if __name__ == "__main__":
    unittest.main()
