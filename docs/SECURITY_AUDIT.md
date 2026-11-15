# Security and defect checks

The Security and Code Architect runs these on every finished slice. A full pass runs before a release or before the owner is asked to try a new build of the Windows app on this PC.

Two skills are already installed and must be used: `vibe-security` (raroque) and `vibe-security-audit` (LadyKerr). The second one scores the project only after its checks are actually read against this code.

| Check | What it looks for | Where | When |
|---|---|---|---|
| 1. Secrets | Keys, tokens, passwords, private certificates | Code, history, and the built app | Every change |
| 2. Dependencies | Known flaws, old libraries, made-up packages | Requirements and the built app | Every change, and daily when a release is open |
| 3. Static flaws | Injection, unsafe loading, weak protection | Source files | Every change |
| 4. Sign-in and permission | A person sees or changes only what they may | Actions the app can take on this PC | Before every release |
| 5. Input and output | What is sent in is checked, and the screen cannot run it as code | Screens and the bridge from the screen to the PC | Every change |
| 6. Live app | Broken headers, who may call it, hidden admin pages | The test or live address | Weekly, and after a publish |
| 7. Hosting | Open storage, open ports, certificates | The host and the domain | Weekly |
| 8. Code quality | Dead code, repeated code, swallowed errors | Source | Every change |
| 9. Limits and cost | Unlimited retries and runaway bills | Any online call | Monthly, and before launch |
| 10. Logs and backups | Problems recorded without private data, and a backup that restores | Logs and saved copies | Before every release |

## Results

### Slice 31.3 / 31.4 / 31.11 Cleanup repair trio — 15 Nov 2026
- **Seat:** Manager (provisional; Security architect pass pending)
- **Verdict:** Pass for source pending formal seat. Duplicate recycle requires latest scan + confirm and keeps one file per hash group. Orphan remove requires confirm + registry backup before delete. Schedule allow-lists three category ids and daily|weekly; task command is fixed argv with `--scheduled-cleanup --no-elevate` only.
- **Scope:** `core/system_tools/duplicates.py`, `orphan_install.py`, `scheduled_cleanup.py`, tests, `main.py` flag path, `web_api.py` bridge, Settings cards in `web/index.html` + `web/app.js`, ADR 034.
- **Middleware:** No new layer.

### Slice 31.2 / 31.5 / 31.6 Catalog export and app repair — 15 Nov 2026
- **Seat:** Manager (Security architect seat hit Other Models limit; Manager check recorded)
- **Verdict:** Pass for source. Unit tests green for export/import and repair/reset. Do not treat as owner-tested until a Build is opened on this PC.
- **Scope:** `core/system_tools/catalog_export.py`, `core/system_tools/app_repair.py`, `core/test_catalog_export.py`, `core/test_app_repair.py`, `web_api.py` bridge methods, Settings cards in `web/index.html` + `web/app.js`, ADR 033.
- **Checks:** (1) No secrets. (2) No new deps. (3) Fixed winget argv; catalog paths limited to profile/Downloads `.json`; package ids allow-listed; reset only after live AppX list match. (4) Import and reset require `confirmed=true` plus double UI confirms. (5) Status text is plain text. (8) Mocked subprocess tests. (9–10) N/A. Checks 6–7 N/A.
- **Middleware:** No new layer.

### Slice 42.5 Early self-update check — 15 Nov 2026
- **Seat:** Manager (Security architect seat hit Other Models limit; Manager check recorded)
- **Verdict:** Pass for source. Still uses ADR 026 official asset rules. Preload only calls `check_for_updates`. Apply path unchanged (caller URL ignored). Failed/offline check does not block the app.
- **Scope:** `main.py` preload join, `web_api.py` preload/get_startup_update/check cache, ready-gate self-update row, `web/app.js` bootstrap-after-check, ADR 032.
- **Middleware:** No new layer.

### Slice 31.10 Print queue / spooler — 15 Nov 2026
- **Seat:** Manager (Security architect seat hit Other Models limit; Manager check recorded)
- **Verdict:** Pass for source. Cancel and machine-wide clear both require `confirmed`. Job cancel re-checks the live list. Spool folder path is fixed under `%SystemRoot%`.
- **Scope:** `core/system_tools/print_spooler.py`, `core/test_print_spooler.py`, `web_api.py` print_* methods, Settings Print queue card, ADR 031.
- **Middleware:** No new layer.

### Slice 31.8 Store app re-register — 15 Nov 2026
- **Seat:** Manager (Security architect seat hit Other Models limit; Manager check recorded)
- **Verdict:** Pass for source. Unit tests cover confirm gate, bad id, and listed-manifest path. Do not treat as owner-tested until a Build is opened on this PC.
- **Scope:** `core/system_tools/store_apps.py`, `core/test_store_apps.py`, `web_api.py` `store_apps_list`/`store_apps_reregister`, Settings card in `web/index.html` + `web/app.js`, ADR 030.
- **Checks:** (1) No secrets. (2) No new deps. (3) Package full name allow-list; manifest path from `Get-AppxPackage` list only; PowerShell register uses escaped path. (4) Requires `confirmed=true` + UI confirm. (5) Select options built with `textContent`. (8) Tests present. Checks 6–7 N/A.
- **Middleware:** No new layer.

### Slice 31.1 Catalog sources — 15 Nov 2026
- **Seat:** Manager (Security architect seat hit Other Models limit; Manager check recorded)
- **Verdict:** Pass for source. Unit tests green. Do not treat as owner-tested until a Build is opened on this PC.
- **Scope:** `core/winget_engine.py` `refresh_sources`/`reset_sources`, `web_api.py` bridge methods, Settings card in `web/index.html` + `web/app.js`, `core/test_winget_sources.py`, ADR 029.
- **Checks:** (1) No secrets. (2) No new deps. (3) Fixed argv only — no shell interpolation. (4) Reset requires `confirmed=true` plus two UI confirms; refresh is everyday. (5) Status text is plain text, not HTML. (8) Tests cover ok / missing winget / confirm gate. (9–10) N/A. Checks 6–7 N/A.
- **Middleware:** No new layer. Bridge stays the only path that starts winget source repair.

