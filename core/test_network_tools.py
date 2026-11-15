import unittest

from core.system_tools import network_tools as nt


class NetworkToolsTests(unittest.TestCase):
    def test_flush_dns_needs_confirm(self):
        self.assertTrue(nt.flush_dns().get("needs_confirm"))

    def test_winsock_needs_confirm(self):
        self.assertTrue(nt.reset_winsock().get("needs_confirm"))

    def test_tcp_port_guard(self):
        self.assertFalse(nt.test_tcp_port("bad host!", 80)["ok"])
        self.assertFalse(nt.test_tcp_port("example.com", 99999)["ok"])

    def test_dns_lookup_localhost(self):
        result = nt.dns_lookup("localhost")
        self.assertTrue(result["ok"])
        self.assertTrue(result["addresses"])

    def test_set_power_plan_bad_guid(self):
        result = nt.set_power_plan("not-a-guid", confirmed=True)
        self.assertFalse(result["ok"])

    def test_hosts_restore_needs_confirm(self):
        self.assertTrue(nt.restore_default_hosts().get("needs_confirm"))

    def test_tcp_port_localhost(self):
        # Port 1 is usually closed; still a valid probe.
        result = nt.test_tcp_port("127.0.0.1", 1)
        self.assertTrue(result["ok"])
        self.assertIn("open", result)


if __name__ == "__main__":
    unittest.main()
