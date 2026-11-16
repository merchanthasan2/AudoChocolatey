# Architecture Decision Records (ADRs)
## DeskVerve (formerly Quantum Updater)
**Company:** Quantum Step ([quantumstep.in](https://quantumstep.in))

---

### ADR 001: Dual-Engine Architecture (Tauri v2 + Python Fallback)
- **Status:** Accepted
- **Context:** The application must run with maximum native performance, minimal memory footprint, and high reliability across Windows 10 and 11 environments.
- **Decision:** Implement Tauri v2 with Rust as the primary native release runtime (~9MB binary, <60MB RAM usage). Retain Python + pywebview as a local standalone development and scripting runtime sharing identical `web/` frontend assets.
- **Consequences:** Provides native compilation speed, embedded UAC manifest integration, and zero dependency on Node runtime at execution.

---

### ADR 002: Direct Brand-Colored CDN Integration for Simple Icons
- **Status:** Accepted
- **Context:** Icons loaded via raw npm package distributions (e.g., jsDelivr Simple Icons npm bundle) are hardcoded with monochrome `#000` (black) path fills. On dark-mode glass cards, this created an unreadable "50% dark overlay" appearance.
- **Decision:** Switch all icon resolutions to `https://cdn.simpleicons.org/{slug}`. This service dynamically delivers authentic brand-colored SVGs (e.g. green Spotify, blurple Discord, blue VS Code). For dark brands (e.g. GitHub, Apple, Steam), provide high-contrast white fallbacks.
- **Consequences:** Eliminates dark overlays, produces vibrant visual clarity, and maintains deterministic brand representation without local icon asset bloat.

---

### ADR 003: Sequential Asynchronous Operation Queue for Installations
- **Status:** Accepted
- **Context:** The Windows Installer MSI engine enforces a strict single-mutex constraint (`_MSIExecute`). Triggering parallel installations crashes background installers with fatal Error 1618. Furthermore, user feedback noted that active statuses were lost when navigating between tabs or filtering search results.
- **Decision:** Implement a client-side FIFO Sequential Operation Queue in `web/app.js` combined with a sticky, cross-tab Global Operation Dock and expandable Queue Drawer.
- **Consequences:** Guarantees zero MSI mutex collisions, ensures operations run reliably in sequence, and provides persistent visibility across all 5 navigation tabs.

---

### ADR 004: Manifest-Driven Single-Prompt UAC Elevation
- **Status:** Accepted
- **Context:** Installing packages typically triggers repetitive Windows User Account Control (UAC) prompts for every application.
- **Decision:** Embed an application manifest with `<requestedExecutionLevel level="requireAdministrator"/>` directly into the binary and provide zero-flash launchers (`Run_QuantumUpdater.vbs`).
- **Consequences:** Users authenticate elevation once upon application launch. All downstream package operations run seamlessly unattended in the background.

---

### ADR 005: Strict Zero-Emoji Enterprise UI Policy
- **Status:** Accepted
- **Context:** Emojis across system utility software look amateurish, inconsistently rendered across Windows builds, and lack precise alignment.
- **Decision:** Ban all emojis from code and layout. Replace every icon with tailored inline vector SVGs matching the Lucide / Heroicons standard (24x24 viewBox, stroke-based, 2px stroke-width).
- **Consequences:** Professional, uniform, enterprise-grade aesthetic that renders identically across Windows 10, Windows 11, and high-DPI displays.

---

### ADR 006: Administrator at every start
- **Status:** Accepted
- **Context:** The 2026-11-08 security review recommended starting as a normal user and asking for administrator only when an install, update, or uninstall needs it. Machine-wide installs need that privilege.
- **Decision:** Keep asking for administrator every time Quantum Updater opens. Confirmed by the owner on 2026-11-08. This continues ADR 004.
- **Consequences:** One prompt at launch. Later tasks do not each ask again. A normal-user mode is not the default.

---

### ADR 007: Visual redesign gated on mockups
- **Status:** Accepted (2026-11-08). Normal theme is Direction B, Deep Teal Refined. Dark mode is Direction A, Graphite Prism. The window uses the real Quantum Updater emblem from `brand/quantum-updater-icon.svg`.
- **Context:** The owner rejected the current window's colours and unreadable button labels. The architect plan offered Direction A (Graphite Prism, recommended), Direction B (Deep Teal Refined), and Direction C (Paper Light).
- **Decision:** On 2026-11-08 the owner asked to see A and B mockups side by side before choosing. Live screens do not change until a direction is picked and its mockups are approved.
- **Consequences:** The designer produces two mockup sets. Implementation follows the architect's numbered sequence only after approval.

---

### ADR 008: Real app icons only
- **Status:** Accepted (2026-11-08)
- **Context:** Rows showed letter tiles (for example "OR" for Orca) or bundled one-colour silhouettes (OBS Studio). The owner requires each app's actual icon, on this PC and in Marketplace.
- **Decision:** Apps on this PC: extract the icon from the app's own program file (the uninstall entry's DisplayIcon, or its main executable). Marketplace apps not installed: download the official icon from the publisher's own website (the catalog homepage) and cache it on this PC. If neither exists, show a plain empty tile.
- **Consequences:** Letter tiles, bundled silhouette icons, and third-party icon services are removed. Icon downloads go only to the publisher's own domain over HTTPS.

---

### ADR 009: About this computer follows the approved mockup, with a hardware illustration library
- **Status:** Accepted (2026-11-08)
- **Context:** The owner approved `design/mockups/b/06-about-v2.html` and asked for the live page to match it exactly, with drawings for every kind of part so a user understands their PC.
- **Decision:** The live page matches the B mockup in the normal theme and the A mockup in dark mode. A library of inline SVG drawings covers processors by vendor and family (Intel Core and Core Ultra, AMD Ryzen, Snapdragon, other ARM, Apple silicon), graphics (NVIDIA, AMD Radeon, Intel Arc and integrated), device types (laptop, desktop, MacBook, tablet), memory, storage (SSD, NVMe, hard disk), battery, display, and camera. The processor card shows the family and generation read from this PC.
- **Consequences:** The engine must report processor vendor, family, generation, cores and threads, graphics adapters, battery, display, and camera. Each drawing needs a fallback when the part is not recognised: the generic drawing for that kind of part. Every graphics adapter is listed, built-in and dedicated.

---

### ADR 010: Physical drives only
- **Status:** Accepted (2026-11-08)
- **Context:** This PC shows a G: drive the same size as C:, which is a cloud drive's virtual volume, not a disk in the computer.
- **Decision:** About this computer, Cleanup and the sidebar disk card show every physical drive. Internal drives always appear. USB and external drives appear while plugged in, labelled External. Cloud and virtual drives (Google Drive, OneDrive, Dropbox, network and RAM disks) are left out by checking the volume's backing physical disk and file system, not the letter. Cleanup scans internal drives only unless the user picks an external one.
- **Consequences:** The engine reports each drive's bus type (internal, USB, external) and skips volumes with no physical disk behind them.

---

### ADR 011: Cleaning Windows-owned files through supported routes
- **Status:** Accepted (2026-11-08)
- **Context:** The architect found the delete guard blocks all of the Windows folder, so the Windows Update cache (4.27 GB on this PC), Windows Temp and crash dumps were counted and pre-ticked but never removed. The cleanup headline was false.
- **Decision:** Clear the Windows Update download folder only after stopping the update services, then restart them. Remove Windows Temp files older than 7 days. Offer Windows' own component-store cleanup as an optional step that Windows performs. Each item shows its own size and consent line, needs administrator, and is refused while an update is installing or a restart is pending.
- **Consequences:** Our code never deletes inside WinSxS, System32, SysWOW64 or Windows\Installer. Service stop needs a pending-reboot guard and a clear failure message if the services will not stop or restart.

---

### ADR 012: Optimize RAM measures a real trim
- **Status:** Accepted (2026-11-08)
- **Context:** Optimize RAM only trimmed Quantum Updater's own memory and displayed invented figures (150 MB in Python, 450 MB in the page). The architect recommended removing it; the owner chose to keep it and make it real.
- **Decision:** The button asks every running process the app can open to release unused memory (EmptyWorkingSet on each process, skipping protected and system-critical processes). Available memory is measured before and after, and only the measured change is shown, including zero.
- **Consequences:** Apps may page memory back in when next used; the result text says so in plain words. No estimated or fallback number is ever displayed.

---

### ADR 013: Large files removal
- **Status:** Accepted (2026-11-08)
- **Context:** The architect recommended Recycle Bin only for the Large files tool.
- **Decision:** Move to Recycle Bin is the default and primary action. A secondary Delete permanently action exists behind an extra confirmation that lists the files and their total size and states it cannot be undone. Both refuse protected folders and reparse points.
- **Consequences:** Every permanent delete is written to the activity history. Files on drives with no Recycle Bin can only be permanently deleted through that extra confirmation, or revealed.

---

### ADR 014: Windows Utility matrix boundary
- **Status:** Accepted (2026-11-08)
- **Context:** The owner supplied a 152-feature Windows utility matrix and asked for a plan before any build. The plan is `windows-utility-plan.html`.
- **Decision:** Quantum Updater stays an updater plus trusted system tools. 80 features are in this product: 12 already covered, then Wave 1 Cleanup, Wave 2 App and shell repair, Wave 3 Windows health, Wave 4 Network and power, Wave 5 Files and admin, after the current System tools track. Drivers, backup, and a vulnerability feed wait for licensing (existing ADRs). Twelve features open the matching Windows screen. Forty-eight PowerToys/DevToys-class tools wait for a later companion product. Six are skipped: HDD defrag, lost-partition recovery, partition resize, file shred, free-space overwrite, Windows 11 readiness.
- **Consequences:** Tracker phases 30–34 hold Waves 1–5. Held-out items are listed on the tracker but do not count in % and are not a live build queue. Mockups still gate layout. One owner decision at a time when a wave needs one.

---

### ADR 015: Leftover folders may remove folders and stale uninstall entries
- **Status:** Accepted (2026-11-08)
- **Context:** The architect recommended report-only for leftover folders until false positives were seen. The owner chose removal in this release.
- **Decision:** Matching leftover folders under user AppData, Program Files, and ProgramData may be moved to the Recycle Bin, with administrator and a per-folder confirmation. Folders modified in the last 90 days, or with a running process, are refused. After the folder is gone, the leftover uninstall registry entry may be deleted, but only after exporting a backup of that entry to `%LOCALAPPDATA%\QuantumUpdater\registry-backups\`.
- **Consequences:** The leftovers page needs Recycle Bin and Remove uninstall entry actions behind confirmations, plus Reveal. Never guess from folder names. Shared runtimes stay. Every removal is in the activity history and can be undone from Recycle Bin; registry backups are kept for restore.

---

### ADR 016: Team cycle and development tracker
- **Status:** Accepted (2026-11-11)
- **Context:** The owner set a standing team and asked that every change be written down before and after it is built.
- **Decision:** The chat agent is the Manager. The Design Architect (Opus 5.5) plans first, including the system picture, the screen flow, and whether a protective middle layer is needed. The Manager approves and writes the plan into `tracker.html` and this log before a build. Two Worker bees (Haiku) do one small step each. The Manager checks. The Security and Code Architect checks the finished slice against `docs/SECURITY_AUDIT.md`. The Design Architect then checks it against the plan. The tracker is updated again. `tracker.html` is the development tracker.
- **Consequences:** No feature starts until the picture and flow are current. Older tracker rows stay as built work from before this cycle. `docs/ARCHITECTURE.md` must be refreshed before the next feature. A test of this product is a new Windows build opened on this PC. `tracker.html` in the project folder is updated whenever the status changes.

---

### ADR 017: Windows branch holds the Windows app. Mac stays on its branch.
- **Status:** Accepted (2026-11-11)
- **Context:** The Windows folder still contained an old Rust app, an old Python window toolkit, and Mac build files. The notes described those as the current app. The owner asked to keep only what this Windows installer needs, and to leave Mac on its own branch. The look and materials should stay shared.
- **Decision:** `channel/windows` is the Windows app: `main.py`, `web_api.py`, `core/`, and `dist\QuantumUpdater\QuantumUpdater.exe`. Remove the Rust app, the old window toolkit, the Mac host, and the launchers that pointed at the old programs. `channel/macos` is left as it is. Screens, colours, type, and the emblem stay shared in `web/`, `design/`, and `brand/`. Windows uses WebView2. Mac will add only its own window material when that work starts.
- **Consequences:** ADR 001's Rust runtime is retired on this branch. A test is the Windows exe, not `python main.py`. Do not merge Mac host code onto this branch.

---

### ADR 018: Mac waits until the Windows app is ready to launch
- **Status:** Accepted (2026-11-11)
- **Context:** The owner said Mac work must wait. The current job is the Windows program on this PC.
- **Decision:** Until Quantum Updater for Windows is launch ready, work only on the Windows app. Do not change `channel/macos`, do not add Mac host code, and do not start a Mac build. Shared screens, colours, type, and the emblem stay as they are. Mac starts only after the owner says the Windows app is launch ready.
- **Consequences:** Open work is the Windows test notes, the owner’s look at Build 1, and the security check. The Mac branch stays as it was left.

---

### ADR 019: A DisplayLink USB device is not a graphics adapter
- **Status:** Accepted (2026-11-11)
- **Context:** On this PC, Windows files two DisplayLink USB interfaces next to the AMD and NVIDIA cards. About this computer counted all four as graphics adapters and called the USB ones dedicated.
- **Decision:** A display-class device whose hardware id starts with USB, or whose name is DisplayLink, is an external USB device. It is not a graphics adapter. Two interfaces of the same dock are one row. Built-in and dedicated cards stay under Graphics.
- **Consequences:** About this computer shows AMD and NVIDIA under Graphics, and one DisplayLink row under USB devices labelled External. The open Build 2 still shows the old list until the next program file.

---

### ADR 020: An update card shows only numbers the installer printed
- **Status:** Accepted (2026-11-11)
- **Context:** The update card drew a speed near 14 MB/s, a 54 MB total, and a percent that climbed to 94 even when the installer had not reported a transfer. The install card already uses the installer's own percent and downloaded-versus-total lines, and only then works out a speed from the change in those bytes. The Design Architect seat could not run because that model family is at its usage limit. The owner said to keep going and leave only their own checks pending.
- **Decision:** An update card must not invent a speed, a size, a percent, or a time. Until the installer prints a percent or a downloaded-versus-total line, the card says "Updating…" and leaves speed and size empty. A size is shown on the card only when the catalog reported one. The saved update list already stays on screen while a new check runs, so that part is not rebuilt. What the person must close, a downloaded installer, the publisher page, listing every installed app, a full catalog search, and a beta choice stay as later slices.
- **Consequences:** The open Build 2 still shows the invented speed. The next program file uses the real installer lines. No new program file is handed over until the security seat can check the DisplayLink slice.

---

### ADR 021: Worker bees use a cheaper seat than the architects
- **Status:** Superseded by ADR 023 (2026-11-12)
- **Context:** Opus is at its usage limit. The Design Architect and Security architect seats are running on Grok 4.7. The owner said the Worker bees do not need that seat.
- **Decision:** Design Architect and Security architect stay on the strong seat (Opus 5.5 when it has room, otherwise Grok 4.7). Worker bees build on Grok 4.6. They do not use Grok 4.7.
- **Consequences:** Named-model locks caused idle time when a seat hit a limit. Replaced by ADR 023.

### ADR 023: Manager assigns any available model to org seats
- **Status:** Accepted (2026-11-12)
- **Context:** Architect seats hit usage limits and the queue sat idle instead of moving to another model. The owner directed that seats are roles, not model names.
- **Decision:** The Manager chooses any available model for Design Architect, Worker bees, and Security from complexity and task type. On usage limit or failure before output, reassign that role immediately in the same turn. Never idle the queue for one named model.
- **Consequences:** ADR 021 named locks no longer bind. Global rule `manager-model-assignment.mdc` and the personal store record the same policy.

---

### ADR 022: Phase 36 plan (11 Nov test notes)
- **Status:** Accepted (2026-11-11)
- **Context:** The Design Architect (Grok 4.7) refreshed `docs/ARCHITECTURE.md` and returned the phase 36 plan. Opus remains at its usage limit. The owner said to keep going and leave only their own checks pending.
- **Decision:** No new protective middle layer. `web_api.py` stays the middle. 36.2 and 36.5 are already true in source. First two worker steps run together on Grok 4.6: (1) `web/app.js` show `Showing the last list. Checking again.` on `#updates-cache-note` while a check runs and cards already exist; (2) `core/scan_orchestrator.py` set `CATALOG_RETRY_SLEEP_S` to 0 and stop guessing a percent for `catalog-updates`. Later: no prefetch on upgrade, failed-update actions with Try again / Publisher page / Run installer on the bridge, Marketplace full-catalog wording, Allow a test version off by default, and real progress only. When Allow a test version is on and no test id exists, use the normal version and show `No test version is listed. The normal version will be used.`
- **Consequences:** Workers build steps 1 and 2 now. 36.4, 36.6, 36.7, and 36.8 wait for the next pair after Manager check. Mockups: `design/mockups/b/01-updates.html`, `02-marketplace.html`, `13-settings-beta.html`.

---

### ADR 033: Catalog export/import and app repair (31.2 / 31.5 / 31.6)
- **Status:** Accepted (2026-11-15). Manager filled Design Architect seat after Other Models limit.
- **Context:** Wave 2 needs WinGet catalog backup (ids and versions), desktop `winget repair`, and Store app data reset without a new screen.
- **Decision:** No new protective middle layer. `web_api.py` stays the middle. Settings cards after **Print queue**: **Catalog export and import** (`winget export` / `winget import` to a `.json` under the user profile or Downloads only) and **App repair** (WinGet repair by package id; **Reset app data** uses the existing Store app list). Import and reset require double UI confirm plus `confirmed=true`. Do not change live Park theme colours.
- **Consequences:** Tracker 31.2, 31.5, and 31.6 move to build. Security checks path allow-list, package id regex, and listed-package-only reset.

### ADR 032: Check for Quantum Updater updates before the app starts (42.5)
- **Status:** Accepted (2026-11-15).
- **Context:** Self-update only ran 2.5s after bridge ready, after catalog scans had already started. Owner wants this PC to pick up a better build before the rest of the app runs.
- **Decision:** No new protective middle layer. `main.py` starts `preload_self_update_check` and waits up to 8s before opening the window. Getting ready includes a **Quantum Updater** row. Catalog bootstrap starts only after that check settles (or times out at 10s in the UI). Offline or failed checks do not hold the app. The install banner still offers the update; applying it remains user-confirmed via the existing Install path. Official Releases rules from ADR 026 stay.
- **Consequences:** Tracker 42.5. A tagged Release is still required before an installed EXE can download a newer build.

### ADR 043: Owner-locked greenfield WinUI plan
- **Status:** Accepted (2026-11-15). Owner answered all six questions. Supersedes the provisional defaults in ADR 042.
- **Decision:**
  1. **Q1 = B.** Wipe the Phase 1 WinUI seed. Greenfield a new WinUI 3 project. Do not evolve the current `src/QuantumUpdater` shell.
  2. **Q2 = NOW.** Python is no longer the product and is not shipped.
  3. **Q3 = A.** Phase A first (Getting ready, Updates, Marketplace, Installed, About, self-update). Phase B (Cleanup and Settings tools) after that.
  4. **Q4 = Stitch.** Visual design comes from the owner’s Google Stitch project (colors, type, assets, material). Not Fluent-default, not cream, not Park, until that project is applied.
  5. **Q5 = installable + portable, multi-channel.** Primary artifact is an installer. A portable zip remains for direct download. GitHub and winget are two channels only. Later submissions include Microsoft Store, Chocolatey, and established download libraries.
  6. **Q6 = B.** When the greenfield project starts, move the Python app into `archive/` as reference. Do not delete it.
- **Protective middle layer:** one C# service layer. UI does not start processes directly.
- **Feature split (2026-11-15):**
  - **Build in Phase A (tracker 45):** Stitch shell, Updates & Health, App Marketplace, Installed, About This PC, Getting ready, self-update check. Catalog is winget.
  - **Discuss before any build (tracker 46):** how deep Disk & Storage goes; Cleanup, startup apps, large files, leftovers, activity log; Settings waves; Chocolatey as a second engine.
  - **Delivery after Phase A (tracker 47):** installer first, portable zip second, then GitHub, winget, Microsoft Store, Chocolatey, and download libraries.
- **Consequences:** Tracker phases 44–48. The headline percent counts only those phases. Older Python rows stay as history and do not count. The Phase 1 WinUI shell is not extended. Python moves to `archive/` when the greenfield project is created.

### ADR 045: Keep building and park owner actions
- **Status:** Accepted (2026-11-15). Owner instruction.
- **Decision:** Finish the tracker without stopping for questions. Decisions and outside actions stay on the Owner later list: phase 46 choices, the elevated packaging install, on-PC acceptance, git push, GitHub release, winget, Store, Chocolatey, download sites, and deploying the admin portal to quantumstep.in.
- **Consequences:** Phase 45 and the local account service proceed. Phase 46 stays unbuilt.

### ADR 044: Mandatory sign-in and a separate admin portal
- **Status:** Accepted (2026-11-15). Owner added this after ADR 043.
- **Decision:** The Windows app does not open its tools until the person signs in. A separate site at `quantumstep.in/quantumupdater/admin` is the only admin view. It shows install counts, who is signed in, updates run, apps installed or removed through Quantum Updater, and a system summary (Windows version, CPU, memory, disk headline).
- **Boundary:** The app does not send passwords, recovery keys, or file contents. The desktop app cannot open admin routes. One account service feeds both the app and the portal.
- **Consequences:** Tracker 45.8 and phase 48. Security review before the first client that uploads this data.

### ADR 042: Full WinUI 3 cutover now (replace dual-track ship)
- **Status:** Superseded by ADR 043 (2026-11-15). Provisional defaults are withdrawn.
- **Context:** Dual-track left Python as ship path and felt like migration stalled. Owner wants no wasted later rewrite.
- **Decision (provisional defaults — owner may override via multi-choice):**
  1. **From-scratch WinUI product** under `src/QuantumUpdater/` (evolve Phase 1 seed into full app structure).
  2. **Python is read-only behaviour reference** — no new Python features; ship path switches to WinUI after Phase A.
  3. **Phase A (core):** Getting ready, Updates, Marketplace, Installed, self-update, About.
  4. **Phase B (rest):** Cleanup, Startup, Large files, Leftovers, Activity log, Settings Waves 2–5.
  5. **UI:** Fluent WinUI native first; Park/cream visual lock owner-later.
  6. **Packaging:** self-contained win-x64 portable zip named `QuantumUpdater-Windows.zip`; MSIX later.
  7. **Elevation:** requireAdministrator every launch (ADR 004/006).
  8. **Protective middle layer:** single C# service layer (`Services/` + facade) — UI does not spawn processes directly.
- **Consequences:** Tracker Phase 44. CI/release retarget after Phase A. Winget PR stays valid against zip asset name.

### ADR 041: Migrate shell to C# + WinUI 3
- **Status:** Superseded in part by ADR 042 (2026-11-15). Dual-track ship path ends at Phase A cutover. Scaffold work from 041 remains the seed.
- **Context:** Python + pywebview + WebView2 + PyInstaller onedir is heavy and does not feel like a proper Windows system utility.
- **Decision:** Dual-track. **Target stack:** C# + WinUI 3. Protective middle layer: a single C# service layer (mirrors `web_api.py`) — no second ad-hoc bridge. Phase 1 screens: Getting ready, Updates, Installed, self-update. Packaging goal: MSIX when ready; portable folder/zip until then. Python app remains the behaviour reference and ship path until Phase 1 WinUI is owner-tested on this PC. Do not change live Park CSS. Folder: `src/QuantumUpdater/` (WinUI project).
- **Phase 1 build note (2026-11-15):** This PC lacks `Microsoft.Build.Packaging.Pri.Tasks.dll` (UWP/MSIX MSBuild). Phase 1 ships as **code-only WinUI** (`WindowsPackageType=None`, `EnablePriGenTooling=false`, no XAML `ms-appx` LoadComponent) so Release EXE can show a window without elevated VS Build Tools. Restore XAML + PRI tooling when owner installs the UWP MSBuild component.
- **Consequences:** Tracker Phase 43. Install .NET SDK + WinUI workload before scaffold. Security reviews every verified WinUI slice.

### ADR 040: Winget community submit for 2.0.3
- **Status:** Accepted (2026-11-15). Updated same day for validator fixes.
- **Context:** Tagged Releases publish `QuantumUpdater-Windows.zip`. Tracker 42.4 needed a public winget-pkgs PR.
- **Decision:** No new protective middle layer. Manifests use `InstallerType: zip` + `NestedInstallerType: portable` (elevation field removed after CI). Publisher/License URLs are GitHub (quantumstep.in timed out for validators). ManifestVersion **1.10.0** with schema headers on all three YAML files. Submitted via `wingetcreate submit` as PR https://github.com/microsoft/winget-pkgs/pull/480804. Local copies under `manifests/w/QuantumStep/QuantumUpdater/2.0.3/`.
- **Consequences:** `winget install QuantumStep.QuantumUpdater` works only after Microsoft merges. Until then use the GitHub zip.

### ADR 039: Self-update applies QuantumUpdater-Windows.zip
- **Status:** Accepted (2026-11-15). Manager provisional (Design Architect seat at model limit).
- **Context:** Releases ship a full onedir zip. In-app update still preferred a bare `.exe`, which cannot replace a working install.
- **Decision:** No new protective middle layer. `core/updater.py` prefers setup exe, then Windows zip, then any zip, then bare exe. Zip path unpacks, finds `QuantumUpdater.exe`, writes a detached `robocopy` swap over the installed folder, then relaunches. Architecture picture refreshed for Waves 2–5 + zip releases. Do not change live Park/cream CSS.
- **Consequences:** Install from Releases and Update in-app use the same zip asset. Tracker continues without waiting on Park lock.

### ADR 038: Publish full onedir zip on GitHub Releases
- **Status:** Accepted (2026-11-15).
- **Context:** Release pipeline uploaded only `dist\...\QuantumUpdater.exe` from an onedir build. That bootloader needs `_internal`; alone it fails with Python errors and will not start.
- **Decision:** CI publishes `QuantumUpdater-Windows.zip` (entire onedir folder). Do not publish a lone bootloader as the install asset. Startup failures write `%LOCALAPPDATA%\QuantumUpdater\logs\startup-crash.log` and show a MessageBox. Packaged as Build 8 / tag `v2.0.2`.
- **Consequences:** Other PCs unzip then run. Winget manifests may point at the zip until a true single-file portable exists.

### ADR 037: Cap stuck winget upgrades so batch updates continue
- **Status:** Accepted (2026-11-15).
- **Context:** Owner's 2.0 app froze on the second package during a multi-app update. `_run_streaming_cmd` waited on winget with no timeout.
- **Decision:** No new protective middle layer. Streaming install/upgrade kills the winget process tree after 600s and returns a timeout notice so the batch moves to the next package. Clear any leftover active winget before each stream. Packaged as Build 7.
- **Consequences:** Stuck packages show Retry; remaining updates keep going. Owner opens `dist\QuantumUpdater\QuantumUpdater.exe`.

### ADR 036: Wave 4/5 remaining engine tools
- **Status:** Accepted (2026-11-15).
- **Context:** Tracker phases 33–34 still had waiting engine rows (context menu, BSOD, TCP/IP, firewall, Wi-Fi, compression, ISO, services, optional features, etc.).
- **Decision:** No new protective middle layer. Extend `network_tools.py` plus focused modules under `core/system_tools/` (`shell_context`, `bsod_triage`, `sleep_wake`, `folder_compression`, `disk_images`, `shortcuts`, `file_locks`, `format_volume`, `acl_tools`, `services_admin`, `optional_features`, `reg_preview`, `stay_awake`, `privacy_settings`) behind `web_api.py`. Extend Network, Files, and Health Settings cards only. Confirm gates on destructive actions. Service denylist blocks `wuauserv`, `WinDefend`, and related security/update services. Removable-only format never targets `SystemDrive`. `.reg` import off unless `allow_import=true`. BitLocker keys still never returned. Do not change live Park theme colours.
- **Consequences:** Tracker 33.1–33.3, 33.7, 33.9, 33.15, 33.18 and 34.2–34.3, 34.6–34.8, 34.10–34.17 move to build/done. Owner later: on-PC checks.

### ADR 035: Waves 3–5 Settings toolkits
- **Status:** Accepted (2026-11-15).
- **Context:** Tracker Waves 3–5 need Windows health, network/power, and file tools without waiting on Park theme lock.
- **Decision:** No new protective middle layer. Ship `windows_health.py`, `network_tools.py`, and `file_tools.py` behind `web_api.py` with Settings cards. Confirm gates on destructive actions. BitLocker never returns recovery keys. 32.1 opens Windows Update settings rather than replacing the Windows Update agent. Remaining advanced rows (context-menu manager, BSOD triage, Wi-Fi survey, optional features, etc.) stay waiting.
- **Consequences:** Most Wave 3 and many Wave 4/5 rows move to built. Owner later: on-PC checks and a tagged Release for self-update downloads.

### ADR 034: Scheduled cleanup, duplicate finder, orphan uninstall (31.3 / 31.4 / 31.11)
- **Status:** Accepted (2026-11-15).
- **Context:** Wave 2 repair slices need duplicate file recycling, broken ARP uninstall cleanup, and a scheduled cleanup task with saved categories.
- **Decision:** No new protective middle layer. `web_api.py` stays the middle. **Duplicate files** scans a user-supplied folder (size then sha256, caps on files/groups) and recycles only paths from the latest scan while keeping one file per group (`confirmed=true`). **Orphan uninstallers** lists ARP entries with missing/broken uninstall targets, backs up the uninstall key, deletes it when confirmed, and returns `suggest_leftovers_scan`. **Scheduled cleanup** saves `user_temp` / `browser_caches` / `recycle_bin` + daily|weekly in `%LOCALAPPDATA%\QuantumUpdater\cleanup_schedule.json`, registers `QuantumUpdaterCleanup` via `schtasks` to run `QuantumUpdater.exe --scheduled-cleanup --no-elevate`; `main.py` handles that flag without UI or elevation loop. Settings cards hold the three flows. Do not change live Park theme colours.
- **Consequences:** Tracker 31.3, 31.4, 31.11 move to build/done. Unit tests for the three modules.

### ADR 031: Clear stuck print jobs and restart spooler (31.10)
- **Status:** Accepted (2026-11-15). Manager filled Design Architect seat after Other Models limit.
- **Context:** Wave 2 F035 — cancel selected jobs first; machine-wide clear is a second confirm.
- **Decision:** No new protective middle layer. `web_api.py` stays the middle. Settings card **Print queue** lists jobs, cancels one listed job with confirm, and offers machine-wide clear (stop Spooler, clear `%SystemRoot%\System32\spool\PRINTERS`, start Spooler) only after two UI confirms and `confirmed=true`. Printer names and job ids are allow-listed; cancel re-checks the live list. Do not change live Park theme colours.
- **Consequences:** Tracker 31.10 moves to build.

### ADR 030: Re-register an installed Store app (31.8)
- **Status:** Accepted (2026-11-15). Manager filled Design Architect seat after Other Models limit.
- **Context:** Wave 2 needs Store app repair when an AppX package will not open. Only packages already installed for this account.
- **Decision:** No new protective middle layer. `web_api.py` stays the middle. Settings card **Store app repair** lists current-user AppX packages (frameworks skipped) and re-registers via `Add-AppxPackage -DisableDevelopmentMode -Register` using the manifest path from that list only. Require `confirmed=true`. Reject unsafe package full names. Do not change live Park theme colours.
- **Consequences:** Tracker 31.8 moves to build. Security checks confirm-gate and path provenance.

### ADR 029: Catalog source refresh and recovery reset (31.1)
- **Status:** Accepted (2026-11-15). Manager filled Design Architect seat after Other Models limit.
- **Context:** Wave 2 F002 needs WinGet source repair. Refresh is everyday; reset is recovery. No new screen; Settings already holds diagnostics.
- **Decision:** No new protective middle layer. `web_api.py` stays the middle. Add Settings card **Catalog sources** with Refresh (`winget source update`) and Reset (`winget source reset --force`) behind two confirm dialogs and `confirmed=true` on the bridge. Engine methods live on `core/winget_engine.py`. Do not change live Park theme colours. After a successful refresh or reset, the user can re-run Check again on Updates.
- **Consequences:** Tracker 31.1 moves to build. Security checks that reset refuses without confirm and never runs arbitrary shell.

### ADR 028: Winget package QuantumStep.QuantumUpdater
- **Status:** Accepted (2026-11-15). In progress — not yet in the community index.
- **Context:** Owner wants `winget install QuantumStep.QuantumUpdater` on any PC. Local manifests existed but pointed at a non-working quantumstep.in setup URL, and the package is not in microsoft/winget-pkgs yet (`winget show` finds nothing).
- **Decision:** Ship a **portable** x64 `QuantumUpdater.exe` from GitHub Releases on `merchanthasan2/AudoChocolatey`. Package id stays `QuantumStep.QuantumUpdater`. Release Action builds the EXE, runs `tools/prepare_winget_release.ps1` for SHA256 + YAML, and attaches those files to the release. After a public tagged release exists, submit the YAML folder to `microsoft/winget-pkgs` (wingetcreate or PR). NSIS `*setup*.exe` remains optional later; portable is enough for first winget availability. Self-update (ADR 026) keeps using the same Releases channel.
- **Consequences:** `winget install` works only after (1) a tagged Release with the EXE is public, and (2) the winget-pkgs PR is merged (can take days). Until then, any PC can still download the Release EXE. Owner later: commit/push, tag `v*`, then `wingetcreate submit` or open the PR.

### ADR 027: Do not stop the queue for owner decisions
- **Status:** Accepted (2026-11-12).
- **Context:** Owner said development was pausing for decisions and actions only they can do. Product is about 20% complete.
- **Decision:** Queue owner decisions, commits, tags, mockup locks, and on-PC looks under Owner later. Keep building the next slice on the recommended provisional path. Ask at most one question at the end of a turn; never idle waiting.
- **Consequences:** Park D + Park palette is provisional until locked. Release publish waits for owner commit/tag. Tracker resume point lists Owner later without blocking the team.

### ADR 026: Official downloads and self-update on this GitHub repo
- **Status:** Accepted (2026-11-12). Owner chose option 1.
- **Context:** Auto-update pointed at `quantumstep/quantum-updater` while the live remote is `merchanthasan2/AudoChocolatey`, so other PCs could not get builds. Owner asked for a free downloadable installable each milestone and auto-update wherever installed.
- **Decision:** No new protective middle layer. Official channel is GitHub Releases on `merchanthasan2/AudoChocolatey`. Tag `v*` (or workflow_dispatch) runs `.github/workflows/release.yml`, which uploads `QuantumUpdater.exe`. `core/updater.py` only accepts https assets under that repo. Portable EXE replaces the frozen app file after exit; a future `*setup*.exe` still uses silent `/S`. Milestone cadence replaces “every 10%” (for example `v2.0.1` after an agreed slice). NSIS setup remains a later polish.
- **Consequences:** Installed copies check this repo’s latest release. A release only appears after the change is pushed and tagged. Source runs do not self-replace Python.

### ADR 025: Theme, ready gate, and measured speed
- **Status:** Proposed (2026-11-12). Blocked on owner lock of Direction D + Park palette.
- **Context:** Owner liked Direction D split-shell layout, but rejected cream/orange/black mockups as not matching the supplied Park photo (modern bright neon). Palette is sampled from `design/mockups/directions/the-park-reference.jpg`. Ready gate and measured speed still follow after mockup lock. [Design Architect](203d4207-230e-4780-a208-93e5f124930b) cream build plan is retained for gate/speed order only.
- **Decision:** No new protective middle layer. `web_api.py` stays the middle. Layout direction is D (dark sidebar + bright workspace). Working palette tokens: navy `#0C1730`, magenta `#F43E53`, lime `#24D177`, amber `#FFAC5C`, lit cream workspace `#FFF6E8`. Review file: `design/mockups/directions/01-D-park.html`. After owner locks this look, Design builds the remaining screens. Live CSS starts only after the full mockup set is accepted. Then workers: theme tokens, ready gate for `catalog-updates`, `catalog-installed`, and `local-arp` only (`local-hardware` off the gate), measured speed, then tracker `30.1`. A failed startup job does not hold the gate. A later Check again does not show the gate again.
- **Consequences:** Cream/orange/black editorial directions A–C are discarded for production. Older `design/mockups/cream/` drafts are superseded.

### ADR 024: Slice 36.8 shows real status only
- **Status:** Accepted (2026-11-12)
- **Context:** Slices 36.1 to 36.7 are in Build 3/4. Slice 36.8 is open. Cleanup delete, uninstall, leftover moves, large-file recycle, large-file delete, and the large-file scan line already show a sentence. Remaining holes: island indeterminate CSS, check-again fake 0/10, install start at 0, scan jobs guessing percent from elapsed time, sidebar disk bar starting at 45%. (Numbered ADR 024 because ADR 023 already records Manager model assignment.)
- **Decision:** No new protective middle layer. `web_api.py` stays the only path that starts a program, a scan, or a delete. A percent is shown only when the installer, the cleanup category count, or the storage-scan category count reported it. Otherwise the island says Working and the bar moves with no number. Words may say "Updating 2 of 5". That count is not drawn as a percent. A large-file scan shows the current folder and the files checked. The cream file `design/mockups/cream/01-shell-or-settings.html` is for review only. Do not change the live colours in this slice. Owner choices left pending with recommendation: cream stays review-only; sidebar unknown scan uses a moving mark and the job name.
- **Consequences:** Workers finish the remaining 36.8 holes. They do not change consent, path checks, or the uninstall plan. `core/progress.py` stays for its unit test. The screen stops reading guessed percents.

### ADR 046: Product renamed to DeskVerve
- **Status:** Accepted (2026-11-15)
- **Context:** On 2026-11-15 the owner renamed the product **DeskVerve** for Windows and **DeskVerve** for Mac. Publisher/company remains **Quantum Step**. The Design Architect Task seat produced the compatibility-first plan and the Manager approved it.
- **Decision:** Change current user-facing product text in the Windows app and admin portal, current shared naming docs and brand accessibility labels, and Windows file Product metadata to **DeskVerve** now. Reuse the existing emblem unchanged. Do not change Mac host code on `channel/windows`.
- **Compatibility boundary:** Until the packaging migration, retain source/project/folder/namespace/executable names; local `%LOCALAPPDATA%\QuantumUpdater` data; portal routes, cookies, and API; GitHub repo and update User-Agent; the `QuantumUpdater-Windows.zip` asset matcher; winget package ID, manifests, and PR #480804; release workflows; and archive, mirrors, dist, and generated output.
- **Future packaging target:** `DeskVerve.exe`, `DeskVerve-Windows.zip`, and DeskVerve installer names will be handled separately in Phase 47 with backward compatibility for existing update and account channels.
- **Consequences:** Tracker 45.9 implements the approved visible-text and Windows Product metadata slice without migrating technical identifiers. Phase 46 remains undecided and unstarted. This ADR does not start Phase 46 and does not authorize release/push/deploy.

### ADR 047: Supplied DeskVerve logo replaces Quantum emblem
- **Status:** Accepted (2026-11-15)
- **Context:** Owner supplied the actual DeskVerve logo artwork; Design Architect confirmed transparent and opaque-white 2000x667 PNG masters. ADR 046's no-new-logo compatibility note is superseded only for product artwork.
- **Source fidelity:** Preserve both original attachments unchanged under `brand/source/`; retain the transparent and opaque-white full lockups unchanged as masters. Derive the V-only transparent emblem from the transparent master by cropping/scaling only; do not recolour, trace, or redraw.
- **Outputs:** `brand/desk-verve-emblem.png`, `brand/desk-verve-lockup-transparent.png`, `brand/desk-verve-lockup-white.png`, `brand/desk-verve-icon.ico`, and `brand/png/desk-verve-emblem-{32,64,128,256,512}.png`.
- **Dark surfaces and placement:** Use transparent V for dark/small UI: Windows titlebar 24x24, Sign in 64x64, About 40x40, and tracker 52x52 with 4px padding. README uses the opaque-white full lockup at 320px width. The full black wordmark must not be placed directly on dark surfaces.
- **Compatibility:** Technical identifiers stay unchanged in this package; existing compatibility paths and old brand filenames may remain where needed, but visible old Q artwork is replaced. Quantum Step publisher logo stays unchanged. Phase 47 still owns the later DeskVerve exe/installer/download rename. No Mac host code, release, push, or deploy.

### ADR 048: Phase 46 scope and order (owner, 2026-11-16)
- **Status:** Accepted (2026-11-16)
- **Primary goal (owner):** The best-functioning app: fast, quick and easy to use. Any change to fundamentals goes to the owner as a choice first.
- **Order:**
  1. Highest priority: Updates and App Marketplace. Fix three owner-reported problems: Installed/Uninstall does not show all apps on the PC; the update screens look and feel poor; Marketplace shows only a handful of apps.
  2. Then Disk & Storage: full view plus delete. Every physical drive with free space and health, external drives labelled, no cloud volumes. Big files go to the Recycle Bin; delete permanently needs a second confirmation.
  3. Then the Cleanup set, all four: Startup apps (view, turn off, undo), Activity history (read-only), Temporary files (Windows-supported route only), Leftover folders (Recycle Bin per folder; registry backup before stale uninstall entries are removed).
  4. Then the old tool groups, one at a time, each tested before the next starts.
- **Catalog:** The full winget catalog comes first, with browse, categories, popular apps and real icons. Chocolatey comes later, as a second catalog for apps that winget does not have.
- **Root causes found (Manager, 2026-11-16):** The Installed list parses winget's text table and its id rule drops ids with a backslash (`ARP\...`, `MSIX\...`) and cut-short ids. Apps that winget does not know never appear. Marketplace is search-only, shows nothing before a search, and drops wide or cut-short rows.

### ADR 049: Phase 46 fundamentals (owner, 2026-11-16)
- **Status:** Accepted (2026-11-16). Questions from `docs/plans/2026-11-16-phase46-apps-first.md` section 10.
- **Q1 Sign in:** Optional. DeskVerve opens and works at once on any PC. A "Sign in" button stays. Signed-in users send reports when the public service is live (45.24).
- **Q2 Admin rights:** Keep the admin prompt at every start (owner choice). The COM read path must be proven in an admin process (spike 46.5.1); the text reader stays as the backup. Tasks 46.5.23-46.5.26 (admin helper, asInvoker) are dropped.
- **Q3 App data:** Microsoft's direct data connection (winget COM API) for reads, with text reading as the backup.
- **Q4 Apps that winget does not know:** Remove them with the app's own uninstaller, after a confirm dialog and strict safety rules. Unsafe entries fall back to Windows Settings.
- **Q5 Tech stack:** The owner delegated stack choices to the Manager, with one rule: DeskVerve must run on every Windows desktop version from Windows 10 onward. Manager choice: .NET 10 (LTS, fixes to 2028), Windows App SDK 1.8, `TargetPlatformMinVersion` 10.0.17763.0 (Windows 10 version 1809). WinUI 3 does not run on Windows 10 before 1809; those versions have no Microsoft support. Builds: win-x64 and win-arm64.
- **Icons (owner rule):** Each app shows its own original icon in every list: Installed, Updates, Marketplace, and installs. Installed apps: the icon from the app's own program file or its Store logo. Marketplace apps: the icon from the maker's own site or package. A letter tile is a last resort only. This answers Q8.
- **Q6 Confirm style:** One click for Update and Install. A dialog for Remove and Update all. The tick box goes away.
- **Q7 Start (owner rule):** At every start, DeskVerve scans the whole system before use: updates, installed apps, Marketplace catalog and icons, disk health and space, and cleanup estimates. Every function waits for readiness. The scan shows progress: one line per job (Waiting, Working with percent, Done, Failed) and an overall percent. Manager design: all jobs run at the same time; target is a full scan in about 5 s on the owner's PC; a failed job never traps the app.
- **Spike results (16 Nov):** winget COM API (Microsoft.WindowsPackageManager.ComInterop 1.29.380, needs target windows10.0.26100.0) listed 238 installed packages in 1.9 s as a normal user (98 ARP, 140 MSIX). Composite catalog for update data is not yet working; admin run is open. Virtual list: 2,000 rows with 47 row elements and 120 MB.
- **Q9 Packaging (owner):** Wait for the installer. The first public release is a setup program or MSIX, not a portable zip. The Manager picks the installer tool in phase 47 (stack delegated in Q5).
- **Q10 winget name (owner):** Close PR #480804 (old Python 2.0.3). After the Phase 46 fixes, the installer, and owner acceptance, open a new request with id `DeskVerve.Utility`, package name "DeskVerve Utility", and moniker `deskverve`, so anyone can run `winget install deskverve`. winget ids cannot contain spaces. The app window keeps the name "DeskVerve" unless the owner says otherwise.
