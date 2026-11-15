import os
import tempfile
import time
import unittest

from core.system_tools import large_files as lf


@unittest.skipIf(os.name != "nt", "Windows only")
class LargeFilesTests(unittest.TestCase):
    def test_scan_filters_and_denylist(self):
        with tempfile.TemporaryDirectory() as tmp:
            big = os.path.join(tmp, "big.bin")
            small = os.path.join(tmp, "small.bin")
            with open(big, "wb") as fh:
                fh.write(b"x" * (2 * 1024 * 1024))
            with open(small, "wb") as fh:
                fh.write(b"y" * 1024)
            job = lf.scan_start(root=tmp, min_mb=1, older_than_days=0)
            deadline = time.time() + 30
            status = lf.scan_status(job)
            while status.get("status") != "done" and time.time() < deadline:
                time.sleep(0.1)
                status = lf.scan_status(job)
            self.assertEqual(status["status"], "done")
            paths = {r["path"] for r in status["results"]}
            self.assertIn(big, paths)
            self.assertNotIn(small, paths)

            win = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "notepad.exe")
            ok, msg, _ = lf._paths_allowed([win])
            self.assertFalse(ok)

    def test_recycle_vs_permanent_token(self):
        with tempfile.TemporaryDirectory() as tmp:
            target = os.path.join(tmp, "remove_me.bin")
            with open(target, "wb") as fh:
                fh.write(b"z" * (2 * 1024 * 1024))
            job = lf.scan_start(root=tmp, min_mb=0.001)
            deadline = time.time() + 30
            while lf.scan_status(job).get("status") != "done" and time.time() < deadline:
                time.sleep(0.05)

            bad = lf.delete_permanently([target], "bad-token")
            self.assertFalse(bad["success"])

            confirm = lf.confirm_delete([target], job_id=job)
            token = confirm["token"]
            deleted = lf.delete_permanently([target], token)
            self.assertTrue(deleted["success"])
            self.assertFalse(os.path.exists(target))

            stale = lf.delete_permanently([target], token)
            self.assertFalse(stale["success"])

    def test_recycle_requires_scan(self):
        with tempfile.TemporaryDirectory() as tmp:
            target = os.path.join(tmp, "x.bin")
            with open(target, "wb") as fh:
                fh.write(b"a" * 5000)
            result = lf.move_to_recycle_bin([target])
            self.assertFalse(result["success"])


if __name__ == "__main__":
    unittest.main()
