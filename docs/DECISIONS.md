# Architecture Decision Records (ADRs)
## Quantum Updater

> v3 redesign (2026-10-12): ADR 001–004 are superseded or amended below. Source of truth: docs/superpowers/specs/2026-10-12-quantum-updater-v3-redesign-design.md

**Company:** Quantum Step ([quantumstep.in](https://quantumstep.in))

---

### ADR 001: Dual-Engine Architecture (Tauri v2 + Python Fallback)
- **Status:** Superseded by ADR 006
- **Context:** The application must run with maximum native performance, minimal memory footprint, and high reliability across Windows 10 and 11 environments.
- **Decision:** Implement Tauri v2 with Rust as the primary native release runtime (~9MB binary, <60MB RAM usage). Retain Python + pywebview as a local standalone development and scripting runtime sharing identical `web/` frontend assets.
- **Consequences:** Provides native compilation speed, embedded UAC manifest integration, and zero dependency on Node runtime at execution.

---

### ADR 002: Direct Brand-Colored CDN Integration for Simple Icons
- **Status:** Superseded by ADR 015
- **Context:** Icons loaded via raw npm package distributions (e.g., jsDelivr Simple Icons npm bundle) are hardcoded with monochrome `#000` (black) path fills. On dark-mode glass cards, this created an unreadable "50% dark overlay" appearance.
- **Decision:** Switch all icon resolutions to `https://cdn.simpleicons.org/{slug}`. This service dynamically delivers authentic brand-colored SVGs (e.g. green Spotify, blurple Discord, blue VS Code). For dark brands (e.g. GitHub, Apple, Steam), provide high-contrast white fallbacks.
- **Consequences:** Eliminates dark overlays, produces vibrant visual clarity, and maintains deterministic brand representation without local icon asset bloat.

---

### ADR 003: Sequential Asynchronous Operation Queue for Installations
- **Status:** Superseded by ADR 007 (queue moves to Rust in P3)
- **Context:** The Windows Installer MSI engine enforces a strict single-mutex constraint (`_MSIExecute`). Triggering parallel installations crashes background installers with fatal Error 1618. Furthermore, user feedback noted that active statuses were lost when navigating between tabs or filtering search results.
- **Decision:** Implement a client-side FIFO Sequential Operation Queue in `web/app.js` combined with a sticky, cross-tab Global Operation Dock and expandable Queue Drawer.
- **Consequences:** Guarantees zero MSI mutex collisions, ensures operations run reliably in sequence, and provides persistent visibility across all 5 navigation tabs.

---

### ADR 004: Manifest-Driven Single-Prompt UAC Elevation
- **Status:** Amended by ADR 008
- **Context:** Installing packages typically triggers repetitive Windows User Account Control (UAC) prompts for every application.
- **Decision:** Embed an application manifest with `<requestedExecutionLevel level="requireAdministrator"/>` directly into the binary and provide zero-flash launchers (`Run_QuantumUpdater.vbs`).
- **Consequences:** Users authenticate elevation once upon application launch. All downstream package operations run seamlessly unattended in the background.

---

### ADR 005: Strict Zero-Emoji Enterprise UI Policy
- **Status:** Accepted (CI-enforced by scripts/check-frontend-policy.mjs since v3 P0)
- **Context:** Emojis across system utility software look amateurish, inconsistently rendered across Windows builds, and lack precise alignment.
- **Decision:** Ban all emojis from code and layout. Replace every icon with tailored inline vector SVGs matching the Lucide / Heroicons standard (24x24 viewBox, stroke-based, 2px stroke-width).
- **Consequences:** Professional, uniform, enterprise-grade aesthetic that renders identically across Windows 10, Windows 11, and high-DPI displays.

---

## v3 decisions (2026-10-12)

### ADR 006: Tauri + Rust Only
- **Status:** Accepted
- **Context:** The Tauri/Python dual runtime drifted, and a release exe built before the last UI commit hid every UI change from the user.
- **Decision:** Remove the Python runtime, `web/`, CustomTkinter `ui/`, PyInstaller build and launchers. Tauri builds the Svelte frontend in `beforeBuildCommand` on every build.
- **Consequences:** One codebase. A stale embedded UI is impossible, and the build SHA in the app proves which commit is running.

### ADR 007: Combined winget + Chocolatey Catalog
- **Status:** Accepted (implemented in P2/P3)
- **Context:** The v2 "active engine" switch meant only one catalog was ever used.
- **Decision:** Search both sources in parallel. Merge entries via curated `mappings.json` first, then a conservative heuristic (same normalized name plus same homepage host or publisher), and otherwise keep them separate. Install prefers winget. Updates are handled by whichever source owns the install ("owner wins"). The job queue lives in Rust.
- **Consequences:** The user never picks an engine. A wrong merge (installing the wrong software as admin) is treated as worse than a duplicate.

### ADR 008: Always Admin, Properly Embedded
- **Status:** Accepted
- **Context:** `build.rs` never embedded `manifest.xml`, so v2 relied on `.bat`/`.vbs` launchers for elevation.
- **Decision:** `build.rs` embeds `requireAdministrator` in release builds and `asInvoker` in debug builds (so `tauri dev` works), both with Common-Controls v6. The launchers are removed.
- **Consequences:** Every UI vulnerability is an admin vulnerability, so ADR 009 is mandatory.

### ADR 009: Hardening for an Elevated WebView
- **Status:** Accepted
- **Decision:**
  - Strict CSP and no remote content; fonts are bundled.
  - `withGlobalTauri: false`.
  - An explicit command manifest in `build.rs` plus capabilities that grant JS only core event/window permissions and our own commands.
  - Validated ID newtypes, absolute exe paths, argv only and no shell.
  - Links open only on HTTPS allowlisted hosts.
  - Logs claim only checks that actually happen; the fake "Security Guard" lines are deleted.
- **Consequences:** Adding a command takes three steps (handler, `COMMANDS`, capability). That friction is deliberate.

### ADR 010: Svelte 5 + Vite Frontend
- **Status:** Accepted
- **Decision:** The `app/` workspace holds Svelte 5 (runes) + Vite + TypeScript. `app/src/lib/ipc/commands.ts` is the only file that calls `invoke`.
- **Consequences:** The 2,200-line `app.js` is replaced by small typed components; spring motion is built in.

### ADR 011: Warm Store Visual Language
- **Status:** Accepted
- **Decision:** Option C, "Warm Store": warm off-white `#F6F3EE` / warm charcoal `#1A1816`, deep green accent, Inter Tight + Inter (bundled), rounded cards, large full-colour icons, plain human copy. Light and dark themes.

### ADR 012: Springy Motion
- **Status:** Accepted
- **Decision:** Content motion uses a spring of about 420 ms with soft overshoot (results, Get → ring → tick → Open, row completion, counter roll, page changes). Hover and press respond in 120 ms or less. Reduced motion gets short fades. Motion is always driven by real state.

### ADR 013: Distribution
- **Status:** Accepted (owner confirmed Azure Trusted Signing on 2026-10-23; implemented in P7)
- **Decision:** NSIS per-machine installer and signed self-updates (`tauri-plugin-updater`, keys only in CI secrets). Authenticode signing runs inside `tauri build` via `signCommand` (Azure Trusted Signing preferred). winget publishing via komac. A missing secret produces an unsigned build with a warning instead of a failure.

### ADR 014: Background Operation
- **Status:** Accepted (implemented in P6)
- **Decision:** A tray icon, a scheduler (2 min after launch, then every 6 h by default) and toasts only when the update set changes. Autostart uses an elevated Scheduled Task (`ONLOGON /RL HIGHEST`), because Windows silently skips Run-key entries for `requireAdministrator` apps.

### ADR 015: Layered Icons
- **Status:** Accepted (implemented in P5)
- **Decision:** Icons are resolved in order: Windows extraction for installed apps, then bundled curated icons, then a cached site icon (HTTPS, size-capped, re-encoded), then a monogram. They are served via the `icon` URI scheme.

### ADR 016: Proprietary Licence
- **Status:** Accepted
- **Decision:** Free to use; all rights reserved by Quantum Step. The winget manifest uses `License: Proprietary`.

### ADR 017: Single Version Source
- **Status:** Accepted
- **Decision:** The root `package.json` version is authoritative. `scripts/bump-version.mjs` writes `app/package.json` and `Cargo.toml`; `scripts/check-versions.mjs` blocks mismatches in CI and on release tags. `tauri.conf.json` reads `../package.json`.

### ADR 018: Living Plan
- **Status:** Accepted
- **Decision:** `docs/PLAN.md` is the live tracker. Every task ticks its box in the same commit, and deviations go in its "Course corrections" log.

### ADR 019: Package-Manager Output Parsing (P1)
- **Status:** Accepted
- **Context:** Real winget 1.29 output on the owner's PC showed several things the spec didn't expect:
  - piped output is not truncated;
  - header words can be separated by a single space;
  - `search --source winget` drops the Source column;
  - `winget list` includes local-only `ARP\...`/`MSIX\...` rows.
  
  Non-elevated Chocolatey also mixes retry noise into `-r` output.
- **Decision:**
  - **winget columns:** each column starts at the display-width offset of a header token. Columns are assigned by position (Name, Id, Version, [Available|Match], [Source]), so header text, and therefore the display language, doesn't matter. The caller says whether a Source column exists.
  - **Row validation:** a row must have spaces at the column boundaries, which rejects footers.
  - **Local-only rows** are display-only (`source: null`).
  - **Truncated `…` ids** are resolved via `winget export`, otherwise dropped.
  - **Chocolatey:** only `<valid-id>|...` lines are accepted. Search uses the community OData feed over HTTPS (native TLS, system proxy), with `$filter=IsLatestVersion`.
- **Consequences:** The parser works in any Windows language and on CJK app names. The fixtures in `src-tauri/tests/fixtures/` are the contract; recapture them when winget changes (see DEVELOPMENT.md).

### ADR 020: Trusted Executable Resolution for an Elevated App (P1 review)
- **Status:** Accepted
- **Context:** The app runs elevated (ADR 008), and autostart runs it elevated at logon (ADR 014). `%LOCALAPPDATA%\Microsoft\WindowsApps` is writable by the standard user, and `ChocolateyInstall` can be overridden in the user's environment. Unprivileged malware could plant a `winget.exe` or redirect choco, and get it executed as admin.
- **Decision:**
  - **winget:** the alias is accepted only if it is an App Execution Link (reparse tag `0x8000001B`) whose target matches `<drive>:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_*_8wekyb3d8bbwe\winget.exe`. The app executes that **target** directly, which is TrustedInstaller-owned and verified working, so nothing can be swapped between the check and the launch.
  - **Chocolatey:** the location comes from HKLM `Session Manager\Environment\ChocolateyInstall`, literal absolute paths only (no `%VAR%`). Otherwise it is `<ProgramData known folder>\chocolatey`. The process environment is never used.
- **Residual risk:** a Chocolatey install directory with weakened ACLs (Chocolatey hardens its own folder by default) remains trusted. Accepted and documented in SECURITY.md (P8).

### ADR 022: Original Icons Only (P5)
- **Status:** Accepted (supersedes the "bundled curated icons" part of ADR 015)
- **Context:** The owner requires real, original app icons: no look-alikes and no generic tiles where an original exists. Using redrawn or third-party icon packs also carries trademark and licensing risk.
- **Decision:** Icons come only from the app itself or its official publisher, tried in this order:
  1. The installed program: its registry DisplayIcon, the main exe in its InstallLocation, or the MSIX package logo.
  2. The Chocolatey package's own icon.
  3. The publisher homepage's declared icon (apple-touch-icon, then the largest `rel=icon`, then `/favicon.ico`).
  
  Images are decoded and re-encoded to PNG (SVG is rendered with all external references disabled), cached on disk and served via `icon://`. If no original exists, a neutral monogram tile is shown. Only definite misses are remembered (7 days); temporary failures are retried.
- **Consequences:**
  - First-time Marketplace icons need one `winget show` plus a homepage request per app (1–3 s each, 2 at a time). After that they come from the cache instantly and work offline.
  - Packages without an original icon (e.g. the Chocolatey `vcredist140` package) keep the monogram.

### ADR 023: Curated Catalog and Strict Merging (P2)
- **Status:** Accepted
- **Context:** winget and Chocolatey list many of the same apps under different ids and names. Showing both copies is confusing; merging the wrong pair is worse, because the app installs as admin.
- **Decision:**
  - A hand-checked catalog (`src-tauri/catalog/apps.json`, embedded in the exe) maps each app to its winget and Chocolatey ids, with a display name, publisher, category and short blurb. `scripts/validate-catalog.mjs` checks every id against the live sources before a catalog change is committed.
  - Search results not in the catalog are merged only when the normalized names are identical, the winget publisher appears in the Chocolatey package, and the Chocolatey id names the same app. Bracketed qualifiers such as "(MSIX)" or "(ESR)" count as part of the name. Anything less certain stays as two rows.
  - Names shown everywhere: catalog name, then the Chocolatey package title, then the source name without a trailing version.
  - When both sources offer an update for the same catalog app, Chocolatey owns it (it only lists packages it installed) and the winget row is dropped.
  - Installs use winget when the app has a winget id, otherwise Chocolatey.
- **Consequences:** apps outside the catalog may appear twice in search. The catalog needs occasional upkeep as ids change; the validator makes that a one-command check.

### ADR 021: Download Progress Under Delivery Optimization (P3)
- **Status:** Accepted
- **Context:** winget downloads through Delivery Optimization, which writes the installer in one step (measured: 0 bytes, then 93 MB), so file-size polling can't show progress. DO's COM API might expose it, but cross-process visibility isn't documented and it means hand-written unsafe COM in an admin process.
- **Decision:** the winget ring is indeterminate while downloading, with the known size shown. Chocolatey's own percentages are shown as-is. The DO COM idea is dropped for v3.

### ADR 024: Microsoft Store Apps (P3 extras)
- **Status:** Accepted (owner decision 2026-10-13; the spec had listed the Store as a non-goal)
- **Decision:**
  - Store apps come through winget's `msstore` source with the same trusted winget exe. Ids are validated by `StoreId` (`9` + 11 or `XP` + 12 uppercase characters).
  - Store results join an app only through the catalog's `msstore` field, never by name, because Store and desktop builds install side by side.
  - Icons come from the Store listing's own logo (the endpoint winget's Store source uses; images only from Microsoft's Store image host).
  - "Microsoft Store apps" can be switched off in Settings. Store jobs are refused when the app runs as a different administrator than the person signed in, because Store apps install per user. That check reads the session's sign-in name; no other process is opened.
  - Installing accepts the Store terms on the person's behalf, so the details panel says so.
- **Verified live (2026-10-13):** an elevated install of a Store app landed in the signed-in user's account; "already installed" returns 0x8A15002B (shown as "Already up to date").

### ADR 025: Backup Sources (P3 extras)
- **Status:** Accepted
- **Decision:** a job that fails before any installer started tries once more from another source. Alternates come only from the hand-checked catalog.
  - Installs: download, checksum, not-found, no-suitable-installer and won't-run-as-admin failures move to the next enabled source (winget, Chocolatey, Store).
  - Updates (owner asked for this; it was advised against): only winget to Chocolatey, and only when Chocolatey's latest version is newer than the installed one. Never for `.portable`/`.commandline` packages or catalog pairs marked `noUpgradeFallback`. Never to or from the Store.
  - Never after an installer started, and never for cancel, busy, restart-needed, offline or app-in-use.
- **Consequences:** an app updated through Chocolatey is then owned by Chocolatey (ADR 023). Watch the first real fallbacks for vendors that create a second Programs entry and mark them `noUpgradeFallback`.

### ADR 026: No Scoop; Ask for Admin Through Windows (P3 extras)
- **Status:** Accepted (owner decision 2026-10-13)
- **Context:** Scoop runs per user and refuses admin. Running it from this always-elevated app means borrowing the signed-in user's token from explorer, which is the same technique malware uses; antivirus software flags it (the owner's Kaspersky had already flagged a debug build).
- **Decision:** Scoop is not supported. The app gets admin rights only from the person, through the normal Windows UAC prompt: the installed app asks at start, and if it is ever running without admin rights a banner offers "Allow admin access", which restarts it through UAC.

### ADR 027: Screens Describe Apps, Not Package Tools (P3 extras)
- **Status:** Accepted (owner decision 2026-10-13)
- **Decision:** normal screens and messages never name winget, Chocolatey or the Store source. Failure messages say what happened and what to do. Tool names appear only in Settings → "Advanced: app sources" and in the details panel's collapsed "Advanced" block. `scripts/policy.mjs` fails the build on tool names in screen text.
- **Amended by ADR 028** (lists show a friendly source per app).

### ADR 028: Friendly Source Names on Every List (P6d)
- **Status:** Accepted (owner decision 2026-10-17, after comparing UniGetUI's Updates and Installed tables)
- **Context:** the owner wants to see each app's version and source, as UniGetUI shows them, without bringing back tool jargon (ADR 027).
- **Decision:** lists show a source column with friendly names: Microsoft catalog (winget), Community catalog (Chocolatey), Microsoft Store, Installed directly (Windows' Programs list), Steam, and Developer once ADR 029 lands. The mapping lives in one file that `policy.mjs` exempts. Tool names stay in the Advanced block and the live job log.
- **Consequences:** a source filter and "Hide Windows components" become possible, which UniGetUI lacks. Sources must be classified once in Rust, not guessed in the UI.

### ADR 029: Developer Pack, Including Cargo (after P7)
- **Status:** Accepted (owner decision 2026-10-17). This reverses the earlier "not planned" note for pip, npm, PowerShell Gallery and Cargo in COMPETITIVE.md. Scoop stays out (ADR 026).
- **Context:** on the owner's PC, UniGetUI lists about 290 developer packages (about 275 from Pip). It also installed `cargo-binstall` on its own to support Cargo, which surprised the owner.
- **Decision:** a separate Developer section holds Pip, Npm, PowerShell Gallery and Cargo.
  - It is off by default and switched on in Settings.
  - These packages are never auto-updated and never included in "Update all".
  - A helper tool (for example `cargo-binstall` or `cargo-update`) is installed only after the person says yes, never on its own.
  - Per-user tools run with the person's own rights through a vetted path, never by borrowing explorer's token (the reason for ADR 026).
- **Consequences:** this is its own phase after P7, with its own plan and security review. Updating libraries can break the person's projects, so the screen says so.

### ADR 030: Web Links Open Through Explorer, Never as Administrator (P6d)
- **Status:** Accepted (architect design, owner-approved P6d plan, 2026-10-17)
- **Context:** Quantum Updater always runs elevated. `ShellExecute` on a web address from an elevated process can start the browser as administrator.
- **Decision:** "What's new" links open only through the desktop's own Explorer (`IShellWindows` → desktop view → `IShellDispatch2::ShellExecute`), so the browser runs as the signed-in person. If that fails, the link doesn't open and a plain message says so; there is no elevated fallback. Every link kept from package data passes `safe_url` (https, a real domain, default port, no user name, at most 2048 characters), and the page only ever sees the host: Rust opens its own cached link for the exact version.
- **Consequences:** links can't open when Explorer isn't running (for example in some remote sessions). That's accepted.

### ADR 031: Linking Apps Installed Directly (P6d)
- **Status:** Accepted (architect design, owner-approved P6d plan, 2026-10-17)
- **Context:** apps like FileZilla and WinSCP are often installed from their own websites, so no source manages them. Linking a Programs-list entry to the wrong catalog app would run a different program's installer as administrator.
- **Decision:** a catalog app may carry a `localMatch` rule. A link needs all of these: a machine-wide entry (HKLM; per-user entries can be written without admin rights); the catalog's publisher; an allowed architecture; the catalog's registry key or an anchored name pattern; exactly one candidate app for the entry and one entry for the app; no copy already managed by a source; and the target source switched on. Linked apps are listed under "Also updatable here", each started by the person after a confirm, and are never part of "Update all", automatic updates or notifications. The first rules are FileZilla (Community catalog) and WinSCP (Microsoft catalog).
- **Consequences:** each new rule needs a live check in Windows Sandbox before it ships. Node.js and other MSIs whose product code changes every release are deferred.

### ADR 032: Reinstall Is the Only Use of `--force` (P6d)
- **Status:** Accepted (owner-approved P6d plan, 2026-10-17)
- **Decision:** `OpRequest.mode` is Normal, Interactive (the installer's own window) or Reinstall. Rust's `mode_allowed` table is the only authority and `build_op` enforces it again. `--force` appears if and only if the mode is Reinstall. The Store gets neither Interactive nor Reinstall. Non-normal jobs never fall back to another source and have no download time limit. The page can't pin a version or ask for a rollback (P6c review).

### ADR 033: Quantum Step Creation Brand (2026-10-17)
- **Status:** Accepted (owner decision 2026-10-17)
- **Decision:** the Quantum Step Creation emblem (gradient ring with a "C" hook, crosshair, inner rings, atom, ring lettering) is the company logo and the source of the app icon. `brand/quantum-step-logo.svg` is a vector redraw of the owner's artwork. The app icon (`brand/quantum-updater-icon.svg`) drops the lettering and crosshair so it reads at 48 px and up, and a heavier version (`brand/quantum-updater-icon-small.svg`) is used at 16–32 px. `icon.ico` holds both.
- **Consequences:** when the owner supplies the original high-resolution file, it replaces the redraw in `brand/` for print and the website; the app icons stay vector-built.

### ADR 034: Startup Items Are Disabled, Never Deleted (P6e)
- **Status:** Accepted
- **Context:** a startup manager that can remove Run values, shortcuts, or scheduled tasks can also be talked into removing security software. The app runs as administrator, so a write here is a machine-wide write.
- **Decision:**
  - The page lists Run keys, Startup-folder shortcuts, and scheduled tasks whose only trigger is sign-in. Tasks under `\Microsoft` are not listed.
  - Turning one off writes the same `StartupApproved` flag Task Manager uses, or sets the task's Enabled bit. The Run value, the shortcut file, and the task are never deleted.
  - Windows (anything under the Windows directory, or a `\Microsoft` task), security products, items held by policy, and this app's own sign-in task are read-only. The page is not given a switch for them.
  - The page sends back only which row it means. Rust reads that row again and classifies it again before writing. A name that points outside one real entry is refused.
  - When a different administrator is signed in, per-user items are left out.
- **Consequences:** an app can still be turned off, and turned back on, from this page. Security software and Windows stay as they are. This PC's list was checked on 2026-10-23: Kaspersky and Security Health had no switch, and Quantum Updater's own task pointed at Settings.

### ADR 035: Public Repository (2026-10-23)
- **Status:** Accepted (owner decision 2026-10-23)
- **Decision:** the public repository is `quantumstep/quantum-updater`. Self-update requests that repository. The earlier repo `merchanthasan2/AudoChocolatey` is not the v3 update address.
- **Consequences:** P7's updater endpoint and the winget package identifier use this name. Renaming it after the first release would leave installed copies looking at the old address.

### ADR 036: Free and Paid (2026-10-23)
- **Status:** Accepted (owner decision 2026-10-23)
- **Decision:** The free app can search, install, update by hand, and use the catalog and bundles. Paid adds automatic updates, drivers, restore points and rollback, and commercial use. A paid copy is recognized by a license key checked on that PC. There is no account.
- **Consequences:** The licensing phase builds the key check. It is not built before that phase.

### ADR 037: winget Ships with the First Public Release (2026-10-23)
- **Status:** Accepted (owner decision 2026-10-23)
- **Decision:** The winget listing is submitted in the same release as the first public build. That release also includes self-update. The listing is not held for a later version.
- **Consequences:** P7 prepares the winget manifest with the first signed public release. Publishing still needs `WINGET_TOKEN`.

### ADR 038: Drivers After Licensing (2026-10-23)
- **Status:** Accepted (owner decision 2026-10-23)
- **Decision:** The driver work is built after the licensing phase. It switches on only when this PC has a paid license key.
- **Consequences:** Drivers are part of the paid tier from ADR 036. They are not in the free app, and they are not built before the key check exists. Acceptance checks on this PC come before any new phase.

### ADR 039: Uninstall apps that were installed directly (2026-10-23)
- **Status:** Accepted (owner decision 2026-10-23)
- **Context:** Rows winget only knows from the Programs list were display-only (ADR 019). Updating one still requires a catalog link (ADR 031), because the wrong installer would run as administrator. The owner asked to manage these apps too, and chose removal through each app's own uninstaller, with updates only where an installer is already known.
- **Decision:** Installed shows Uninstall on a directly installed app when its own uninstaller can be opened. The page sends only the row id. Rust reads the command again from that PC and does not pass it to a shell. A machine-wide uninstaller runs here and must be the system copy of msiexec, the system copy of rundll32 loading a DLL under a program folder, or a program under Program Files, ProgramData, or Windows Installer. A per-user uninstaller runs as the signed-in person, and only from that person's own folders. When a different administrator is signed in, per-user apps are left out. Windows components, and entries Windows marks as not removable, have no button. "Remove selected" stays limited to catalog apps, because each direct uninstall opens that app's own window. Updates are unchanged: only a catalog link, never part of Update all.
- **Consequences:** FileZilla's uninstaller on this PC was checked on 2026-10-23 and is its own `uninstall.exe` under Program Files. No app was actually removed in that check.
