# Quantum Updater v3 — Redesign Spec

**Date:** 2026-10-12 · **Status:** Draft for review · **Owner:** Quantum Step
**Supersedes:** v2.0 architecture (Tauri + Python dual runtime, vanilla JS UI)

---

## 1. Why this redesign

| Problem in v2 | Effect on the user |
|---|---|
| `.exe` built before the last UI commit; Tauri embeds the frontend at build time | "I see nothing different" |
| Three UIs (Tauri/`web/`, Python/pywebview, CustomTkinter `ui/`) | Changes drift; double work |
| Chocolatey and winget are an either/or "active engine" | Only one catalog is ever used |
| `save_setting` / `set_engine` called from JS but never registered in Rust | Settings silently never save |
| `requireAdministrator` manifest not actually embedded (`build.rs` is bare) | Needs `.bat`/`.vbs` elevation launchers |
| Fake "Quantum Security Guard: SHA-256 & Authenticode" log lines | Claims checks that never happen — trust problem |
| `csp: null`, Google Fonts + simpleicons loaded at runtime, app runs as admin | Any injected script runs with admin rights |
| `winget upgrade --force` on every update | Can downgrade / reinstall unexpectedly |
| `choco list --local-only` (removed in Chocolatey v2) | Installed list breaks on modern choco |
| Byte-index winget parser, English header matching, no `…` handling | Panics on non-ASCII names; broken on non-English Windows |
| Monochrome Simple Icons CDN | Black/white or missing icons |
| UI reads as AI-generated (glassmorphism, generic gradients) | Not credible as a professional product |

## 2. Goals and non-goals

**Goals**
1. A Windows app that feels hand-crafted and modern: the "Warm Store" visual language with springy, responsive motion.
2. One combined catalog of winget and Chocolatey; the user never picks an "engine".
3. Real, honest progress for search, download, install, update, and update-all.
4. Marketplace (renamed from Explore): curated, offline-capable home and live search, with full-colour icons.
5. Keeps the PC up to date in the background: tray, scheduled checks, notifications, skip/ignore rules.
6. Safe even though it always runs as admin.
7. Professional distribution: signed NSIS installer, signed self-updates, listed on winget.
8. `docs/` is the project memory and is kept current with every phase.

**Non-goals (v3.0)**
- Microsoft Store (`msstore`) source, Scoop, or other managers.
- A winget COM API backend (CLI parsing for now; revisit later).
- macOS/Linux.
- Accounts, cloud sync, or telemetry.

## 3. Decisions (user-confirmed)

| # | Decision | Choice |
|---|---|---|
| D1 | Runtime | **Tauri v2 + Rust only.** Python, `web/`, `ui/`, launchers removed |
| D2 | Package sources | **Combined:** parallel search, dedupe, best source per app, merged updates |
| D3 | Elevation | **Always admin** (`requireAdministrator`), mitigated by hardening (§8) |
| D4 | Distribution | NSIS installer, auto-update, code signing, winget listing |
| D5 | Extra features | Tray + background checks, ignore/pin, export/import, Marketplace bundles |
| D6 | Marketplace data | Bundled curated catalog + live search |
| D7 | Visual direction | **C · Warm Store** (light + dark) |
| D8 | Motion | **Springy & lively** (~400 ms, soft overshoot); instant hover/press feedback |
| D9 | Frontend | **Svelte 5 + Vite + TypeScript** |
| D10 | Icons | Layered + cached: Windows extraction, then bundled curated icons, then site icon, then monogram |
| D11 | Licence | **Proprietary** (free to use; all rights reserved) |
| D12 | Autostart | **Elevated Windows Scheduled Task** at logon (`--background`) |
| D13 | Old planning files | Move `progress.md`, `findings.md`, `task_plan.md`, `HANDOVER.md` to `docs/archive/` |

## 4. Product experience

### 4.1 Navigation
Left sidebar: **Marketplace · Updates (badge) · Installed · Settings**, plus a persistent **Queue** pill at the bottom. The pill shows the active job and expands into a drawer. Ctrl+K focuses search from anywhere.

