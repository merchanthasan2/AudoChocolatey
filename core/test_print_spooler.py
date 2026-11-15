import unittest
from unittest.mock import patch
import subprocess

from core.system_tools import print_spooler


class PrintSpoolerTests(unittest.TestCase):
    def test_cancel_requires_confirm(self):
        result = print_spooler.cancel_print_job("Printer", "1")
        self.assertFalse(result["ok"])
        self.assertTrue(result.get("needs_confirm"))

    def test_clear_requires_confirm(self):
        result = print_spooler.clear_print_queue_and_restart_spooler()
        self.assertFalse(result["ok"])
        self.assertTrue(result.get("needs_confirm"))

    def test_cancel_rejects_bad_printer(self):
        result = print_spooler.cancel_print_job("bad/name", "1", confirmed=True)
        self.assertFalse(result["ok"])
        self.assertIn("not allowed", result["error"].lower())

    def test_list_parses_jobs(self):
        payload = (
            '[{"printer":"Office","id":3,"document":"doc.pdf","user":"me",'
            '"status":"Printing","size_bytes":12,"submitted":"2026-01-01"}]'
        )
        completed = subprocess.CompletedProcess(args=[], returncode=0, stdout=payload, stderr="")
        with patch.object(print_spooler, "_ps_run", return_value=completed):
            result = print_spooler.list_print_jobs()
        self.assertTrue(result["ok"])
        self.assertEqual(result["count"], 1)
        self.assertEqual(result["jobs"][0]["id"], "3")

    def test_cancel_checks_live_list(self):
        listed = {
            "ok": True,
            "jobs": [{"printer": "Office", "id": "3", "document": "a"}],
        }
        completed = subprocess.CompletedProcess(args=[], returncode=0, stdout="", stderr="")
        with patch.object(print_spooler, "list_print_jobs", return_value=listed), patch.object(
            print_spooler, "_ps_run", return_value=completed
        ) as run:
            result = print_spooler.cancel_print_job("Office", "3", confirmed=True)
        self.assertTrue(result["ok"])
        self.assertIn("Remove-PrintJob", run.call_args[0][0])

    def test_clear_runs_spooler_script(self):
        completed = subprocess.CompletedProcess(args=[], returncode=0, stdout="Running", stderr="")
        with patch.object(print_spooler, "_ps_run", return_value=completed) as run:
            result = print_spooler.clear_print_queue_and_restart_spooler(confirmed=True)
        self.assertTrue(result["ok"])
        cmd = run.call_args[0][0]
        self.assertIn("Stop-Service", cmd)
        self.assertIn("Start-Service", cmd)
        self.assertIn("PRINTERS", cmd)


if __name__ == "__main__":
    unittest.main()
