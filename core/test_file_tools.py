import os
import tempfile
import unittest
import zipfile

from core.system_tools import file_tools as ft


class FileToolsTests(unittest.TestCase):
    def test_zip_roundtrip_and_checksum(self):
        with tempfile.TemporaryDirectory() as tmp:
            src = os.path.join(tmp, "src")
            os.makedirs(src)
            sample = os.path.join(src, "a.txt")
            with open(sample, "w", encoding="utf-8") as handle:
                handle.write("hello")
            zpath = os.path.join(tmp, "out.zip")
            created = ft.create_zip(src, zpath)
            self.assertTrue(created["ok"], created)
            dest = os.path.join(tmp, "out")
            extracted = ft.extract_zip(zpath, dest, confirmed=True)
            self.assertTrue(extracted["ok"], extracted)
            self.assertTrue(os.path.isfile(os.path.join(dest, "a.txt")))
            chk = ft.file_checksum(sample)
            self.assertTrue(chk["ok"])
            ver = ft.verify_checksum(sample, chk["hex"])
            self.assertTrue(ver["match"])

    def test_extract_needs_confirm(self):
        self.assertTrue(ft.extract_zip("a.zip", "b").get("needs_confirm"))

    def test_empty_folders(self):
        with tempfile.TemporaryDirectory() as tmp:
            empty = os.path.join(tmp, "empty")
            os.makedirs(empty)
            result = ft.find_empty_folders(tmp)
            self.assertTrue(result["ok"])
            self.assertIn(empty, result["folders"])

    def test_end_process_needs_confirm(self):
        self.assertTrue(ft.end_process(1234).get("needs_confirm"))


if __name__ == "__main__":
    unittest.main()
