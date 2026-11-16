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

### Greenfield Phase 45 and local account service — 15 Nov 2026
- **Seat:** Manager (provisional)
- **Verdict:** Pass for source. Winget list, search, install, upgrade, and uninstall use ArgumentList only. Package ids must match the allow-list. Search text must match a short allow-list and cannot start a flag. Install, upgrade, and uninstall stay behind a confirm checkbox that starts unchecked. Self-update reads the public GitHub release API and does not download or run an installer.
- **Account service:** Listens on 127.0.0.1:8787 only. Passwords are stored as PBKDF2-SHA256 hashes. The desktop client can register, sign in, send a heartbeat, and send install/upgrade/remove results. It cannot open `/quantumupdater/admin`. Admin pages need an owner cookie. Heartbeat stores Windows, CPU, memory, and disk headline only. Reports do not include passwords, recovery keys, or file contents.
- **Follow-up:** Deploying that service to quantumstep.in, and the elevated MSIX workload, stay on the Owner later list.

### WinUI Phase 44.2–44.3 core screens — 15 Nov 2026
- **Seat:** Manager (provisional)
- **Verdict:** Pass for Phase A core source so far. Services use ArgumentList only; package-id allow-list; uninstall/install/upgrade gated by UI confirm checkbox (default off). Self-update CheckAsync is HTTPS GitHub API read-only; Apply stubbed until packaging cutover. Manifest requireAdministrator restored.
- **Scope:** `src/QuantumUpdater/Services/*`, `Views/*`, `MainWindow.xaml.cs`, `app.manifest`, ADR 042.
- **Middleware:** AppServices facade + Winget/Installed/Marketplace/Update services.
- **Follow-up:** Owner elevated on-PC smoke; 44.4 zip; Security seat when available.

### WinUI Phase 1 shell + winget 2.0.3 refresh — 15 Nov 2026
- **Seat:** Manager (provisional)
- **Verdict:** Pass for Phase 1 shell source. Unpackaged self-contained WinUI EXE launches with a real window (code-only UI; no PRI). No secrets; Windows App SDK from nuget.org. Crash log path under `%LOCALAPPDATA%\QuantumUpdater\winui-crash.log`. Python ship path remains 2.0.3 · Build 10 zip on Release `v2.0.3`. Winget PR #480804: CLA done; URLs on GitHub; schema headers required by CI.
- **Scope:** `src/QuantumUpdater/*`, `manifests/w/QuantumStep/QuantumUpdater/2.0.3/*`, release asset `QuantumUpdater-Windows.zip`, ADR 040/041.
- **43.4 addendum:** `WingetService` uses `ProcessStartInfo` ArgumentList only (no shell). Package ids filtered by allow-list regex. Upgrade UI gated by confirm checkbox (default off).
- **Middleware:** C# `WingetService` is the start of the planned single service layer. Python `web_api.py` remains the live ship bridge.
- **Follow-up:** Owner on-PC accept WinUI Phase 1; wait for winget-pkgs merge; install UWP MSBuild before XAML/PRI Phase 2.

### Zip self-update (ADR 039) — 15 Nov 2026
- **Seat:** Manager (provisional)
- **Verdict:** Pass for source. Asset pick prefers official zip; apply only when frozen; unpack then robocopy over install dir via detached cmd; official URL checks unchanged.
- **Scope:** `core/updater.py`, `core/test_updater_windows_zip.py`, `docs/ARCHITECTURE.md`.
- **Middleware:** No new layer.

### Release packaging + upgrade timeout (ADR 037 / 038) — 15 Nov 2026
- **Seat:** Manager (Security architect seat unavailable; provisional)
- **Verdict:** Pass for source. Publishing a lone onedir bootloader was a launch defect (Python runtime missing). Fixed by shipping full zip. Streaming winget now times out and kills the process tree so a stuck upgrade cannot hang the UI forever.
- **Scope:** `.github/workflows/release.yml`, `main.py` startup crash log, `core/winget_engine.py` stream timeout, `core/test_winget_stream_timeout.py`, tags `v2.0.1` / `v2.0.2`.
- **Middleware:** No new layer.
- **Follow-up:** Owner opens Build 8 / unzipped `QuantumUpdater-Windows.zip` on this PC and a second PC.

### Wave 4/5 remaining engine tools (ADR 036) — 15 Nov 2026
- **Seat:** Manager (provisional; Security architect pass pending)
- **Verdict:** Pass for source pending formal seat. Confirm gates on TCP/IP reset, firewall reset, format removable, compression, mount/eject, and context-menu disable/restore. Service denylist blocks protected names. Registry import blocked unless `allow_import`. System drive format refused.
- **Scope:** `core/system_tools/*` wave-4/5 modules, `core/test_wave45_remaining.py`, `web_api.py`, Settings cards in `web/index.html` + `web/app.js`, ADR 036, tracker 33.x/34.x waiting rows.
- **Middleware:** No new layer.
- **Follow-up (15 Nov):** Manager wired Settings start/stop/restart service and enable/disable optional feature buttons (extra-field name + confirm) after [Wave 4/5 worker](da000787-67db-4048-a643-9b5a542d9293) noted list-only UI.

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

## Wave 1 batch review — Phase 46 (16 Nov 2026)

**Scope:** `git diff dcaa51c..9ff3f8a -- src tests`. Reviewer: Security & Code Architect. **Verdict: PASS WITH FIXES.** No Critical or High finding.

| Earlier finding | Status |
|---|---|
| F3 fake winget on PATH | Closed |
| F4 fuzzy id match | Closed for current callers |
| F16 kill on cancel | Partly (W3, W7) |
| F18 one action at a time | Open (W2): ActionQueue not used by the pages |
| F22 crash log | Partly (W1) |
| F24 copied process code | Closed |

