import unittest
from unittest import mock

from core.system_tools import (
    format_volume,
    network_tools as nt,
    reg_preview,
    services_admin,
    shell_context,
    stay_awake,
)


class Wave45RemainingTests(unittest.TestCase):
    def test_tcpip_needs_confirm(self):
        self.assertTrue(nt.reset_tcpip().get("needs_confirm"))

    def test_firewall_reset_needs_confirm(self):
        self.assertTrue(nt.firewall_restore_defaults().get("needs_confirm"))

    def test_format_rejects_system_drive(self):
        with mock.patch.dict("os.environ", {"SystemDrive": "C:"}):
            result = format_volume.format_removable_volume("C", confirmed=True)
        self.assertFalse(result["ok"])

    def test_service_denylist_blocks(self):
        result = services_admin.set_service_state("wuauserv", "stop", confirmed=True)
        self.assertFalse(result["ok"])

    def test_reg_import_blocked_by_default(self):
        result = reg_preview.import_reg_file(r"C:\Users\x\test.reg", confirmed=True, allow_import=False)
        self.assertTrue(result.get("import_blocked"))

    def test_context_disable_needs_confirm(self):
        self.assertTrue(shell_context.disable_context_handler("bad").get("needs_confirm"))

    def test_keep_awake_status(self):
        stay_awake.keep_awake_cancel()
        result = stay_awake.keep_awake_status()
        self.assertTrue(result.get("ok"))
        self.assertFalse(result.get("active"))


if __name__ == "__main__":
    unittest.main()