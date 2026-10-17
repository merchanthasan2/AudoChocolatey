# Quantum Updater v3: Master Plan (live tracker)

> **This is the project memory.** Update this file in the same commit as each task. Tick `[x]` when a task's verification passes, add the commit SHA, and note anything that changed from the spec under "Course corrections".
>
> **Spec:** [superpowers/specs/2026-10-12-quantum-updater-v3-redesign-design.md](superpowers/specs/2026-10-12-quantum-updater-v3-redesign-design.md)
> **Detailed phase plans:** `docs/superpowers/plans/`. Each phase's plan is written just before the phase starts, because it depends on code from earlier phases.

**Status:** P0–P5 done, including P3 extras + sources · P6a nearly done (live check pending) · next: P6b–e → P7 → P8 · **Last updated:** 2026-10-17

| Phase | Name | Status | Detailed plan |
|---|---|---|---|
| P0 | Cleanup and foundation | Done (merged to main; UAC + build SHA confirmed by owner) | [p0-foundation](superpowers/plans/2026-10-12-p0-foundation.md) |
| P1 | Rust core and sources | Done (branch v3/p1-core) | [p1-rust-core](superpowers/plans/2026-10-12-p1-rust-core.md) |
| P2 | Combined catalog | Done (branch v3/p2-catalog) | [p2-catalog](superpowers/plans/2026-10-13-p2-catalog.md) |
| P3 | Queue and real progress | Done (core: v3/p3-core; extras + sources: v3/p3x-sources) | [p3-core-queue](superpowers/plans/2026-10-12-p3-core-queue.md), [p3x-sources](superpowers/plans/2026-10-13-p3x-sources.md) |
| P4 | New interface (Warm Store + springy motion) | Done (branch v3/p4-ui) | [p4-interface](superpowers/plans/2026-10-12-p4-interface.md) |
| P5 | Real icons | Done, pulled forward (branch v3/p4-ui) | [p5-icons](superpowers/plans/2026-10-12-p5-icons.md) |
| P6 | Background (a), Control (b), Safety (c), Lists (d), Everyday polish (e) | In progress (branch v3/p6); d and e added 2026-10-17 | [p6](superpowers/plans/2026-10-13-p6.md) |
| P7 | Distribution (signing, auto-update, winget) | Not started | (to be written) |
| PDev | Developer pack (Pip, Npm, PowerShell Gallery, Cargo), off by default | Approved by owner 2026-10-17; after P7 (ADR 029) | (to be written) |
| P7b | Payment and licensing | Not started; questions for the owner first | (to be written) |
| PD | Drivers (signed Windows Update drivers, maker tools, driver list) | **Approved by owner 2026-10-13**; build after payment and licensing | [drivers](superpowers/plans/2026-10-13-drivers.md) |
| P8 | Docs and hardening | Not started | (to be written) |

---

## P0: Cleanup and foundation
Goal: one runtime, a fresh Svelte shell, admin manifest really embedded, locked-down security config, and a visible build SHA so stale builds are obvious.
- [x] T1 Archive old planning docs; delete Python runtime, `web/`, launchers, `manifests/`; fix `.gitignore`
- [x] T2 Root `package.json` as single version source, plus version scripts (bump/check) with tests
- [x] T3 Frontend policy checker (no emoji, no `{@html}`) with tests
- [x] T4 Scaffold `app/` (Svelte 5 + Vite + TS, bundled fonts, Warm Store placeholder screen)
- [x] T5 Rust shell: `build.rs` manifest embed (release admin / debug asInvoker), `BUILD_SHA`, command manifest, capabilities, CSP, `get_overview`
- [x] T6 CI and release workflow cleanup (no Python, builds frontend, fmt/clippy/test, policy checks)
- [x] T7 Docs: ADRs D1–D13, DEVELOPMENT.md, README; release build verification (UAC + SHA shown)

## P1: Rust core and sources
- [x] `error.rs` (`AppError{code,message}`), `ids.rs` validated newtypes (WingetId, ChocoId, PkgVersion, SourceRef), `versions.rs`
- [x] `sources/process.rs`: absolute exe resolution, argv only, `CREATE_NO_WINDOW`, timeouts, ANSI strip (Job Object streaming moved to P3)
- [x] Capture fixtures: winget list/upgrade/search/show (en, de, ja, truncated `…`), choco v1/v2 list/outdated
- [x] winget table parser (separator-based columns, display-width slicing, locale-agnostic) + `winget export` truncation resolution
- [x] winget argv builder (no `--force`, no `--all`, `--exact --source winget`), self-exclusion
- [x] choco source: `list -r` / `outdated -r` with v1/v2 detection, OData search over HTTPS, argv builder
- [x] `settings/` store: schema v1, atomic writes, migrations, unknown-version read-only
- [x] ts-rs bindings into `app/src/lib/ipc/bindings` + CI diff check
- [x] Commands: `get_overview` (full), `get_diagnostics`, `list_installed`, `list_updates`, `search` (unmerged)