### 4.2 Screens
- **Marketplace:** featured hero row; category chips (Browsers, Developer, Media, Communication, Utilities, Gaming, AI, Security, Productivity); **Bundles** (e.g. "Developer setup", "Gaming PC", "Creator kit"); live search across both sources. Results show source badges and "Requires Chocolatey" when choco is missing.
- **App detail:** large icon, name, publisher, description (plain text), version, sources available, homepage link (policy-checked), Get/Update/Uninstall button.
- **Updates:** headline "N updates ready" and **Update all**. Merged list with from → to versions and source. Per-row Update, Skip this version, Never update. A collapsible **Hidden (n)** section.
- **Installed:** searchable list of everything installed, with real icons, owner source, and Uninstall.
- **Settings:** Appearance (theme, motion), Background (check interval, notifications, start at login, start in tray, close to tray), Sources (winget/choco status, choco setup), Your apps (export/import), Updates (self-update mode), About (version + **build SHA**, licence, diagnostics).
- **Onboarding card** (once): offers Chocolatey setup if it's missing.

### 4.3 Visual language: "Warm Store"
- **Light:** warm off-white canvas `#F6F3EE`, white cards with a hairline warm border, ink `#1D1B18`, muted `#7A7368`, accent deep green `#1F5C45`, neutral pills `#EFE9DF`.
- **Dark:** warm charcoal canvas `#1A1816` (not pure black), card `#24211E`, ink `#F1ECE4`, muted `#A39B8F`, accent `#5FB38E`, pills `#2E2A26`. These are the starting values; they're tuned against WCAG AA contrast in P4 and recorded in `docs/DESIGN_SYSTEM.md`.
- **Type:** Inter Tight (headings, bold, tight tracking) and Inter (body), bundled locally. Tabular numerals for versions and sizes.
- **Shape:** 12 px cards, 9 px icon radius, pill buttons, generous spacing, soft layered shadows only (no glassmorphism, no neon gradients).
- **Icons:** full-colour app icons at 32/48/96 px. UI glyphs are a single stroke set (Lucide). Zero emoji (existing policy, CI-enforced).
- **Copy:** plain human words ("4 updates ready", "Everything is up to date"), no jargon like "engine".

### 4.4 Motion
- **Personality:** springy, about 400 ms with soft overshoot for anything that *moves*: results arriving (staggered 70 ms), the Get → ring → tick → Open morph, rows completing and collapsing, counter digit roll, page transitions, drawer and dialog entrances.
- **Instant feedback:** hover and press states respond in 120 ms or less, with no bounce.
- **Loading:** shimmer skeletons for search and lists; never a blank screen.
- **Honesty:** motion is driven by real state. Determinate ring only when the real byte or percent progress is known; otherwise an indeterminate arc.
- **Failure:** a gentle shake with a red retry affordance.
- **Accessibility:** Windows "Reduce motion" (or the in-app setting) swaps every spring for a short opacity fade. Nothing waits on an animation to finish.
- **Implementation:** Svelte `Spring`/`Tween` and transitions; CSS `linear()` spring easings; presets live in `app/src/lib/motion/`.

## 5. Architecture

### 5.1 Repository layout
```
/package.json, /package-lock.json   single version source; workspace scripts
/app/                               Svelte 5 + Vite SPA
  src/lib/ipc/{commands,events}.ts  only place that calls invoke/listen
  src/lib/ipc/bindings/             ts-rs generated types (checked in, CI diff-checked)
  src/lib/stores/                   *.svelte.ts: jobs, updates, installed, marketplace, search, settings, theme, motion, toasts
  src/lib/components/               AppIcon, Monogram, AppCard, AppRow, JobButton, ProgressRing, SearchBar,
                                    Skeleton, BundleCard, Sidebar, QueueDrawer, UpdateAllBar, Dialog, Toast, EmptyState
  src/lib/motion/, src/lib/styles/  spring presets; tokens + themes + bundled fonts
  src/views/                        Marketplace, Category, AppDetail, BundleDetail, Updates, Installed, Settings, Onboarding
  public/icons/curated/*.webp       bundled curated icons
/src-tauri/
  build.rs                          manifest embed (requireAdministrator release / asInvoker debug), command manifest, BUILD_SHA
  capabilities/main.json, windows/manifest.xml, nsis/hooks.nsh
  catalog/{catalog.json,mappings.json}
  tests/fixtures/                   captured winget/choco output incl. localized + truncated
  src/ error.rs state.rs ids.rs versions.rs rules.rs transfer.rs updater.rs
       sources/{process.rs, winget/*, choco/*}  catalog/*  jobs/*  icons/*  settings/*  background/*  commands/*
/scripts/                           bump-version, check-versions, check-no-emoji
/docs/                              project memory (§11)
```
**Removed:** `main.py`, `web_api.py`, `core/`, `ui/`, `build_exe.py`, `requirements.txt`, `Run_*.bat/.vbs`, `web/`, `manifests/` (komac generates manifests at release).

