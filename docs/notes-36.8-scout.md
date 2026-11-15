# 36.8 Scout — fake / silent progress (2026-11-12)

Source: Scout agent. Use for worker slices. Prefer honest indeterminate over invented numbers.

## P0 (island honesty — workers on app.js)
- setIslandState null pct → fake 30% (`web/app.js` ~3398)
- executeCleanup seeds 0.15 → 15% (`~4700`); finish clears with no summary (`~4723`)
- uninstall / memory / leftovers / large-file actions silent or fake bar
- scanUpdates `job.percent || 10` invents 10% (`~2548`)
- batch upgrade `stepPct` from queue index (`~2913`)

## P1 (backend progress)
- catalog-updates skipped in `_tick_progress` (`core/scan_orchestrator.py`)
- uninstall no `on_progress` (`web_api.py`, `manager.py`, `winget_engine.py`)
- large_files `scan_status` missing top-level `percent`
- leftovers_job no `update_job_progress`
- cleanup clean only per-category; scan stalls on large category

## P2
- dock fill not updated in `updateGlobalDock`
- sidebar disk default 45% in HTML
- nav ring stuck at 0% for updates/leftovers