## P2: Combined catalog
- [x] `catalog/merge.rs` normalize + tests (incl. must-not-merge near misses, FirefoxESR vs Firefox MSIX)
- [x] `catalog/apps.json`: 96 curated apps in 10 categories plus 5 bundles, every id validated live (`scripts/validate-catalog.mjs`); ADR 023
- [x] Merge: mapping, then strict heuristic, else separate; best source; Chocolatey owns updates of apps it installed
- [x] Proper names: catalog name, then Chocolatey package title, then the source name without its version tail
- [x] "Requires Chocolatey" badge when Chocolatey is off; Chocolatey bootstrap job moves to P6 (settings completion)
- [x] Commands: `marketplace_home`, `marketplace_category`, merged `search`, `get_app`; `search://partial` dropped (both sources answer in 1–3 s)
- [x] UI: category chips and pages, featured cards, bundles (install only what's missing), app details panel

## P3: Queue and real progress
- [x] Streaming runner: Job Object (kill the whole process tree), split output on CR and LF (moved from P1)
- [x] `jobs/model.rs` state machine + tests; `jobs/worker.rs` single FIFO worker
- [x] `Global\_MSIExecute` wait; Job Object cancel; installing not cancellable
- [x] *(P3 extras)* `jobs/persist.rs` atomic `jobs.json`, history 200, startup recovery (nothing re-runs by itself); pause/resume; close guard
- [x] Progress parsers (winget phases + download watcher, choco percent) with fixtures; 10 Hz throttle
- [-] *(P3 extras)* Real winget download progress via the Delivery Optimization COM API: dropped for v3 (undocumented cross-process visibility, unsafe COM in an admin process); the ring stays honest-indeterminate
- [x] Exit-code table verified against winget docs; reboot-required flag
- [x] *(P3 extras)* Post-success version verification (a mismatch is a note, never a failure)
- [x] *(P3 extras)* `rules.rs` skip-version / never (auto-expiry); Hidden list
- [x] *(P3 extras)* Plain failure reasons; needs-admin guard and "Allow admin access" (UAC) banner
- [x] *(Sources)* Microsoft Store apps (ADR 024); backup sources (ADR 025); fuller category pages; no tool names on screens (ADR 027); Scoop dropped (ADR 026)
- [x] Commands + events: `enqueue`, `update_all`, `get_queue`, `cancel_job`, `retry_job`, `clear_finished`, `get_job_log`; `job://changed`, `job://progress`, `queue://changed`
- [x] *(P3 extras)* `pause/resume_queue`, `set_rule/remove_rule` (rules come back with `list_updates`); `install_bundle` not needed (bundles use validated `enqueue`)

## P4: New interface
- [x] Tokens: light + dark Warm Store; bundled Inter / Inter Tight; Lucide-geometry glyphs (formal WCAG audit in P8)
- [x] Motion presets (spring ~420 ms for moments, 140 ms view fade, instant press feedback, reduced-motion fades)
- [x] Shell: sidebar (Updates first, owner request), Ctrl+K, activity pill + drawer, toasts, state router
- [x] Components: AppIcon (real icon, monogram fallback), AppRow (live status + progress bar), JobButton (Get → ring → check → Done), ProgressRing, Checkbox, Skeleton, CountRoll, EmptyState, Banner, Segmented, Toggle
- [x] Views: Updates (select + Update selected/all, per-row and batch progress), Marketplace (popular apps + search), Installed (uninstall), Settings
- [x] *(P2)* Marketplace categories and bundles, AppDetail
- [ ] *(P3 extras / P6)* Updates "Hidden", Chocolatey onboarding
- [x] vitest for UI logic (17 tests); screenshot review of every view in light/dark at 1200x780 and 960x640

## P5: Real icons
- [x] `icon` URI scheme + disk cache; 7-day memory of definite misses only (temporary failures retry)
- [x] Windows extraction: registry DisplayIcon, main exe in InstallLocation, MSIX package logo
- [x] Remote fetch: Chocolatey IconUrl, publisher homepage apple-touch-icon/icon/favicon; https, public IPs, size caps, sandboxed SVG
- [ ] *(dropped)* Bundled icons: replaced by live originals (owner: "icons must be actual original")

## P6: Background, control, safety, lists, polish
- [x] P6a 1–3 Launch modes and single instance, window and close-to-tray, tray (`db5833e`, `e368eb7`)
- [x] P6a 4–5 Scheduler and toasts only on change (`d6d3c00`)
- [x] P6a 6 Scheduled Task start at sign-in, first-run card, NSIS uninstall hook (`9a6133f`); live check awaits owner approval
- [x] P6a 7 Notification choices (ready / finished / failed) (`f395925`)
- [ ] P6b Per-app policy, automatic mode with battery, metered, quiet hours and minimum update age, My bundles (add selection, export and import, scheduled backup), remove several, Settings
- [ ] P6c Restore point, history, rollback, signer note, security first, download site changed
- [ ] P6d Lists: toolbar, table, friendly sources and filter, hide Windows components, views, What's new and size and date, CSV, link "Installed directly" apps ([plan](superpowers/plans/2026-10-17-p6d-lists.md), approved 2026-10-17)
- [ ] P6e Polish: desktop shortcut cleanup, close the app first, restart needed, better retry, startup manager, live log, searchable Settings

## P7: Distribution
- [ ] Updater key generation + offline backup procedure; `tauri-plugin-updater` via Rust `updater.rs` + UI
- [ ] `release.yml`: version check, conditional updater artifacts, conditional Azure Trusted Signing `signCommand`, tauri-action draft → publish
- [ ] winget: first manual `komac new`, then `winget-releaser` job (needs `WINGET_TOKEN`)
- [ ] RC rehearsal: v3.0.0-rc.1 → rc.2 auto-update on a fork

## P8: Docs and hardening
- [ ] ARCHITECTURE, SECURITY, DESIGN_SYSTEM, RELEASING, PRD final
- [ ] Network audit (zero external requests from the UI), `{@html}` audit, accessibility pass, 100%/150% DPI, light/dark
- [ ] README + screenshots; v3.0.0 release

---

## Things the owner must provide
- [ ] Code-signing: Azure Trusted Signing account (org eligibility) or an OV certificate (needed by P7)
- [ ] `WINGET_TOKEN` (GitHub PAT with a fork of microsoft/winget-pkgs) (P7)
- [ ] Decide final repo name before the first public v3 release (the updater endpoint depends on it)

## Course corrections log
| Date | Change | Why |
|---|---|---|
| 2026-10-12 | Plan created | Approved spec |
| 2026-10-12 | P0 built on branch `v3/p0-foundation`, not pushed to `main` | Push/merge awaits owner approval |
| 2026-10-12 | `test:scripts` uses the glob `"scripts/*.test.mjs"` | Node 24 `--test` doesn't accept a bare directory |
| 2026-10-12 | create-vite 9 ships Vite 8 / TS 6 and `public/favicon.svg` + `icons.svg` (deleted) | Template changed since plan |
| 2026-10-12 | `#app` added to the full-height CSS rule | Placeholder card rendered top-aligned |
| 2026-10-12 | Live UI verified via WebView2 remote debugging (CDP): 0 console errors under the production CSP, fonts load, IPC OK | Faster and more thorough than right-click Inspect |
| 2026-10-12 | Release exe: `requireAdministrator` embedded (verified in binary); the UAC prompt on double-click is for the owner to confirm | Needs interactive consent |
| 2026-10-12 | Final review fix: `build.rs` reruns on `src/`, `app/src`, `Cargo.toml`, `package.json` edits (`910bb84`) | `-dirty` flag went stale on unstaged edits |
| 2026-10-12 | Production CSP re-verified at runtime: inline scripts and remote images blocked | Reviewer asked for runtime proof |
| 2026-10-12 | Deferred to P7: `release.yml` must run the CI checks before publishing; `bump-version` should refresh lockfile versions | Review minors |
| 2026-10-12 | Deferred to P6: `tray-icon` feature unused until then; trim unused window capabilities if the UI doesn't need them | Review minors |
| 2026-10-12 | Owner ran the release exe: UAC prompt, "Running as admin: Yes", build `14557bcd` = HEAD | Closes the last P0 check |
| 2026-10-12 | Owner chose to keep the phase order (engine P1–P3 before interface P4) | Offered to build the interface first on sample data; declined |
| 2026-10-12 | P1: no truncation when winget output is piped; header gaps can be one space, so columns come from header-token offsets | Observed on owner PC (winget 1.29.380) |
| 2026-10-12 | P1: `winget search --source winget` omits the Source column; parser takes `has_source` | Found by live check, regression test added |
| 2026-10-12 | P1: reqwest uses native-tls (SChannel) + system proxy, not rustls | rustls pulls aws-lc-sys (CMake/NASM); SChannel uses the Windows cert store |
| 2026-10-12 | P1: concrete WingetSource/ChocoSource generic over Runner, no PackageSource trait | Signatures differ; the fake Runner is the test seam |
| 2026-10-12 | P1: Job Object streaming moved to P3 | P1 only runs short read-only queries |
| 2026-10-12 | **Order changed by owner: P3 core, then P4 interface, then P2 / P5–P8** | Owner wants a usable app sooner; P1 already provides real installed/updates/search data. Early builds may list winget and Chocolatey copies of the same app separately until P2 lands |
| 2026-10-12 | P3: live install of 7-Zip through the queue succeeded; the uninstall command was verified elevated | Owner-approved live test; the PC was left as it was |
| 2026-10-12 | P3: winget downloads go through Delivery Optimization, which writes the file in one shot, so the winget ring is indeterminate until the file lands | Measured: 0 bytes, then 93 MB in a single step; ADR 021 |
| 2026-10-12 | P3: persistence/recovery, rules, bundles, pause and post-success verification moved to "P3 extras" after P4 | Owner wants a usable app first |
| 2026-10-12 | P4: owner asked for Updates as the start screen, selectable updates, and visible update animations | Done: Updates first, checkboxes + "Update N selected", per-row progress bars, batch "Updating 2 of 5" bar |
| 2026-10-12 | P5 pulled forward; bundled icons dropped in favour of live originals only | Owner: "icons need to be actual original"; ADR 022 |
| 2026-10-13 | P2: catalog is 96 hand-checked apps instead of about 300 auto-mapped ones | Every id is validated against live winget and Chocolatey; a wrong mapping installs the wrong program as admin |
| 2026-10-13 | P2: Chocolatey by-id lookups use `Packages()?$filter=tolower(Id) eq '…' and IsLatestVersion` | `FindPackagesById` with a filter returns nothing; this also fixed Chocolatey icons |
| 2026-10-13 | P2: the name merge keeps bracketed words and requires the Chocolatey id to name the app | Live search paired Firefox MSIX (winget) with FirefoxESR, whose Chocolatey title is plain "Mozilla Firefox" |
| 2026-10-13 | P2 review (0 Critical, 3 Important fixed): Chocolatey title lookups capped at 3 s with a 10-minute miss cache; bundles wait for the installed list and skip queued apps; "Use Chocolatey" off is enforced by the queue too | Fresh whole-branch review |
| 2026-10-13 | Deferred from P2 review to P8: detail panel dialog focus handling, category stale guard, cancel `winget show` on close, same-name merge tests, retry of Chocolatey jobs while it's off | Review minors |
| 2026-10-13 | Owner: hide tool names, add Microsoft Store + fuller categories + Scoop, backup sources incl. updates | Spec listed Store and Scoop as non-goals; changed by owner. Plan: p3x-sources |
| 2026-10-13 | A live Store test uninstalled the owner's existing Microsoft To Do; reinstalled at once (same version) | Live tests now check the app isn't already installed first |
| 2026-10-13 | Kaspersky flagged and removed an unsigned debug build | Store user check switched to session APIs (no other process opened); signing in P7 is the real fix |
| 2026-10-13 | Scoop dropped by owner | Running it unelevated needs explorer's token (malware-like, antivirus alerts); owner chose to ask for admin via UAC instead (ADR 026) |
| 2026-10-13 | P3 extras review (0 Critical, 3 Important fixed): Update all honours the source switches; closing asks while any job runs (phase text is English-only); restored jobs show the real package and drop pinned versions | Fresh whole-branch review |
| 2026-10-13 | Deferred to P8: restrict app data/config folders to Administrators; skip rules across source changes; NSIS uninstall hand-off check; side-by-side version rows; offline "More apps" cache; minimized close dialog; RowMenu focus; Store refusal wording; invisible rules at the 500 cap | Review minors |
| 2026-10-13 | Owner plans a payment and licensing phase; proposed between P7 (signing, auto-update) and P8 (final hardening) | Licensing needs a signed, self-updating app; P8's security review should include it |
| 2026-10-13 | Icon cache remembers only definite misses | Temporary winget/network failures had hidden icons for 7 days |
| 2026-10-13 | Debug builds use their own WebView2 profile | WebView2 refuses to share a profile between an elevated and a normal instance |
| 2026-10-13 | P4/P5 review: misdirected clicks fixed (likely cause of the unexplained vcredist140 job); icon SSRF, path and DoS hardening | Review findings; tests added for each |
| 2026-10-13 | Residual risk logged: the elevated app parses user-writable icon files (HKCU) in-process | A fix needs an unelevated helper process; planned for P8 hardening |
| 2026-10-17 | `start_at_login` default changed to off, and a settings file without `backgroundPrompted` loads it as off | Plan says off until the person says yes; the P0 default was on, so the toggle showed on with no task |
| 2026-10-17 | Owner compared UniGetUI 2026.3.x (installed on their PC): added P6a 7, minimum update age, My bundles, download site changed, P6d Lists and P6e Everyday polish | Owner picked 8 of 10 ideas plus 4 web-found extras; see COMPETITIVE.md |
| 2026-10-17 | Source column with friendly names (ADR 028); Developer pack incl. Cargo approved for after P7 (ADR 029) | Owner wants version and source per app like UniGetUI, and its full source list |
