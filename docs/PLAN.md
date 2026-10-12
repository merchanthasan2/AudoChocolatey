# Quantum Updater v3: Master Plan (live tracker)

> **This is the project memory.** Update this file in the same commit as each task. Tick `[x]` when a task's verification passes, add the commit SHA, and note anything that changed from the spec under "Course corrections".
>
> **Spec:** [superpowers/specs/2026-10-12-quantum-updater-v3-redesign-design.md](superpowers/specs/2026-10-12-quantum-updater-v3-redesign-design.md)
> **Detailed phase plans:** `docs/superpowers/plans/`. Each phase's plan is written just before the phase starts, because it depends on code from earlier phases.

**Status:** P0 in progress · **Last updated:** 2026-10-12

| Phase | Name | Status | Detailed plan |
|---|---|---|---|
| P0 | Cleanup and foundation | In progress | [p0-foundation](superpowers/plans/2026-10-12-p0-foundation.md) |
| P1 | Rust core and sources | Not started | (to be written) |
| P2 | Combined catalog | Not started | (to be written) |
| P3 | Queue and real progress | Not started | (to be written) |
| P4 | New interface (Warm Store + springy motion) | Not started | (to be written) |
| P5 | Real icons | Not started | (to be written) |
| P6 | Background and extras | Not started | (to be written) |
| P7 | Distribution (signing, auto-update, winget) | Not started | (to be written) |
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
- [ ] T7 Docs: ADRs D1–D13, DEVELOPMENT.md, README; release build verification (UAC + SHA shown)

## P1: Rust core and sources
- [ ] `error.rs` (`AppError{code,message}`), `ids.rs` validated newtypes (WingetId, ChocoId, PkgVersion, SourceRef), `versions.rs`
- [ ] `sources/process.rs`: absolute exe resolution, argv only, `CREATE_NO_WINDOW`, Job Object streaming, `\r`/`\n` split, ANSI strip
- [ ] Capture fixtures: winget list/upgrade/search/show (en, de, ja, truncated `…`), choco v1/v2 list/outdated
- [ ] winget table parser (separator-based columns, display-width slicing, locale-agnostic) + `winget export` truncation resolution
- [ ] winget argv builder (no `--force`, no `--all`, `--exact --source winget`), self-exclusion
- [ ] choco source: `list -r` / `outdated -r` with v1/v2 detection, OData search over HTTPS, argv builder
- [ ] `settings/` store: schema v1, atomic writes, migrations, unknown-version read-only
- [ ] ts-rs bindings into `app/src/lib/ipc/bindings` + CI diff check
- [ ] Commands: `get_overview` (full), `get_diagnostics`, `list_installed`, `list_updates`, `search` (unmerged)

## P2: Combined catalog
- [ ] `catalog/normalize.rs` + tests (incl. must-not-merge near misses)
- [ ] `catalog/mappings.json` seeded with about 300 top apps; `catalog.json` (featured, categories, bundles, descriptions, icon sources)
- [ ] `catalog/merge.rs`: mapping, then conservative heuristic, else separate; best source; owner-wins updates
- [ ] Source status + choco-absent mode ("Requires Chocolatey"); verify `Chocolatey.Chocolatey` winget ID; bootstrap job
- [ ] Commands: `marketplace_home`, `marketplace_category`, merged `search` + `search://partial`, `get_app`

## P3: Queue and real progress
- [ ] `jobs/model.rs` state machine + tests; `jobs/worker.rs` single FIFO worker
- [ ] `Global\_MSIExecute` wait; Job Object cancel; installing not cancellable
- [ ] `jobs/persist.rs` atomic `jobs.json`, history 200, startup recovery
- [ ] Progress parsers (winget size/percent, choco percent) with fixtures; 10 Hz throttle
- [ ] Exit-code table verified against winget docs; reboot-required flag; post-success verification
- [ ] `rules.rs` skip-version / never (auto-expiry); Hidden list
- [ ] Commands + events: `enqueue`, `update_all`, `install_bundle`, `get_queue`, `cancel_job`, `retry_job`, `pause/resume_queue`, `clear_finished`, `get_job_log`, `list_rules/set_rule/remove_rule`

## P4: New interface
- [ ] Tokens: light + dark Warm Store, WCAG AA tuned; bundled Inter / Inter Tight; Lucide glyphs
- [ ] Motion presets (spring ~400 ms soft overshoot, instant hover/press, reduced-motion fades)
- [ ] Shell: sidebar, Ctrl+K, Queue pill + drawer, toasts, dialogs, router
- [ ] Components: AppIcon/Monogram, AppCard, AppRow, JobButton (Get → ring → spin → tick → Open), ProgressRing, Skeleton, UpdateAllBar (counter roll), EmptyState
- [ ] Views: Marketplace (featured, categories, bundles, search), AppDetail, Updates (+ Hidden), Installed, Settings, Onboarding
- [ ] vitest for stores and the job→button mapping; visual check against the approved mockups

## P5: Real icons
- [ ] `icon` URI scheme + memory LRU + disk cache with negative cache
- [ ] Windows extraction (registry Uninstall → DisplayIcon → SHDefExtractIconW → PNG)
- [ ] Remote fetch (choco IconUrl, apple-touch-icon, favicon) with the safety rules from spec §5.6
- [ ] Bundled curated `.webp` icons with a recorded source per icon

## P6: Background and extras
- [ ] Scheduler (2 min after launch, then interval), tray menu, toasts only on change
- [ ] Scheduled Task autostart (`--background`) + NSIS uninstall hook; single instance; close-to-tray
- [ ] Export / import with preview; bundles install flow
- [ ] Settings page complete; over-the-shoulder elevation warning

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