| ID | Sev | Problem | Fix | When |
|---|---|---|---|---|
| W1 | Medium | Crash log writes exception messages (user paths), no size cap, admin app appends to a user-writable folder (junction risk) | Type, HResult, stack only; strip profile paths; 1 MB cap; refuse reparse points | Before next release build |
| W2 | Medium | Several installs/uninstalls can run at once; shared LastError | Route every action through one ActionQueue; per-result errors | Before owner acceptance |
| W3 | Medium | Kill can throw Win32Exception/AggregateException; winget may keep running | Catch, report Cancelled/TimedOut with a note | Before next release build |
| W4 | Medium | ScanOrchestrator starts jobs on the caller thread; sync work blocks parallelism and the limit | Task.Run each job; blocking-job test | Before the scan screen |
| W5 | Low | Locator caches a path that App Installer updates can delete | Re-check File.Exists, re-locate once | Next batch |
| W6 | Low | Highest version chosen before the trust check | Filter trusted + Store/System signature first | Next batch |
| W7 | Low | stderr dropped from error messages | ErrorLines in RunResult | Next batch |
| W8 | Low | Runner is a blocklist; action verbs accept positional/query flags | Allowlist: exactly one --id, add --source winget | Next batch |
| W9 | Low | Abandoned reader tasks raise unobserved exceptions | Observe faults | Next batch |
| W10 | Low | HKCU DisplayName can fake a header line; pin footers read as rows | Header only with dash separator; footer patterns; Version required | Next batch |
| W11 | Low | No row caps + StackPanel: large search freezes the UI | Virtual list (46.7.6) or --count 200 | With Wave 3 |
| W12 | Low | Same id twice (two versions) fails uninstall | Pass --version or group rows | With 46.5.15 |
| W13 | Low | Events raised inside locks; live object passed; _post throw stops queue | Post after unlock; snapshot; try/catch | Next batch |
| W14 | Low | Tests needed an ignored file; FakeWinget copied before build | Fixture moved to Data/ (fixed 16 Nov); after-build copy | Partly fixed |
| W15 | Low | HKCU uninstall strings are user-writable; running them as admin is privilege escalation | Never run HKCU strings elevated; HKLM program must be under Program Files/System32 | Before 46.5.18/19 |
| W16 | Low | Result-mapping code copied six times; duplicate result types; empty if; HKCU read twice | One helper and type; delete dead code; read HKCU once | Next batch |
| W17 | Low (privacy) | Test fixture was the owner's real installed-app list | Replaced with synthetic rows on 16 Nov; old copy remains in public git history (owner decision) | Fixed in tree |

Middleware: none new. WingetRunner is the gate between the admin app and winget; make it an allowlist (W8). A second gate is needed before the own-uninstaller route (W15).

### Re-check of commit fb479f6 (16 Nov 2026): PASS WITH FIXES

Scope: W1, W3 and W4 fixes, and 46.5.13 WingetReader (COM). Manager result: build 0 warnings, 202 of 202 tests pass.

| Id | Status |
|---|---|
| W1 crash log | Closed (one Low item: W19) |
| W3 kill errors | Partly: no caller reads StopIncomplete (W18) |
| W4 background jobs | Closed (Low follow-ups: W25, W26) |

| ID | Sev | Problem | Fix | When |
|---|---|---|---|---|
| W18 | Medium | No service reads RunResult.StopIncomplete; the user sees "Cancelled" while winget can still run | Clear message; block new actions until the process ends; one test per service | Before next release build |
| W19 | Low | CrashLog check-then-write race; hard links; moved known folder | Open once, check final path under the profile, refuse link count over 1 | Next batch |
| W20 | Medium | WingetReader timeout stops only the wait; sync COM call keeps a thread busy; no gate for parallel COM calls | Use ConnectAsync/FindPackagesAsync with the token; one COM call at a time; new search replaces old; search debounce | Before next release build |
| W21 | Low | Cached catalog kept after a failure, and grows old | Clear cache on failure or timeout; reconnect after 30 minutes | Next batch |
| W22 | Low | Dispose frees the gate while a connect runs | Do not dispose the SemaphoreSlim, or cancel a private token | Next batch |
| W23 | Low | Search results have no publisher | Fall back to DefaultInstallVersion.Publisher | Next batch |
| W24 | Low | Homepage, icon URL and description come from public manifests | https only; check icon Sha256, size and type; plain text with a length cap | Rule for 46.7 screens |
| W25 | Low | Extra Task.Delay in ScanOrchestrator repeats CancelAfter; timer stays 15 s | Remove it, or tie it to a token cancelled at job end | Next batch |
| W26 | Low | Abandoned job or COM task faults are not observed; retry can start a second copy | Observe faults with one "abandoned job" log line; no retry while the old run runs | Next batch (with W9) |
| W27 | Low | Search rule copied from MarketplaceService; ASCII only | One shared rule; owner decides on non-ASCII search | Next batch (with W16) |
| W28 | Low | No packages.lock.json; COM DLL in a user-writable app folder could be replaced | RestorePackagesWithLockFile; install under Program Files only; test on Windows 10 1809 | Before the installer |
| W29 | Low (privacy) | Shipped PDBs put the build folder path in crash stacks | PathMap or ContinuousIntegrationBuild for release builds | Before next release build |

Middleware: not needed. DeskVerve has no backend server of its own. The needed protection is in the code: W20 (call gate and cancel route) and W24 (URL and icon checks).