### DisplayLink USB devices (phase 40) — 11 Nov 2026
- **Seat:** Security architect on Grok 4.7 (`68945f3d-a951-473c-8323-261353355ee6`)
- **Verdict:** Pass. A new program file may be built for this slice.
- **Scope:** `core/hardware_facts.py`, `core/test_hardware_facts.py`, About USB rows in `web/app.js`, ADR 019.
- **Checks 1–5, 8–10:** Pass for this slice. Checks 6 and 7 do not apply. No project score. A full 30-point audit was not run.
- **Low notes:** page merge by name only, and that merge duplicated in `web/app.js` — fixed after the review (shared `aboutCollectUsbDevices` / `merge_key`). Graphics and USB still each walk the same registry key once; that is repeated work, not a security risk.

### Phase 36 steps 1–2 (saved update list + catalog wait) — 11 Nov 2026
- **Seat:** Security architect on Grok 4.7 (`e5e95fe7-e63b-4561-bda5-86781ba2c9ee`)
- **Verdict:** Pass with fixes. Next workers may continue. Do not hand over a new program file until the medium screen bug is fixed.
- **Medium (fixed after review):** `refreshUpdatesFromStore` cleared scanning and set Sync complete at 100% while a new check was still queued. It now keeps scanning state until the job is ready.
- **Low (fixed after review):** ready cached note no longer says “refreshing”; scanning placeholder only when `this.updates` is empty, and the false “previous results stay visible” sentence is removed.
- No project score. Checks 6 and 7 do not apply.

### Phase 36 mid-slice (Run installer, Marketplace, beta, prefetch) — 11 Nov 2026
- **Seat:** Security architect on Grok 4.7 (`5374078e-db5b-4d6f-9546-d60cb50c9a41`)
- **Verdict:** Pass with fixes. Do not hand over a new program file until the medium Run installer finding is fixed.
- **Medium (fixed after review):** `launch_downloaded_installer` now uses only the top download folder. Exactly one allowed installer may start; more than one is refused with the file names. The toast names the file that started.
- **Low (fixed after review):** Publisher page uses a `data-url` attribute and a button handler. Upgrade prefetch marking runs before install download slots fill.
- DisplayLink pass still stands. No project score. Checks 6 and 7 do not apply.

### Slice 36.8 / 36.9 (real progress, Installed layout, uninstall progress) — 12 Nov 2026
- **Seat:** Security architect (`f5516db2-8a2a-4fcc-aaec-3041409c1eb0`)
- **Verdict:** Pass (after re-check). Build 5 may be packaged.
- **Scope:** `web/app.js`, `web/style.css`, `web/index.html`, `web_api.py`, `core/manager.py`, `core/winget_engine.py`, `core/choco_engine.py`, `core/base_engine.py`, `core/system_tools/large_files.py`, `core/system_tools/leftovers.py`, `web_api_tools.py`, `core/scan_orchestrator.py`, ADR 024.
- **Checks 1–5, 8–10:** Pass for this slice after the three mediums were fixed. Checks 6 and 7 do not apply. No project score. A full 30-point audit was not run.
- **Middleware:** No new layer. `web_api.py` stays the only path that starts a program, a scan, or a delete.
- **Medium (fixed):** `setIslandProgress` updates ticker only when percent is null/non-finite; fill and Working state stay.
- **Medium (fixed):** Catalog `uninstall_package` returns `{success, message}` from `OperationStatus` (timeout → false). Screen and queue clear `installedSet` only when `success === true`.
- **Medium (fixed):** `leftovers_scan` / `_orphan_install_locations` pass `percent=None` with `current_item` only; job bridge omits percent kwarg when null.
- **Low (not blocking):** Duplicate `.package-main-row` CSS; null progress skips 0.06s throttle; empty `_tick_progress` still called.
- **Re-check** (`c0403185-0de4-45ab-82e3-0612f7af5ab0`, 12 Nov 2026): Pass. A new program file (Build 5) may be built. Null or non-finite progress updates the ticker only and leaves the fill and Working state. Catalog uninstall returns success and message from the operation status; a timeout is a failure. The Installed screen and the queue remove an app only when success is true. Leftover scan progress sends no percent, and the job bridge omits percent when it is null. No new protective middle layer. The earlier low notes stay non-blocking.

### ADR 026 release channel (self-update → AudoChocolatey) — 12 Nov 2026
- **Seat:** Manager (Security architect seat hit Other Models limit; Manager check recorded)
- **Verdict:** Pass for source wiring. Do not treat a tagged release as owner-tested until a Build is downloaded on a second PC.
- **Scope:** `core/updater.py`, `core/test_arp_plan.py` UpdateUrlTests, `.github/workflows/release.yml`, ADR 026.
- **Checks:** (1) No secrets. (2) No new deps. (3) `is_official_asset` still requires https + this repo path + `.exe`/`.zip`/`.dmg`; caller URL ignored in `apply_self_update`. (4) Portable apply only when `sys.frozen`; setup path still `/S`. (5) Swap script uses absolute paths with quotes stripped from path strings. (8) Tests updated. (9) GitHub free tier. (10) N/A. Checks 6–7 N/A.
- **Middleware:** No new layer. Download still only through `AppUpdater` after an official URL check.
- **Note:** A public or reachable Release is required before another PC can download. Private repo Actions minutes still apply.

No full scored review of the whole app has been run under this list yet.
