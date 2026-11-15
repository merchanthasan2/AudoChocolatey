"""Streaming winget upgrades must not hang the batch forever."""

from __future__ import annotations

import threading
import unittest
from unittest.mock import MagicMock, patch

from core.winget_engine import WINGET_STREAM_TIMEOUT_EXIT, WingetEngine


class TestWingetStreamTimeout(unittest.TestCase):
    def test_streaming_cmd_times_out_and_returns_sentinel(self):
        engine = WingetEngine()
        engine._executable = "winget.exe"
        release = threading.Event()
        fake_proc = MagicMock()
        fake_proc.stdout = MagicMock()
        fake_proc.pid = 4242
        fake_proc.poll.return_value = None

        def _block_stream(stdout, on_log=None, on_progress=None):
            release.wait(30)

        def _wait():
            release.wait(30)
            return 1

        fake_proc.wait.side_effect = _wait

        with patch.object(engine, "abort") as abort_mock:
            with patch("core.winget_engine.subprocess.Popen", return_value=fake_proc):
                with patch("core.winget_engine.stream_winget_stdout", side_effect=_block_stream):
                    code = engine._run_streaming_cmd(
                        ["upgrade", "--id", "Demo.Pkg"],
                        timeout_s=1,
                    )
            release.set()
        self.assertEqual(code, WINGET_STREAM_TIMEOUT_EXIT)
        self.assertTrue(abort_mock.called)

    def test_upgrade_package_maps_timeout_to_notice(self):
        engine = WingetEngine()
        engine._executable = "winget.exe"
        with patch.object(engine, "_run_streaming_cmd", return_value=WINGET_STREAM_TIMEOUT_EXIT):
            status = engine.upgrade_package("Demo.Pkg")
        self.assertFalse(status.success)
        self.assertEqual(status.reason, "timeout")
        self.assertIn("timed out", status.message.lower())


if __name__ == "__main__":
    unittest.main()
