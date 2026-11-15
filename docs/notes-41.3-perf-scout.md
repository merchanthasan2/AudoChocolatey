# 41.3 Performance scout (2026-11-12)

Source: Scout. Ranked delays for install / update / scan sluggishness.

## P0
1. Single catalog worker — updates then installed winget back-to-back (`scan_orchestrator.py`)
2. Cached catalog still re-queued every `scans_start_all` — double winget every launch
3. Bootstrap starts all jobs (cleanup 120s, leftovers 90s, startup…) contending with winget
4. Winget query lock + 90s communicate timeout vs 60s catalog budget → retries waste

## P1
5. Budget vs timeout mismatch + retry
6. Post-install `resume_catalog_after_install(force=True)` forces two winget scans
7. `get_installed` waits up to 10s on ARP on the bridge
8. Legacy `scan_updates` blocks JS up to 60s
9. Sequential upgrade loop
10. Install/uninstall long `event.wait` on bridge

## P2
11. 500ms poll while scanning
12. Explore gated on catalog-installed behind updates queue
13–17. Smaller: ARP plan_uninstall cost, duplicate hardware, icon wait 180s, pause spin

## First worker slices (approved direction)
- Do not re-queue catalog when already `ready`+cached unless TTL/user refresh
- Bootstrap: start catalog + ARP only; defer cleanup/leftovers/startup to tab focus
- `get_installed`: return orchestrator cache; no `wait_for_job` on bridge
