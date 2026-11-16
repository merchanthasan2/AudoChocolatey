import json
import os
import sys
import tempfile
import unittest
from unittest.mock import patch

from core.system_tools import scheduled_cleanup


class ScheduledCleanupTests(unittest.TestCase):
    def tearDown(self):
        scheduled_cleanup.reset_test_hooks()

    def test_save_and_list_schedule_json(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "cleanup_schedule.json")
            def _fake_schtasks(_args):
                import subprocess

                return subprocess.CompletedProcess(args=_args, returncode=0, stdout="", stderr="")

            scheduled_cleanup.configure_test_hooks(schedule_path=path, task_runner=_fake_schtasks)
            saved = scheduled_cleanup.save_schedule(
                ["user_temp", "browser_caches", "invalid"],
                "weekly",
            )
            self.assertTrue(saved["success"])
            self.assertEqual(saved["categories"], ["user_temp", "browser_caches"])
            with open(path, "r", encoding="utf-8") as fh:
                data = json.load(fh)
            self.assertEqual(data["cadence"], "weekly")
            listed = scheduled_cleanup.list_schedule()
            self.assertTrue(listed["configured"])
            self.assertEqual(listed["cadence"], "weekly")

    def test_save_rejects_empty_categories(self):
        result = scheduled_cleanup.save_schedule([], "daily")
        self.assertFalse(result["success"])

    def test_scheduled_argv_command_includes_flags(self):
        cmd = scheduled_cleanup.scheduled_cleanup_command()
        self.assertIn("--scheduled-cleanup", cmd)
        self.assertIn("--no-elevate", cmd)

    def test_main_scheduled_path_runs_engine(self):
        with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False, encoding="utf-8") as fh:
            json.dump({"categories": ["recycle_bin"], "cadence": "daily"}, fh)
            schedule_path = fh.name
        scheduled_cleanup.configure_test_hooks(schedule_path=schedule_path)
        clean_result = {
            "total_freed_bytes": 100,
            "total_freed_str": "100 B",
            "files_removed": 2,
        }
        with patch("core.system_tools.cleanup.CleanupEngine.clean", return_value=clean_result):
            result = scheduled_cleanup.run_scheduled_cleanup()
        self.assertTrue(result["success"])
        os.remove(schedule_path)


class ScheduledCleanupMainTests(unittest.TestCase):
    def test_main_argv_branch(self):
        import main as main_mod

        argv = sys.argv[:]
        try:
            sys.argv = ["main.py", "--scheduled-cleanup", "--no-elevate"]
            with patch.object(main_mod, "run_scheduled_cleanup_cli") as runner, patch.object(
                main_mod, "elevate_if_needed"
            ) as elevate:
                main_mod.main()
                runner.assert_called_once()
                elevate.assert_not_called()
        finally:
            sys.argv = argv


if __name__ == "__main__":
    unittest.main()