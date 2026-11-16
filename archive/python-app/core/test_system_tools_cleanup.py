import os
import secrets
import tempfile
import time
import unittest
from unittest import mock

from core.system_tools import safety
from core.system_tools.cleanup import CleanupEngine, PLAN_TTL_SECONDS
from core.system_tools.drives import enumerate_cleanup_volumes


class SystemToolsSafetyTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        self.cat_root = os.path.join(self.tmp, "temp")
        self.protected = os.path.join(self.tmp, "protected")
        os.makedirs(self.cat_root, exist_ok=True)
        os.makedirs(self.protected, exist_ok=True)
        with open(os.path.join(self.protected, "secret.txt"), "w", encoding="utf-8") as handle:
            handle.write("stay")

    def tearDown(self):
        import shutil

        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_junction_to_protected_folder_refused(self):
        if not hasattr(os, "symlink"):
            self.skipTest("symlinks unavailable")
        junction = os.path.join(self.cat_root, "link")
        try:
            os.symlink(self.protected, junction, target_is_directory=True)
        except OSError:
            self.skipTest("could not create junction")
        target = os.path.join(junction, "secret.txt")
        outcome, _ = safety.try_delete_file(target, self.cat_root)
        self.assertIn(outcome, ("blocked",))
        self.assertTrue(os.path.isfile(os.path.join(self.protected, "secret.txt")))

    def test_dotdot_traversal_blocked(self):
        sneaky = os.path.join(self.cat_root, "..", "protected", "secret.txt")
        outcome, _ = safety.try_delete_file(sneaky, self.cat_root)
        self.assertEqual(outcome, "blocked")

    def test_category_root_delete_refused(self):
        outcome, _ = safety.try_delete_file(self.cat_root, self.cat_root)
        self.assertEqual(outcome, "blocked")

    def test_denylist_blocks_program_files(self):
        pf = os.environ.get("ProgramFiles", r"C:\Program Files")
        sample = os.path.join(pf, "Example", "app.exe")
        self.assertTrue(safety.is_globally_denied(sample))

    def test_locked_file_reported_skipped(self):
        locked = os.path.join(self.cat_root, "locked.bin")
        with open(locked, "wb") as handle:
            handle.write(b"x" * 16)
        handle = open(locked, "rb")
        try:
            outcome, _ = safety.try_delete_file(locked, self.cat_root)
            self.assertIn(outcome, ("skipped_in_use", "blocked", "needs_admin"))
        finally:
            handle.close()
            if os.path.exists(locked):
                os.remove(locked)


class CleanupEnginePlanTests(unittest.TestCase):
    def test_unknown_category_returns_error(self):
        engine = CleanupEngine(temp_dir=tempfile.mkdtemp())
        plan = engine.cleanup_get_plan(["does_not_exist"])
        self.assertIn("error", plan)

    def test_stale_token_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            engine = CleanupEngine(temp_dir=tmp)
            path = os.path.join(tmp, "a.bin")
            with open(path, "wb") as handle:
                handle.write(b"z" * 64)
            engine._last_scan_result = engine.scan()
            plan = engine.cleanup_get_plan(["user_temp"])
            token = plan["token"]
            engine._plans[token]["expires_at"] = time.time() - 1
            result = engine.cleanup_execute(token)
            self.assertIn("expired", result.get("error", "").lower())

    def test_token_id_mismatch_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            engine = CleanupEngine(temp_dir=tmp)
            with open(os.path.join(tmp, "a.bin"), "wb") as handle:
                handle.write(b"z" * 64)
            engine._last_scan_result = engine.scan()
            plan = engine.cleanup_get_plan(["user_temp"])
            token = plan["token"]
            result = engine.cleanup_execute(token, category_ids=["user_temp", "browser_caches"])
            self.assertIn("not in token", result.get("error", "").lower())

    def test_reboot_pending_refuses_windows_owned(self):
        with tempfile.TemporaryDirectory() as tmp:
            engine = CleanupEngine(
                temp_dir=tmp,
                system_root=os.path.join(tmp, "Win"),
            )
            os.makedirs(os.path.join(engine.system_root, "SoftwareDistribution", "Download"), exist_ok=True)
            engine._last_scan_result = {"scan_id": "abc"}
            engine._plans["abc.tok"] = {
                "scan_id": "abc",
                "expires_at": time.time() + PLAN_TTL_SECONDS,
                "categories": [{"id": "windows_update_cache", "size_bytes": 0, "file_count": 0}],
            }
            with mock.patch.object(engine, "reboot_pending", return_value=True):
                res = engine.cleanup_execute("abc.tok")
            self.assertGreater(res.get("blocked", 0), 0)


