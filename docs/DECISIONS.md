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
- **Status:** Accepted (implemented in P7)
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
