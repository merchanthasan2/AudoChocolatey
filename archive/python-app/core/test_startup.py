import os
import struct
import tempfile
import unittest

import winreg

from core.system_tools import startup as startup_mod


@unittest.skipIf(os.name != "nt", "Windows only")
class StartupToolTests(unittest.TestCase):
    TEST_ROOT = r"Software\QuantumUpdaterTest"
    TEST_RUN = TEST_ROOT + r"\Run"

    def setUp(self):
        self._tmpdir = tempfile.TemporaryDirectory()
        journal = os.path.join(self._tmpdir.name, "startup-journal.json")
        startup_mod.configure_test_hooks(
            hkcu_run_subkey=self.TEST_RUN,
            journal_path=journal,
            skip_tasks=True,
            skip_folders=True,
        )
        try:
            winreg.CreateKey(winreg.HKEY_CURRENT_USER, self.TEST_ROOT)
        except OSError:
            pass
        try:
            winreg.CreateKey(winreg.HKEY_CURRENT_USER, self.TEST_RUN)
        except OSError:
            pass
        approved = r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
        try:
            with winreg.OpenKey(
                winreg.HKEY_CURRENT_USER,
                approved,
                0,
                winreg.KEY_SET_VALUE,
            ) as key:
                winreg.DeleteValue(key, "QuantumUpdaterFixture")
        except OSError:
            pass

    def tearDown(self):
        startup_mod.reset_test_hooks()
        for sub in (self.TEST_RUN, self.TEST_ROOT):
            try:
                winreg.DeleteKey(winreg.HKEY_CURRENT_USER, sub)
            except OSError:
                pass
        self._tmpdir.cleanup()

    def _set_run(self, name: str, command: str) -> None:
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, self.TEST_RUN) as key:
            winreg.SetValueEx(key, name, 0, winreg.REG_SZ, command)

    def _delete_run(self, name: str) -> None:
        try:
            with winreg.OpenKey(
                winreg.HKEY_CURRENT_USER, self.TEST_RUN, 0, winreg.KEY_SET_VALUE
            ) as key:
                winreg.DeleteValue(key, name)
        except OSError:
            pass

    def test_list_disable_enable_undo_and_journal(self):
        notepad = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "notepad.exe")
        self._set_run("QuantumUpdaterFixture", notepad)
        listed = startup_mod.startup_list()
        match = [e for e in listed if e["name"] == "QuantumUpdaterFixture"]
        self.assertEqual(len(match), 1)
        entry_id = match[0]["id"]
        self.assertTrue(match[0]["enabled"])

        off = startup_mod.startup_set_enabled(entry_id, False)
        self.assertTrue(off["success"])
        self.assertFalse(off["entry"]["enabled"])

        raw = startup_mod._reg_read_binary(
            winreg.HKEY_CURRENT_USER,
            startup_mod._APPROVED_RUN,
            "QuantumUpdaterFixture",
        )
        self.assertIsNotNone(raw)
        self.assertEqual(raw[0], startup_mod._DISABLED_BYTE)

        on = startup_mod.startup_set_enabled(entry_id, True)
        self.assertTrue(on["success"])
        self.assertTrue(on["entry"]["enabled"])

        history = startup_mod.startup_history()
        self.assertGreaterEqual(len(history), 2)

        startup_mod.startup_set_enabled(entry_id, False)
        undo = startup_mod.startup_undo_last()
        self.assertTrue(undo.get("success"))

    def test_startup_approved_encoding(self):
        disabled = startup_mod.encode_startup_approved(False, b"\x02" + b"\x00" * 11)
        self.assertEqual(disabled[0], 0x03)
        self.assertTrue(any(disabled[4:12]))
        enabled = startup_mod.encode_startup_approved(True, disabled)
        self.assertIn(enabled[0], startup_mod._ENABLED_BYTES)

    def test_refuse_system32_target(self):
        system32 = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32", "cmd.exe")
        refused, reason = startup_mod._is_refused("Test", system32, system32)
        self.assertTrue(refused)
        self.assertIn("system folder", reason.lower())

        entry = startup_mod._build_registry_entry("Sec", system32, startup_mod.SOURCE_HKCU_RUN, True, False, False)
        self.assertTrue(entry["refused"])


if __name__ == "__main__":
    unittest.main()
