import inspect
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from web_api import WebAPI
from core.manager import PackageManager
from core.windows_cleaner import WINDOWS_DEFERRED_CATEGORY_IDS, WindowsCleaner

REPO_ROOT = Path(__file__).resolve().parents[1]


class CleanupSafetyTests(unittest.TestCase):
    def test_one_click_clean_api_removed(self):
        self.assertFalse(hasattr(WebAPI, "one_click_clean"))
        api = WebAPI(PackageManager())
        self.assertFalse(callable(getattr(api, "one_click_clean", None)))

    def test_frontend_no_one_click_clean_path(self):
        app_js = (REPO_ROOT / "web" / "app.js").read_text(encoding="utf-8")
        self.assertNotIn("oneClickClean", app_js)
        self.assertNotIn("one_click_clean", app_js)
        index_html = (REPO_ROOT / "web" / "index.html").read_text(encoding="utf-8")
        self.assertIn("openCleanupConsentModal('purge')", index_html)
        self.assertNotIn("oneClickClean()", index_html)

    def test_delete_guard_blocks_windows_installer_roots(self):
        cleaner = WindowsCleaner()
        system_root = cleaner.system_root
        blocked = [
            system_root,
            os.path.join(system_root, "WinSxS", "x"),
            os.path.join(system_root, "System32", "kernel32.dll"),
            os.path.join(system_root, "SysWOW64", "x"),
            os.path.join(system_root, "Installer", "abc.msi"),
            os.path.join(system_root, "Temp", "safe-looking.tmp"),
        ]
        for path in blocked:
            with self.subTest(path=path):
                self.assertTrue(cleaner._is_forbidden_delete_path(path))

    def test_unsafe_category_paths_removed(self):
        cleaner = WindowsCleaner()
        paths_joined = "\n".join(
            p for cat in cleaner.get_category_definitions() for p in cat.get("paths", [])
        ).lower()
        forbidden_fragments = (
            os.path.join(".nuget", "packages").lower(),
            "package cache",
            "desktopappinstaller",
            os.path.join("spotify", "data").lower(),
        )
        for fragment in forbidden_fragments:
            with self.subTest(fragment=fragment):
                self.assertNotIn(fragment, paths_joined)

    def test_headline_excludes_windows_owned_categories(self):
        with tempfile.TemporaryDirectory() as tmp:
            cleaner = WindowsCleaner()
            cleaner.temp_dir = os.path.join(tmp, "UserTemp")
            cleaner.system_root = os.path.join(tmp, "FakeWindows")
            cleaner.local_appdata = os.path.join(tmp, "LocalAppData")
            os.makedirs(cleaner.temp_dir, exist_ok=True)
            os.makedirs(os.path.join(cleaner.system_root, "Temp"), exist_ok=True)
            os.makedirs(os.path.join(cleaner.system_root, "Minidump"), exist_ok=True)
            os.makedirs(
                os.path.join(cleaner.system_root, "SoftwareDistribution", "Download"),
                exist_ok=True,
            )

            user_file = os.path.join(cleaner.temp_dir, "scratch.tmp")
            with open(user_file, "wb") as handle:
                handle.write(b"a" * 2048)

            win_temp_file = os.path.join(cleaner.system_root, "Temp", "svc.tmp")
            with open(win_temp_file, "wb") as handle:
                handle.write(b"b" * 4096)

            dump_file = os.path.join(cleaner.system_root, "Minidump", "dump.dmp")
            with open(dump_file, "wb") as handle:
                handle.write(b"c" * 8192)

            minimal_categories = [
                {
                    "id": "user_temp",
                    "name": "User Temporary Files",
                    "tag": "Safe to Clean",
                    "risk": "low",
                    "description": "Temp",
                    "roots": [cleaner.temp_dir],
                    "svg_icon": "trash-2",
                },
                {
                    "id": "system_temp",
                    "name": "Windows System Temp",
                    "tag": "Needs a Windows decision",
                    "risk": "low",
                    "description": "System temp",
                    "roots": [os.path.join(cleaner.system_root, "Temp")],
                    "svg_icon": "cpu",
                    "needs_windows_decision": True,
                    "needs_admin": True,
                },
                {
                    "id": "windows_minidump",
                    "name": "Windows Memory Minidumps",
                    "tag": "Needs a Windows decision",
                    "risk": "low",
                    "description": "Minidumps",
                    "roots": [os.path.join(cleaner.system_root, "Minidump")],
                    "svg_icon": "file-text",
                    "needs_windows_decision": True,
                    "needs_admin": True,
                },
            ]
            cleaner._engine.get_category_definitions = lambda: minimal_categories

            result = cleaner.scan()
            self.assertEqual(result["total_reclaimable_bytes"], 2048)
            self.assertEqual(result["windows_owned_bytes"], 4096 + 8192)

            by_id = {c["id"]: c for c in result["categories"]}
            for cat_id in ("system_temp", "windows_minidump"):
                with self.subTest(cat_id=cat_id):
                    self.assertTrue(by_id[cat_id]["needs_windows_decision"])
                    self.assertFalse(by_id[cat_id]["selected"])
            self.assertEqual(set(WINDOWS_DEFERRED_CATEGORY_IDS), {
                "system_temp",
                "windows_minidump",
                "windows_update_cache",
            })

            with mock.patch.object(
                cleaner._engine,
                "_clean_windows_update_downloads",
                return_value=(0, 0, 1, 0),
            ):
                clean_result = cleaner.clean(list(WINDOWS_DEFERRED_CATEGORY_IDS))
            self.assertEqual(clean_result.get("total_freed_bytes", 0), 0)

    def test_package_cache_copy_has_no_package_manager_branding(self):
        cleaner = WindowsCleaner()
        for cat in cleaner.get_category_definitions():
            blob = " ".join(
                str(cat.get(key, "")) for key in ("name", "tag", "description")
            ).lower()
            for brand in ("winget", "chocolatey", "choco", "scoop", "msstore"):
                with self.subTest(category=cat["id"], brand=brand):
                    self.assertNotIn(brand, blob)


if __name__ == "__main__":
    unittest.main()
