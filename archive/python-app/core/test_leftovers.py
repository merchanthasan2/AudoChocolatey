import json
import os
import tempfile
import time
import unittest
import uuid
from unittest import mock

import winreg

from core.system_tools import leftovers as lo_mod


@unittest.skipIf(os.name != "nt", "Windows only")
class LeftoversTests(unittest.TestCase):
    TEST_UNINSTALL = r"Software\QuantumUpdaterTest\LeftoversUninstall"

    def test_matching_removed_program_folder(self):
        with tempfile.TemporaryDirectory() as tmp:
            hist_dir = os.path.join(tmp, "qu")
            os.makedirs(hist_dir, exist_ok=True)
            prog = "QuantumUpdaterTestProduct"
            pub = "QuantumUpdaterTestPublisher"
            folder = os.path.join(tmp, "AppData", "Local", prog)
            os.makedirs(folder, exist_ok=True)
            with open(os.path.join(folder, "data.txt"), "w", encoding="utf-8") as fh:
                fh.write("leftover")

            history_path = os.path.join(hist_dir, "history.jsonl")
            with open(history_path, "w", encoding="utf-8") as fh:
                fh.write(
                    json.dumps(
                        {
                            "action": "uninstall",
                            "program": prog,
                            "publisher": pub,
                        }
                    )
                    + "\n"
                )

            active = {}

            def fake_history():
                rows = []
                with open(history_path, encoding="utf-8") as fh:
                    for line in fh:
                        rows.append(json.loads(line))
                return rows

            def fake_roots():
                return [os.path.join(tmp, "AppData", "Local")]

            with mock.patch.object(lo_mod, "_quantum_history_removed", fake_history):
                with mock.patch.object(lo_mod, "_candidate_appdata_roots", fake_roots):
                    with mock.patch.object(lo_mod, "_read_uninstall_install_locations", return_value=active):
                        items = lo_mod._orphan_install_locations(active)
            self.assertEqual(len(items), 1)
            self.assertEqual(items[0]["program"], prog)
            self.assertTrue(os.path.isdir(items[0]["folder"]))

    def test_refuse_recent_modification_90_days(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = os.path.join(tmp, "AppData", "Local", "OldProduct")
            os.makedirs(folder, exist_ok=True)
            with open(os.path.join(folder, "a.txt"), "w", encoding="utf-8") as fh:
                fh.write("x")
            lo_mod._last_scan_folders = {
                lo_mod._norm(folder): {
                    "id": lo_mod._folder_item_id(folder),
                    "folder": folder,
                    "last_modified": time.time(),
                    "program": "OldProduct",
                }
            }
            can, reason, _ = lo_mod._removal_refusal(folder, time.time())
            self.assertFalse(can)
            self.assertIn("90 days", reason)
            result = lo_mod.leftovers_recycle(folder, confirmed=True)
            self.assertFalse(result["success"])
            self.assertIn("90 days", result["message"])

    def test_refuse_running_process_handle(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = os.path.join(tmp, "AppData", "Local", "InUseProduct")
            os.makedirs(folder, exist_ok=True)
            old = time.time() - (100 * 86400)
            os.utime(folder, (old, old))
            lo_mod._last_scan_folders = {
                lo_mod._norm(folder): {
                    "id": lo_mod._folder_item_id(folder),
                    "folder": folder,
                    "last_modified": old,
                    "program": "InUseProduct",
                }
            }
            with mock.patch.object(lo_mod, "_folder_has_open_handle", return_value=True):
                result = lo_mod.leftovers_recycle(folder, confirmed=True)
            self.assertFalse(result["success"])
            self.assertIn("open", result["message"].lower())

    def test_missing_backup_refuse(self):
        entry_id = "deadbeefdeadbeefdeadbeef"
        lo_mod._registry_catalog[entry_id] = {
            "entry_id": entry_id,
            "hive": winreg.HKEY_CURRENT_USER,
            "hive_name": "HKCU",
            "subkey": self.TEST_UNINSTALL + r"\Fake",
            "wow64": False,
            "folder": r"C:\missing\folder",
        }
        lo_mod._reg_confirm_tokens["tok"] = {
            "entry_id": entry_id,
            "backup_id": "missing-backup",
            "expires_at": time.time() + 60,
        }
        result = lo_mod.leftovers_delete_registry(entry_id, "tok")
        self.assertFalse(result["success"])
        self.assertIn("backup", result["message"].lower())

    def test_backup_then_delete_and_restore(self):
        subkey = self.TEST_UNINSTALL + "\\" + uuid.uuid4().hex
        name = "Quantum Leftover Test App"
        try:
            winreg.CreateKey(winreg.HKEY_CURRENT_USER, self.TEST_UNINSTALL)
        except OSError:
            pass
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, subkey) as key:
            winreg.SetValueEx(key, "DisplayName", 0, winreg.REG_SZ, name)
            winreg.SetValueEx(
                key,
                "InstallLocation",
                0,
                winreg.REG_SZ,
                r"C:\QuantumUpdaterMissingInstall",
            )

        entry_id = lo_mod._registry_entry_id("HKCU", subkey)
        with tempfile.TemporaryDirectory() as tmp:
            lo_mod.configure_test_hooks(backup_root=os.path.join(tmp, "registry-backups"))
            lo_mod._registry_catalog[entry_id] = {
                "entry_id": entry_id,
                "hive": winreg.HKEY_CURRENT_USER,
                "hive_name": "HKCU",
                "subkey": subkey,
                "wow64": False,
                "folder": r"C:\QuantumUpdaterMissingInstall",
            }
            confirm = lo_mod.leftovers_confirm_registry(entry_id)
            self.assertTrue(confirm["success"])
            token = confirm["token"]
            backup_id = confirm["backup_id"]
            self.assertTrue(os.path.isfile(lo_mod._backup_path(backup_id)))

            deleted = lo_mod.leftovers_delete_registry(entry_id, token)
            self.assertTrue(deleted["success"])

            with self.assertRaises(OSError):
                winreg.OpenKey(winreg.HKEY_CURRENT_USER, subkey)

            restored = lo_mod.leftovers_restore_registry(backup_id)
            self.assertTrue(restored["success"])
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, subkey) as key:
                val, _ = winreg.QueryValueEx(key, "DisplayName")
                self.assertEqual(val, name)
        try:
            winreg.DeleteKey(winreg.HKEY_CURRENT_USER, subkey)
        except OSError:
            pass
        try:
            winreg.DeleteKey(winreg.HKEY_CURRENT_USER, self.TEST_UNINSTALL)
        except OSError:
            pass
        lo_mod.reset_test_hooks()

    def test_recycle_temp_fixture(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = os.path.join(tmp, "AppData", "Local", "RecycleMe")
            os.makedirs(folder, exist_ok=True)
            old = time.time() - (100 * 86400)
            probe = os.path.join(folder, "old.dat")
            with open(probe, "wb") as fh:
                fh.write(b"data")
            os.utime(folder, (old, old))
            os.utime(probe, (old, old))

            def fake_roots():
                return [os.path.join(tmp, "AppData", "Local")]

            history_row = {
                "action": "uninstall",
                "program": "RecycleMe",
                "publisher": "QuantumUpdaterTestPublisher",
                "install_location": folder,
            }

            with mock.patch.object(lo_mod, "_quantum_history_removed", lambda: [history_row]):
                with mock.patch.object(lo_mod, "_candidate_appdata_roots", fake_roots):
                    with mock.patch.object(lo_mod, "_read_uninstall_install_locations", return_value={}):
                        scan = lo_mod.leftovers_scan()
            self.assertEqual(scan["count"], 1)
            item = scan["items"][0]
            self.assertTrue(item.get("can_recycle"))

            with mock.patch.object(lo_mod, "_move_folder_to_recycle", return_value=(True, "ok")):
                result = lo_mod.leftovers_recycle(folder, confirmed=True)
            self.assertTrue(result["success"])


if __name__ == "__main__":
    unittest.main()
