# Where Quantum Updater Stands Against the Field

**Purpose:** decide what to build next. Revisit when a competitor ships something new or before planning a phase.
**Last updated:** 2026-10-13 (after P3 extras + sources).

## Feature comparison

| Capability | UniGetUI (formerly WingetUI) | Ninite / Patch My PC / IObit / UCheck | Quantum Updater |
|---|---|---|---|
| Install, update, remove | Yes | Yes | Yes |
| Select several apps and update together | Yes | IObit free: 2 batch updates a day | Yes for updates; not yet for removals (P6b) |
| Skip or ignore an update | Yes | Some | Yes: skip a version, never update, Hidden list |
| Tray notifications, automatic updates | Yes | IObit Pro, UCheck Premium, Patch My PC | Planned (P6a, P6b) |
| Export and import the app list | Yes | Ninite: custom installer | Planned (P6b) |
| Curated catalog and bundles | No (raw package lists) | Ninite: about 100 apps | Yes: 96 hand-checked apps, 10 categories, 5 bundles |
| Original app icons, proper names, plain language | Partial | Partial | Yes |
| Checks that an update really took effect | No | No | Yes |
| Backup source when one fails | No | No | Yes |
| Microsoft Store apps | No | No | Yes |
| Per-app install options (version, scope) | Yes | Some | No |
| Developer tools (pip, npm, Bun, Cargo, vcpkg, .NET tool, PowerShell Gallery, Scoop) | Yes (Cargo through cargo-binstall since 3.1.7) | No | No (Scoop dropped, ADR 026; Cargo not planned: developer-only, installs per user, unvetted downloads) |
| Drivers | No | IObit sells a separate Driver Booster | Planned later (drivers phase, needs approval) |
| Languages | 40+ | Some | English only |

## Competitors at a glance

| App | Updates | Installs from a catalog | Price | Where paid starts |
|---|---|---|---|---|
| Microsoft Store | Store apps update automatically | Yes (official marketplace) | Free | No paid tier; some apps inside are paid |
| Ninite | Supported apps | Yes, one custom installer | Home free; Pro from $1 per machine per year | Pro adds browser-based install, update and uninstall management |
| Patch My PC Home Updater | Scheduled and silent, 500+ apps | Yes | Home free; business from $3,500 per year (up to 1,000 devices) | Commercial use |
| UniGetUI | Through winget, Chocolatey, Scoop, pip and others | Yes, from those catalogs | Free, open source (MIT) | Never; 300,000+ monthly users, 21,000+ GitHub stars, 40+ languages |
| UCheck | Scans and installs updates | New-app installs are Premium | Free; Premium $12 per year | Scheduled scans, new installs, unwanted-program protection, commercial use |
| IObit Software Updater | One-click; Pro adds automatic and scheduled | Yes | Free; Pro about $12–30 per year | Unlimited batch updates, automatic and scheduled updates, support |

Sources: devolutions.net/unigetui and the owner's market notes (2026-10-13).

## What this tells us

- UniGetUI is the free power-user tool and covers the most package managers. Matching it on breadth (pip, npm, .NET) is not our game.
- The paid products sell **automation** (scheduled and silent updates), **safety** and **commercial use**.
- Our edge is an **app-store feel** (curated catalog, real icons, plain words) plus **safety nobody else has**: post-update checks, backup sources, Store apps, nothing auto-runs from user-writable files.

## Direction

| Order | Phase | Main features | Why |
|---|---|---|---|
| 1 | P6a Background | Tray, scheduled checks, notifications, start at login, single instance | Table stakes; every paid competitor has it |
| 2 | P6b Control | Per-app update policy (auto, notify, never), quiet hours, battery and metered limits, export and import, multi-select remove, complete Settings | Matches UniGetUI and the paid tiers |
| 3 | P6c Safety | Restore point before batches, update history with rollback, verified-publisher badge, security updates first | Our differentiator |
| 4 | P7 Distribution | Code signing, self-update, winget listing | Needed before selling; also stops antivirus alerts |
| 5 | Payment and licensing | Free vs Pro | See the split below |
| 6 | Drivers (after approval) | Windows Update signed drivers, vendor tools, driver inventory | A Pro feature competitors charge for |
| 7 | P8 Hardening | Security review, accessibility, docs | Last, so it covers everything |
| Later | Optional | More languages, developer pack, multi-PC dashboard | Only if users ask |

## Proposed free vs Pro split (to confirm before the licensing phase)

- **Free:** search, install, manual updates, catalog, bundles, skip and ignore.
- **Pro:** automatic and scheduled updates, drivers, restore points and rollback, security alerts, commercial use.
- **Later:** a multi-PC (family or small office) tier.
