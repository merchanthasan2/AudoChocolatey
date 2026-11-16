import unittest

from core.files_in_use import (
    ProcessModuleSnapshot,
    WINGET_ERROR_APPLICATION_IN_USE,
    build_files_in_use_message,
    exe_to_display_name,
    files_in_use_operation_fields,
    find_process_holders,
    is_winget_files_in_use,
)

OBS_ROOT = r"C:\Program Files\obs-studio"
OBS_DLL = (
    r"C:\Program Files\obs-studio\data\obs-plugins\win-dshow\obs-virtualcam-module64.dll"
)


class WingetFilesInUseTests(unittest.TestCase):
    def test_hresult_alone_is_files_in_use(self):
        self.assertTrue(is_winget_files_in_use(WINGET_ERROR_APPLICATION_IN_USE, ""))

    def test_bare_exit_code_six_without_in_use_phrase_is_not_enough(self):
        self.assertFalse(is_winget_files_in_use(6, "Something failed with exit code: 6"))

    def test_installer_exit_six_with_in_use_phrase(self):
        output = (
            "ShellExecute installer failed: 6\n"
            "Application is currently in use by another application.\n"
        )
        self.assertTrue(is_winget_files_in_use(6, output))

    def test_find_holders_from_fake_module_list(self):
        processes = [
            ProcessModuleSnapshot(
                pid=8168,
                exe_name="chrome.exe",
                module_paths=(OBS_DLL, r"C:\Windows\System32\kernel32.dll"),
            ),
            ProcessModuleSnapshot(
                pid=4242,
                exe_name="notepad.exe",
                module_paths=(r"C:\Windows\System32\notepad.exe",),
            ),
        ]
        holders = find_process_holders(OBS_ROOT, processes=processes)
        self.assertEqual(len(holders), 1)
        self.assertEqual(holders[0]["name"], "Chrome")
        self.assertEqual(holders[0]["pid"], 8168)

    def test_files_in_use_message_names_chrome(self):
        processes = [
            ProcessModuleSnapshot(
                pid=8168,
                exe_name="chrome.exe",
                module_paths=(OBS_DLL,),
            ),
        ]
        fields = files_in_use_operation_fields(
            "OBSProject.OBSStudio",
            WINGET_ERROR_APPLICATION_IN_USE,
            "",
            processes=processes,
            install_dir_resolver=lambda _pid, _name: OBS_ROOT,
            main_exe_finder=lambda _root, _name: r"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
        )
        self.assertIsNotNone(fields)
        message, holders = fields
        self.assertEqual(exe_to_display_name("chrome.exe"), "Chrome")
        self.assertEqual(holders[0]["name"], "Chrome")
        self.assertIn("Chrome", message)
        self.assertIn("Close Chrome", message)

    def test_build_files_in_use_message_without_holders(self):
        msg = build_files_in_use_message([])
        self.assertIn("Another program is using", msg)
        self.assertIn("browsers", msg)


if __name__ == "__main__":
    unittest.main()
