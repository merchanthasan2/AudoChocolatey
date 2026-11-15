import unittest
from unittest.mock import patch
from core.arp_inventory import ArpEntry
from core.system_tools import orphan_install


class OrphanInstallTests(unittest.TestCase):
    def test_list_broken_flags_missing_uninstall(self):
        entry = ArpEntry(
            id="arp:machine:BadApp",
            name="Bad App",
            version="1.0",
            publisher="Acme",
            uninstall_string="",
            quiet_uninstall_string="",
            install_location=r"C:\Missing",
            scope="machine",
            registry_path=r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BadApp",
        )
        with patch.object(orphan_install, "list_arp_entries", return_value=[entry]):
            result = orphan_install.list_broken_uninstallers()
        self.assertEqual(result["count"], 1)
        self.assertEqual(result["items"][0]["entry_id"], entry.id)

    def test_remove_requires_confirm(self):
        result = orphan_install.remove_orphan_entry("arp:machine:BadApp")
        self.assertFalse(result["success"])
        self.assertTrue(result.get("needs_confirm"))

    def test_remove_backs_up_and_deletes(self):
        entry = ArpEntry(
            id="arp:machine:BadApp",
            name="Bad App",
            version="1.0",
            publisher="Acme",
            uninstall_string="",
            quiet_uninstall_string="",
            install_location="",
            scope="machine",
            registry_path=r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BadApp",
        )
        with patch.object(orphan_install, "list_arp_entries", return_value=[entry]), patch.object(
            orphan_install, "_resolve_entry", return_value=entry
        ), patch.object(orphan_install, "_export_registry_key", return_value={"DisplayName": {"type": 1, "data": "Bad App"}}), patch.object(
            orphan_install, "_write_registry_backup", return_value="backup123"
        ), patch.object(orphan_install, "_delete_registry_key", return_value=True) as delete_key:
            result = orphan_install.remove_orphan_entry(entry.id, confirmed=True)
        self.assertTrue(result["success"])
        self.assertTrue(result.get("suggest_leftovers_scan"))
        delete_key.assert_called_once()
        self.assertEqual(delete_key.call_args[0][1], entry.registry_path)


if __name__ == "__main__":
    unittest.main()