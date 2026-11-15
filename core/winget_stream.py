"""Shared winget stdout streaming and progress parsing."""

from __future__ import annotations

import re
import time
from typing import Callable, Optional, TextIO

_PERCENT_RE = re.compile(r"(\d{1,3})\s*%")
_ANSI_ESCAPE = re.compile(
    r"[\x1b\x9b][\[()#;?]*(?:[0-9]{1,4}(?:;[0-9]{0,4})*)?[0-9A-ORZcf-nqry=><]"
)
_TRANSFER_RE = re.compile(
    r"(\d+(?:\.\d+)?)\s*(B|KB|KiB|MB|MiB|GB|GiB)\s*/\s*(\d+(?:\.\d+)?)\s*(B|KB|KiB|MB|MiB|GB|GiB)",
    re.IGNORECASE,
)


def parse_winget_transfer_line(line: str) -> Optional[str]:
    """Return 'down / total' with units as printed by winget, if present."""
    match = _TRANSFER_RE.search(line or "")
    if not match:
        return None
    down, du, total, tu = match.groups()
    return f"{down} {du.upper()} / {total} {tu.upper()}"


def stream_winget_stdout(
    stdout: TextIO,
    on_log: Optional[Callable[[str], None]] = None,
    on_progress: Optional[Callable[[Optional[float], str], None]] = None,
) -> None:
    """Read winget output char-by-char; emit progress from percent lines."""
    buffer: list[str] = []
    last_progress_time = 0.0
    last_pct = -1.0

    while True:
        char = stdout.read(1)
        if not char:
            break

        if char in ("\r", "\n"):
            raw_line = "".join(buffer).strip()
            buffer = []

            if raw_line:
                clean_line = _ANSI_ESCAPE.sub("", raw_line).strip()
                clean_line = re.sub(r"[\x00-\x1f\x7f-\x9f]", " ", clean_line).strip()

                if clean_line:
                    pct_match = _PERCENT_RE.search(clean_line)
                    pct_val = float(pct_match.group(1)) / 100.0 if pct_match else None

                    if on_log and (char == "\n" or pct_val is None):
                        on_log(clean_line + "\n")

                    if on_progress and pct_val is not None:
                        now = time.time()
                        if (
                            (now - last_progress_time >= 0.08)
                            or abs(pct_val - last_pct) >= 0.01
                            or pct_val >= 1.0
                        ):
                            last_progress_time = now
                            last_pct = pct_val
                            on_progress(pct_val, clean_line)
        else:
            buffer.append(char)

    if buffer:
        raw_line = "".join(buffer).strip()
        if raw_line:
            clean_line = _ANSI_ESCAPE.sub("", raw_line).strip()
            if on_log:
                on_log(clean_line + "\n")