class DriveFilterTests(unittest.TestCase):
    def test_virtual_volume_marker_filtered(self):
        from core.system_tools.drives import _is_virtual_label, _probe_letter

        self.assertTrue(_is_virtual_label("Google Drive", "NTFS"))
        with mock.patch("core.system_tools.drives._volume_label", return_value="Google Drive"):
            with mock.patch("core.system_tools.drives._volume_fs", return_value="NTFS"):
                row = _probe_letter("G", 3)
        self.assertIsNone(row)


class SentinelCleanupTests(unittest.TestCase):
    def test_sentinel_user_temp_file_removed(self):
        temp_root = os.environ.get("TEMP") or tempfile.gettempdir()
        name = f"qu_sentinel_{secrets.token_hex(4)}.bin"
        sentinel = os.path.join(temp_root, name)
        payload = b"s" * (5 * 1024 * 1024)
        with open(sentinel, "wb") as handle:
            handle.write(payload)

        engine = CleanupEngine(temp_dir=temp_root, test_path_allowlist=lambda p: os.path.basename(p) == name)
        engine._category_deletable_files["user_temp"] = [sentinel]
        result = engine._execute_category_ids(["user_temp"])
        try:
            self.assertFalse(os.path.exists(sentinel))
            self.assertGreaterEqual(result.get("freed_bytes", 0), 5 * 1024 * 1024)
            self.assertGreaterEqual(result.get("removed_files", 0), 1)
        finally:
            if os.path.exists(sentinel):
                os.remove(sentinel)


class Wave1CleanupCategoryTests(unittest.TestCase):
    def test_wave1_categories_are_defined(self):
        engine = CleanupEngine(temp_dir=tempfile.mkdtemp())
        ids = {c["id"] for c in engine.get_category_definitions()}
        self.assertIn("recycle_bin", ids)
        self.assertIn("thumbnail_cache", ids)
        self.assertIn("directx_cache", ids)
        self.assertIn("browser_history_cookies", ids)
        self.assertIn("delivery_optimization", ids)
        privacy = next(c for c in engine.get_category_definitions() if c["id"] == "browser_history_cookies")
        self.assertTrue(privacy.get("opt_in"))
        dx = next(c for c in engine.get_category_definitions() if c["id"] == "directx_cache")
        self.assertTrue(dx.get("rebuild_warning"))

    def test_privacy_scan_lists_cookie_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            local = os.path.join(tmp, "Local")
            profile = os.path.join(local, "Google", "Chrome", "User Data", "Default")
            os.makedirs(profile, exist_ok=True)
            cookie = os.path.join(profile, "Cookies")
            with open(cookie, "wb") as handle:
                handle.write(b"c" * 128)
            engine = CleanupEngine(temp_dir=tmp, local_appdata=local, appdata=os.path.join(tmp, "Roaming"))
            row = engine._scan_one_category(
                next(c for c in engine.get_category_definitions() if c["id"] == "browser_history_cookies")
            )
            self.assertIn(cookie, row["deletable_paths"])
            self.assertGreaterEqual(row["file_count"], 1)

    def test_delivery_optimization_skipped_when_servicing(self):
        with tempfile.TemporaryDirectory() as tmp:
            win = os.path.join(tmp, "Win")
            cache = os.path.join(
                win, "ServiceProfiles", "NetworkService", "AppData", "Local",
                "Microsoft", "Windows", "DeliveryOptimization", "Cache",
            )
            os.makedirs(cache, exist_ok=True)
            blob = os.path.join(cache, "blob.bin")
            with open(blob, "wb") as handle:
                handle.write(b"d" * 64)
            engine = CleanupEngine(temp_dir=tmp, system_root=win)
            with mock.patch.object(engine, "update_in_progress", return_value=True):
                with mock.patch.object(engine, "reboot_pending", return_value=False):
                    res = engine._execute_category_ids(["delivery_optimization"])
            self.assertGreaterEqual(res.get("blocked", 0), 1)
            self.assertTrue(os.path.isfile(blob))


class RegisterCleanupScanJobTests(unittest.TestCase):

    def test_orchestrator_scan_runs_on_shared_engine(self):
        import time

        from core.manager import PackageManager
        from core.scan_orchestrator import ScanOrchestrator
        from core.system_tools.cleanup import register_cleanup_scan_job

        manager = PackageManager()
        orch = ScanOrchestrator(manager)
        engine = manager.cleaner._engine
        register_cleanup_scan_job(orch, engine)
        payload = {"scan_id": "unit-test", "categories": []}
        with mock.patch.object(engine, "scan", return_value=payload) as scan_mock:
            orch.scan_start("cleanup_scan", force=True)
            deadline = time.time() + 8.0
            status = "queued"
            while time.time() < deadline:
                status = orch.scans_state()["jobs"]["cleanup_scan"]["status"]
                if status == "ready":
                    break
                time.sleep(0.05)
        self.assertEqual(status, "ready")
        scan_mock.assert_called_once()
        args, kwargs = scan_mock.call_args
        self.assertIn("budget_s", kwargs)


if __name__ == "__main__":
    unittest.main()
