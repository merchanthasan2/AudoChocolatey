# Where Quantum Updater Stands Against the Field

**Purpose:** decide what to build next. Revisit when a competitor ships something new or before planning a phase.
**Last updated:** 2026-10-17 (UniGetUI 2026.3.x review on the owner's PC).

## Feature comparison

| Capability | UniGetUI (formerly WingetUI) | Ninite / Patch My PC / IObit / UCheck | Quantum Updater |
|---|---|---|---|
| Install, update, remove | Yes | Yes | Yes |
| Select several apps and update together | Yes | IObit free: 2 batch updates a day | Yes for updates; not yet for removals (P6b) |
| Toolbar; sortable table with version and source columns; source filter; list, compact and grid views | Yes | Patch My PC: grid and compact | Planned (P6d), with friendly source names (ADR 028) |
| Hide Windows components (runtimes, codecs) | No | No | Planned (P6d) |
| Skip or ignore an update | Yes | Some | Yes: skip a version, never update, Hidden list |
| Tray notifications, automatic updates | Yes | IObit Pro, UCheck Premium, Patch My PC | Planned (P6a, P6b) |
| Minimum update age; warning when the installer's website changes | Yes | No | Planned (P6b, P6c) |
| Export and import the app list; add a selection to a bundle; scheduled list backup | Yes (local file or GitHub Gist) | Ninite: custom installer; Patch My PC: list import and export | Planned (P6b "My bundles", local only) |
| Desktop shortcut cleanup, close the app before updating, restart needed, retry options | Yes | Patch My PC: stop shortcut creation | Planned (P6e) |
| Startup manager | No | Patch My PC (2026) | Planned (P6e) |
| Release notes link, download size, release date | Yes | Patch My PC: size | Planned (P6d) |
| Update apps installed straight from a website | Only when winget recognises them | Patch My PC: its own detection rules | Planned (P6d, catalog-declared rules only) |
| Curated catalog and bundles | Experimental "Software Catalog" (2026.3.2) | Ninite: about 100 apps | Yes: 96 hand-checked apps, 10 categories, 5 bundles |
| Original app icons, proper names, plain language | Partial | Partial | Yes |
| Checks that an update really took effect | No | No | Yes |
| Backup source when one fails | No | No | Yes |
| Microsoft Store apps | Yes (installed list) | No | Yes |
| Per-app install options (version, scope) | Yes | Some | No |
| Developer tools (pip, npm, Bun, Cargo, vcpkg, .NET tool, PowerShell Gallery, Scoop) | Yes. For Cargo it installs `cargo-binstall` and `cargo-update` itself with `cargo install … --locked` | No | Planned after P7: Pip, Npm, PowerShell Gallery and Cargo, off by default, helper tools only after asking (ADR 029). Scoop stays out (ADR 026) |
| Drivers | No | IObit sells a separate Driver Booster | Approved; after payment and licensing |
| Languages | 40+ | Some | English only |

## Competitors at a glance

| App | Updates | Installs from a catalog | Price | Where paid starts |
|---|---|---|---|---|
| Microsoft Store | Store apps update automatically | Yes (official marketplace) | Free | No paid tier; some apps inside are paid |
| Ninite | Supported apps | Yes, one custom installer | Home free; Pro from $1 per machine per year | Pro adds browser-based install, update and uninstall management |
| Patch My PC Home Updater | Scheduled and silent, 500+ apps; startup manager, grid view, restore points | Yes | Home free; business from $3,500 per year (up to 1,000 devices) | Commercial use |
| UniGetUI | Through winget, Chocolatey, Scoop, pip and others | Yes, from those catalogs | Free, open source (MIT), now maintained by Devolutions | Never; 300,000+ monthly users, 21,000+ GitHub stars, 40+ languages |
| UCheck | Scans and installs updates | New-app installs are Premium | Free; Premium $12 per year | Scheduled scans, new installs, unwanted-program protection, commercial use |
| IObit Software Updater | One-click; Pro adds automatic and scheduled | Yes | Free; Pro about $12–30 per year | Unlimited batch updates, automatic and scheduled updates, support |

Sources: devolutions.net/unigetui, the owner's market notes (2026-10-13), [UniGetUI releases](https://github.com/Devolutions/UniGetUI/releases), [UniGetUI README](https://github.com/devolutions/unigetui), [Patch My PC Home Updater release notes](https://patchmypc.com/release-notes/production-release/home-updater-releases/).

## UniGetUI on the owner's PC (2026-10-17)

Version 2026.3.1, read from its install folder and its 1,844 English text strings, without running it.
- **Its list is mostly code libraries.** Of 545 "installed packages":
  - about 290 are developer packages: about 275 Pip, 12 Npm, 2 PowerShell Gallery, 2 Cargo;
  - about 115 are Microsoft Store entries, roughly half of them Windows components;
  - about 76 are winget, about 45 come from the Programs list ("Local PC"), 15 from Chocolatey and 1 from Steam.
  
  Only about 11 real app updates were pending (9 winget, 2 Chocolatey). The "218 updates" screen was mostly Pip.
- **Cargo:** on finding Rust, it ran `cargo install cargo-binstall --locked` and tried to install `cargo-update`. Its record of this is `DependencyManagement.json` in `%LOCALAPPDATA%\UniGetUI\Configuration`.
- **Added since 2026.2:**
  - toast notifications, scheduled maintenance, automatic updates per package;
  - minimum update age, a skip level for minor updates;
  - installer-host and download-size columns, restart needed, CSV export;
  - an operation history with a live log, Start-menu and desktop shortcut management;
  - a terminal UI and command line (`uniget`), a local API on port 7058 for widgets;
  - telemetry and crash reports to Devolutions, a business policy editor (Devolutions Agent).
- **We skip:**
  - custom command-line options and before/after commands (they would run from user-editable data);
  - the local API and command line (more ways to attack the app);
  - GitHub Gist backup (needs an account);
  - the business policy editor and pre-release versions.

## What this tells us

- UniGetUI is the free power-user tool and covers the most package managers. We match its everyday controls (P6b–e) and add developer sources later, off by default (ADR 029). Our edge stays safety and clarity, not breadth.
- UniGetUI is moving toward a curated catalog (2026.3.2), so the catalog alone won't stay our differentiator.
- The paid products sell **automation** (scheduled and silent updates), **safety** and **commercial use**.
- Our edge is an **app-store feel** (curated catalog, real icons, plain words) plus **safety nobody else has**: post-update checks, backup sources, Store apps, nothing auto-runs from user-writable files.

## Direction

| Order | Phase | Main features | Why |
|---|---|---|---|
| 1 | P6a Background | Tray, scheduled checks, notifications and their choices, start at login, single instance | Table stakes; every paid competitor has it |
| 2 | P6b Control | Per-app update policy (auto, notify, never), quiet hours, battery and metered limits, minimum update age, My bundles, multi-select remove, complete Settings | Matches UniGetUI and the paid tiers |
| 3 | P6c Safety | Restore point before batches, update history with rollback, verified-publisher badge, security updates first, download site changed | Our differentiator |
| 4 | P6d Lists, P6e Polish | Toolbar and table, friendly sources and filter, hide Windows components, What's new, link directly installed apps, shortcut cleanup, close-app prompt, restart needed, startup manager | Owner review of UniGetUI, 2026-10-17 |
| 5 | P7 Distribution | Code signing, self-update, winget listing | Needed before selling; also stops antivirus alerts |
| 6 | Developer pack | Pip, Npm, PowerShell Gallery, Cargo, off by default | Owner decision 2026-10-17 (ADR 029) |
| 7 | Payment and licensing | Free vs Pro | See the split below |
| 8 | Drivers (approved) | Windows Update signed drivers, vendor tools, driver inventory | A Pro feature competitors charge for |
| 9 | P8 Hardening | Security review, accessibility, docs | Last, so it covers everything |
| Later | Optional | More languages, multi-PC dashboard | Only if users ask |

## Proposed free vs Pro split (to confirm before the licensing phase)

- **Free:** search, install, manual updates, catalog, bundles, skip and ignore.
- **Pro:** automatic and scheduled updates, drivers, restore points and rollback, security alerts, commercial use.
- **Later:** a multi-PC (family or small office) tier.
