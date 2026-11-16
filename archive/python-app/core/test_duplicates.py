import os
import tempfile
import unittest

from core.system_tools import duplicates


class DuplicatesTests(unittest.TestCase):
    def setUp(self):
        duplicates._last_scan_groups = []
        duplicates._last_scan_index = {}

    def test_scan_finds_duplicate_group(self):
        with tempfile.TemporaryDirectory() as tmp:
            path_a = os.path.join(tmp, "a.bin")
            path_b = os.path.join(tmp, "nested", "b.bin")
            os.makedirs(os.path.dirname(path_b), exist_ok=True)
            payload = b"x" * 2048
            with open(path_a, "wb") as fh:
                fh.write(payload)
            with open(path_b, "wb") as fh:
                fh.write(payload)
            result = duplicates.scan_duplicates(tmp, min_bytes=1024)
            self.assertGreaterEqual(result["count"], 1)
            group = result["groups"][0]
            self.assertEqual(len(group["paths"]), 2)
            self.assertIn(path_a, group["paths"])
            self.assertIn(path_b, group["paths"])

    def test_recycle_requires_confirm(self):
        result = duplicates.recycle_duplicates([r"C:\nope.txt"])
        self.assertFalse(result["success"])
        self.assertIn("Confirmation", result["message"])

    def test_recycle_keeps_one_file_per_group(self):
        with tempfile.TemporaryDirectory() as tmp:
            path_a = os.path.join(tmp, "a.bin")
            path_b = os.path.join(tmp, "b.bin")
            payload = b"y" * 2048
            with open(path_a, "wb") as fh:
                fh.write(payload)
            with open(path_b, "wb") as fh:
                fh.write(payload)
            scan = duplicates.scan_duplicates(tmp, min_bytes=1024)
            self.assertGreaterEqual(scan["count"], 1)
            blocked = duplicates.recycle_duplicates(
                [path_a, path_b],
                confirmed=True,
            )
            self.assertFalse(blocked["success"])
            ok = duplicates.recycle_duplicates([path_b], confirmed=True)
            self.assertTrue(ok["success"])
            self.assertTrue(os.path.isfile(path_a))
            self.assertFalse(os.path.isfile(path_b))


if __name__ == "__main__":
    unittest.main()