### 5.2 Process layer (`sources/process.rs`)
- The only module that spawns processes. It uses an absolute exe path, argv vector, no shell, and `CREATE_NO_WINDOW`.
- Streaming runs assign the child to a **Job Object** (kill-on-close), so cancel kills the whole installer tree.
- Output is split on `\r` and `\n`, ANSI is stripped, and decoding is lossy UTF-8.

### 5.3 Sources (`PackageSource` trait)
Operations: `status`, `search`, `list_installed`, `list_upgrades`, `details`, `build_op` (pure, unit-tested argv), `progress_parser`, `classify_exit`.

- **winget**
  - Column detection from the dashed separator line plus header gaps, not header words, so it works in any Windows language. Slicing is by display width.
  - Truncated IDs (`…`) are resolved via `winget export` (installed/upgrades) or `winget show` (search, lazily).
  - Argv: `--exact --id <id> --source winget --silent --disable-interactivity --accept-package-agreements --accept-source-agreements [--version v]`. **No `--force`, no `--all`.**
  - The app's own ID (`QuantumStep.QuantumUpdater`) is excluded from updates.
- **Chocolatey**
  - Search via the community OData feed over HTTPS (works even when choco isn't installed; returns description, icon URL, project URL).
  - Installed and outdated via `choco list -r` and `choco outdated -r`, with v1/v2 detection.
  - Jobs run `choco install|upgrade <id> -y --no-color`, keeping progress output.
  - **Absent choco:** search still shows choco-only results labelled "Requires Chocolatey"; installed and updates are winget-only.
  - **Bootstrap:** a queued `winget install --id Chocolatey.Chocolatey` (ID to be verified). The fallback is an Authenticode-checked `install.ps1`, run only with explicit consent.

### 5.4 Catalog, merge and dedupe
- `AppKey` = curated key (e.g. `vscode`) or `w:<id>` / `c:<id>`.
- **Match order:**
  1. `mappings.json` (authoritative; seed with about 300 top apps).
  2. Conservative heuristic: normalized names equal **and** (same homepage host or publisher token matches).
  3. Otherwise keep separate. *A duplicate is acceptable; a wrong merge is not.*
- **Install source:** winget if available, else choco.
- **Update ownership:** owner wins. If choco lists the app as installed, choco updates it and the winget ARP row is suppressed; otherwise winget updates it.

### 5.5 Job queue (`jobs/`)
- A single Rust worker with a FIFO queue; commands send messages to it, and events fan out to the UI and tray.
- Before each job, it waits for `Global\_MSIExecute` to be free ("Waiting for another installer…").
- **State machine:**
  ```
  queued → preparing → downloading → installing → verifying → succeeded | failed
  queued|preparing|downloading --cancel--> cancelled      (installing is NOT cancellable)
  preparing --no update / already installed--> skipped
  failed --retry--> new queued job
  startup: downloading|installing → failed(interrupted); queued restored with queue paused
  ```
- Persisted atomically to `jobs.json` (last 200 finished kept as history).
- After success: re-read the installed version, flag `rebootRequired` on 3010/1641, refresh the updates snapshot.
- **Progress parsing (best effort, never invented):**
  - winget download: size `x MB / y MB` or `%`; installing is indeterminate.
  - choco: `Progress: Downloading … NN%`.
  - Throttled to 10 Hz.
- **Exit codes:** a verified table in `exit_codes.rs` (from winget's official return-code docs).

### 5.6 Icons (`icons/`)
- Served through a custom `icon` URI scheme (`http://icon.localhost/<app_key>`). A 404 makes the UI fall back to a monogram (colour from a hash of the name).
- **Lookup order:**
  1. memory LRU;
  2. disk cache (`%LOCALAPPDATA%\…\icons\`, 7-day negative cache);
  3. bundled curated `.webp` (served directly by the frontend);
  4. installed apps: registry Uninstall keys, then `DisplayIcon`, then `SHDefExtractIconW` at 128 px, then PNG;
  5. remote: choco `IconUrl`, homepage `apple-touch-icon` / `rel=icon`, or `/favicon.ico`.
- **Remote fetch rules:** HTTPS only, 5 s timeout, at most 3 redirects, 512 KB cap, private IPs refused, SVG refused, always decoded and re-encoded before caching, at most 4 concurrent fetches.

### 5.7 Settings, rules, transfer
- **Settings:** Rust-owned JSON in `%APPDATA%\in.quantumstep.quantumupdater\settings.json`.
  - Schema-versioned with migrations and atomic writes.
  - Updated through a validated `SettingsPatch`; side effects (scheduled task, scheduler) are applied in Rust.
  - An unknown newer schema loads defaults read-only and never overwrites the file.
- **Rules** (`rules.json`): `skip_version` (auto-expires when a newer version appears) or `never`. Applied when building the updates list; hidden items are still returned for the Hidden section.
- **Export/import:** a `quantum-updater-apps` v1 JSON file (app keys and IDs only, no personal data).
  - Import goes through a native file dialog, a 1 MB cap, ID validation and an **ImportPreview** (installable / already installed / unavailable / invalid). The selected apps are queued as a batch.

### 5.8 Background
- **Scheduler:** checks 2 minutes after launch, then every `check_interval_hours` (default 6). It caches results, updates the tray tooltip, and sends a toast only when the set of updates changes.
- **Tray:** Open, Check now, Update all (n), Pause checks, Quit. Close-to-tray is optional.
- **Autostart (D12):** `schtasks /Create /TN "QuantumStep\QuantumUpdater" /SC ONLOGON /RL HIGHEST /TR "<exe> --background"`, called with argv and no shell. The NSIS uninstall hook removes it.
- **Single instance:** a second launch focuses the existing window.

### 5.9 Self-update
- `tauri-plugin-updater`, called from Rust only.
- Checks at startup and every 24 h. Install is refused while jobs are active.
- Installs via the NSIS installer in passive mode, then the app restarts.
- Mode set in Settings: notify (default) / auto / off.

## 6. IPC contract (summary)
Types are generated by `ts-rs`. Every command returns `Result<T, AppError{code, message}>`.

**Commands:**
- **Overview:** `get_overview`, `get_diagnostics`.
- **Marketplace:** `marketplace_home`, `marketplace_category`, `search(query, requestId)`, `get_app(key)`.
- **Library:** `list_installed(refresh)`, `list_updates(refresh)`.
- **Queue:** `enqueue(ops)`, `update_all`, `install_bundle(bundleId, keys)`, `get_queue`, `cancel_job`, `retry_job`, `pause_queue`, `resume_queue`, `clear_finished`, `get_job_log`.
- **Settings and rules:** `get_settings`, `update_settings(patch)`, `list_rules`, `set_rule`, `remove_rule`.
- **Transfer:** `export_apps`, `import_preview`, `import_apply(keys)`.
- **System:** `choco_bootstrap`, `check_updates_now`, `self_update_check`, `self_update_install`, `open_external(url)`.

**Events:**
- **Jobs:** `job://changed`, `job://progress` (at most 10 Hz), `job://log`.
- **Queue and library:** `queue://changed`, `updates://changed`, `sources://changed`, `search://partial`.
- **Settings and updater:** `settings://changed`, `updater://available`, `updater://progress`.
- **Navigation:** `nav://open`.

**UI bindings:** `JobButton` binds to `Job.phase` and `Job.percent`. `UpdateAllBar` binds to `Batch.done / total`.

## 7. Error handling
- Every failure surfaces as a human sentence plus a "Details" log view (`get_job_log`); there are no silent catches.
- Source missing or broken: a source status chip plus an onboarding or repair action, with the rest of the app still working.
- Network loss: curated Marketplace and cached icons and updates still render; live search shows "You're offline".
- Interrupted jobs are recovered on restart as `failed(interrupted)` with Retry.
- Reboot-required results show a persistent banner, never an automatic restart.

## 8. Security (always admin: any script injection = admin code execution)
- **CSP:** `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: http://icon.localhost; font-src 'self'; connect-src ipc: http://ipc.localhost; object-src 'none'; base-uri 'none'; frame-src 'none'; form-action 'none'`
- **No remote content:** fonts and curated icons are bundled. `withGlobalTauri: false`. No `{@html}` (CI grep). Descriptions are plain text, with HTML stripped in Rust.
- **Capabilities:** JS gets only the core event/window/app-version permissions plus our own commands, which are declared explicitly in `build.rs`. No shell, fs, opener, dialog or updater permissions in JS.
- **Validated newtypes at the IPC boundary:**
  - WingetId: `^[A-Za-z0-9][A-Za-z0-9.+_\-]{0,127}$`
  - ChocoId: `^[A-Za-z0-9][A-Za-z0-9._\-]{0,99}$`
  - Version: `^[A-Za-z0-9][A-Za-z0-9.+_\-]{0,63}$`
  - Search query: 1–100 characters, no control characters, no leading `-`.
  - The leading-alphanumeric rule blocks argument injection.
- **URL policy:** HTTPS only, no credentials, host on an allowlist (curated catalog, current app homepage, static list). Opened from Rust.
- **Honesty:** the fake security log lines are deleted. The app only states checks that actually happen (winget's own hash validation is attributed to winget).
- **Updater key:** private key only in CI secrets plus an offline backup. Losing it means existing installs can never auto-update again (documented in `RELEASING.md`).
- **Accepted residual risks:**
  - Community packages run as admin by design.
  - Over-the-shoulder elevation puts user-scope installs in the admin profile. It is detected and the user is warned.

## 9. Packaging, distribution, CI
- **`tauri.conf.json`:**
  - `version` read from root `package.json`.
  - `beforeBuildCommand` builds `app/`, and `frontendDist: ../app/dist`, so **stale UI builds become impossible**.
  - The build SHA appears in About.
- **Dev loop:** debug builds embed `asInvoker` (so `tauri dev` runs from a normal terminal); release builds embed `requireAdministrator` plus Common-Controls v6. The UI shows a "Not elevated (dev)" banner.
- **CI** (`ci.yml`, push/PR):
  - Frontend: `npm ci`, `svelte-check`, `vitest`, build, no-emoji and no-`{@html}` checks.
  - Rust (Windows): fmt, clippy `-D warnings`, tests (fixtures, argv, validators, merge, migrations), ts-rs bindings diff check.
  - Build smoke: `tauri build --no-bundle`.
- **Release** (`release.yml`, tag `v*`):
  1. Version check.
  2. Generate the release config: updater artifacts if the signing key secret exists; Authenticode `signCommand` (Azure Trusted Signing) if the Azure secrets exist. **Signing happens inside `tauri build`** so updater signatures stay valid.
  3. `tauri-action`: draft release with `latest.json`, then publish.
  4. winget submission via komac (`winget-releaser`) if `WINGET_TOKEN` exists.
- Missing secrets produce unsigned builds with a warning; the release never fails because of them.
- **Signing:** the user buys the certificate. Azure Trusted Signing (about $10/month) is preferred, subject to organisation eligibility; a traditional OV certificate is the fallback.
- **Versioning:** `scripts/bump-version.mjs` sets the version everywhere; `check-versions.mjs` blocks mismatches. `package-lock.json` is tracked.
- **winget:** the first submission is manual (`komac new`), then automated. The licence field is "Proprietary".

## 10. Testing strategy
- **Rust unit tests on fixtures:**
  - winget tables in English, German and Japanese, including truncated rows;
  - choco v1/v2 output;
  - progress lines;
  - argv builders (assert no `--force`, IDs are separate argv entries);
  - ID validators (reject `-x`, `;`, `&`, spaces, unicode);
  - merge/dedupe cases including near-misses that must **not** merge;
  - rules expiry;
  - settings migrations;
  - export/import validation.
- **Frontend:** vitest for stores and the job→button state mapping; `svelte-check` for types.
- **Manual verification checklist per phase**, kept in `docs/PLAN.md`: install, upgrade, cancel during download, reload mid-job, offline mode, choco absent, reduced motion, light/dark, 100%/150% DPI.
- **Release rehearsal:** tag an RC on a fork, then auto-update rc1 → rc2.

## 11. Documentation = project memory
| File | Purpose |
|---|---|
| `docs/PLAN.md` | **Live** phased task tracker with checkboxes; updated as each task lands |
| `docs/ARCHITECTURE.md` | Current system structure (rewritten from this spec) |
| `docs/DECISIONS.md` | ADRs; v2 ADRs marked superseded, new ADRs for D1–D13 |
| `docs/DESIGN_SYSTEM.md` | Tokens, type, components, motion presets |
| `docs/SECURITY.md` | Threat model, CSP, validation, residual risks |
| `docs/DEVELOPMENT.md` | Dev setup, elevation in dev, fixtures, commands |
| `docs/RELEASING.md` | Secrets, signing, key backup, komac |
| `docs/PRD.md` | Product requirements (updated to v3) |
| `docs/archive/` | v2 handover, progress, findings, task plan |

**Rule:** every phase ends by updating `PLAN.md` and any affected doc in the same commit series.

## 12. Phases (detail lives in `docs/PLAN.md`)
| Phase | Outcome | Verify |
|---|---|---|
| **P0** Cleanup and foundation | Python/`web/`/launchers removed, docs archived, `app/` scaffolded, manifest embedded, CSP + capabilities, build SHA | Release exe shows the new placeholder UI with SHA; UAC prompt appears |
| **P1** Rust core and sources | process layer, ID newtypes, winget/choco sources (locale- and width-safe), settings store, ts-rs | `cargo test` green on fixtures |
| **P2** Combined catalog | mappings, merge/dedupe, owner-wins updates, curated catalog, choco status + bootstrap | Merge tests; choco-absent mode |
| **P3** Queue and real progress | Rust job worker, MSI mutex, Job Object cancel, persistence, progress parsers, rules, batches | Parser tests; manual install/upgrade/cancel/reload |
| **P4** New interface | Warm Store tokens/themes, all views, JobButton/ring/skeleton/spring motion, reduced motion | Visual review vs approved mockups, light/dark |
| **P5** Real icons | icon protocol, Windows extraction, fetch + cache | Installed apps show real icons; offline from cache |
| **P6** Background and extras | tray, scheduler, toasts, scheduled-task autostart, single instance, export/import, bundles, full Settings | Manual checklist |
| **P7** Distribution | updater key + plugin, release pipeline with conditional signing, komac winget | RC1 → RC2 auto-update on a fork |
| **P8** Docs and hardening | all docs final, CSP/network audit, accessibility pass | No external requests in DevTools; checklist green |

## 13. Risks and open items
1. **Winget CLI output is not a stable API.** Mitigation: a large fixture set and locale-agnostic parsing. COM API later.
2. **A wrong cross-source merge installs the wrong app as admin.** Mitigation: curated mappings first, conservative heuristic.
3. **Chocolatey feed rate limits.** Mitigation: 300 ms debounce, 10-minute cache.
4. **Toast click-to-open a specific view** may need a direct WinRT toast later.
5. **Signing eligibility** (Azure Trusted Signing requires a verified organisation). The user is to confirm when purchasing.
6. **Brand icon rights.** Prefer publisher press-kit assets; record the source per icon in `catalog.json`.
7. **Verify `Chocolatey.Chocolatey` exists in winget** before making it the primary bootstrap path.
8. The updater endpoint uses the repo `merchanthasan2/AudoChocolatey` (rename to be decided before the first public release; a rename breaks the endpoint for older installs).
