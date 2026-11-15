"""Job progress estimation for scan orchestrator."""

from __future__ import annotations

import statistics
from typing import List, Optional

DEFAULT_CATALOG_DURATION = 12.0
MAX_PERCENT_BEFORE_DONE = 95


def median_duration(durations: List[float], default: float = DEFAULT_CATALOG_DURATION) -> float:
    if not durations:
        return default
    return float(statistics.median(durations[-5:]))


def estimate_percent(
    elapsed_s: float,
    durations: List[float],
    *,
    done: bool = False,
    default_duration: float = DEFAULT_CATALOG_DURATION,
) -> int:
    if done:
        return 100
    expected = median_duration(durations, default_duration)
    if expected <= 0:
        expected = default_duration
    pct = int((elapsed_s / expected) * 100)
    return min(MAX_PERCENT_BEFORE_DONE, max(0, pct))


def estimate_eta_s(
    elapsed_s: float,
    durations: List[float],
    *,
    done: bool = False,
    default_duration: float = DEFAULT_CATALOG_DURATION,
) -> int:
    if done:
        return 0
    expected = median_duration(durations, default_duration)
    return max(0, int(round(expected - elapsed_s)))
