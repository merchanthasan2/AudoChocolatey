import unittest
from unittest.mock import patch

from core.arp_inventory import ArpEntry, plan_uninstall
from core.updater import is_official_asset


def _entry(command: str) -> ArpEntry:
    return ArpEntry(
        id="arp:machine:test",
        name="Example",
        version="1",
        publisher="",
        uninstall_string=command,
        quiet_uninstall_string="",
        install_location="",
        scope="machine",
        registry_path="",
    )


class ArpPlanTests(unittest.TestCase):
    def test_msiexec_becomes_a_fixed_argument_list(self):
        plan = plan_uninstall(_entry(r"MsiExec.exe /X{11111111-2222-3333-4444-555555555555}"))
        self.assertIsNotNone(plan)
        self.assertTrue(plan["exe"].lower().endswith("msiexec.exe"))
        self.assertEqual(plan["args"], ["/X{11111111-2222-3333-4444-555555555555}"])

    def test_command_shell_is_refused(self):
        self.assertIsNone(plan_uninstall(_entry(r"cmd.exe /c echo no")))
        self.assertIsNone(plan_uninstall(_entry(r"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -c exit")))

    @patch("core.arp_inventory.os.path.isfile", return_value=True)
    @patch("core.arp_inventory.os.path.realpath", side_effect=lambda path: path)
    def test_url_argument_is_refused(self, _real, _file):
        self.assertIsNone(plan_uninstall(_entry(r'"C:\Program Files\App\uninstall.exe" https://evil.example/run')))


class UpdateUrlTests(unittest.TestCase):
    def test_only_official_release_assets_are_accepted(self):
        good = "https://github.com/merchanthasan2/AudoChocolatey/releases/download/v2.0.0/setup.exe"
        portable = "https://github.com/merchanthasan2/AudoChocolatey/releases/download/v2.0.0/QuantumUpdater.exe"
        self.assertTrue(is_official_asset(good))
        self.assertTrue(is_official_asset(portable))
        self.assertFalse(is_official_asset("https://evil.example/setup.exe"))
        self.assertFalse(is_official_asset("http://github.com/merchanthasan2/AudoChocolatey/releases/download/v2.0.0/setup.exe"))
        self.assertFalse(is_official_asset("https://github.com/quantumstep/quantum-updater/releases/download/v2.0.0/setup.exe"))


class ArpPathTests(unittest.TestCase):
    def test_env_var_and_parent_path_are_refused(self):
        self.assertIsNone(plan_uninstall(_entry(r'"%LOCALAPPDATA%\Programs\App\uninstall.exe"')))
        self.assertIsNone(plan_uninstall(_entry(r'"C:\Program Files\App\..\uninstall.exe"')))


if __name__ == "__main__":
    unittest.main()
