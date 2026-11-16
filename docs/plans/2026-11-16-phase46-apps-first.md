# Phase 46 priority plan: apps first (46.5, 46.6, 46.7)

- **Status:** Revised 16 Nov 2026 after the owner answers in ADR 049. Author: Design Architect.
- **Needs:** Manager review of this revision. The owner answered all 10 questions in section 10 (ADR 049).
- **Rules:** ADR 049 (owner answers; source of truth), ADR 048 (scope and order), ADR 043 (WinUI, Stitch shell, one service layer), ADR 020 and ADR 024 (show only numbers that an installer printed), ADR 046 (compatibility names stay until phase 47).
- **Mockups:** `design/mockups/phase46/index.html`. Getting ready: `design/mockups/phase46/00-getting-ready.html`.
- **Architecture:** `docs/ARCHITECTURE.md`, section "Planned: Phase 46 priority".

---

## 1. Goal

The owner wants the best-functioning app: fast, quick and easy to use.

This plan fixes the three problems that the owner reported:

1. **Installed** does not show all apps on the PC. (Task 46.5.)
2. **Marketplace** shows only a few apps. (Task 46.6.)
3. **Updates** looks and feels poor. (Task 46.7.)

The owner added three rules (ADR 049):

4. **Getting ready** scans the whole system at every start. The menu opens when the scan is done. The screen shows progress for each job. (Section 5.8.)
5. **Icons:** each app shows its own original icon on every screen. The icons are the most important part of each listing. (Section 5.6.)
6. **Sign-in is optional.** DeskVerve opens and works with no account. (Section 5.9.)

The plan also folds in the security fixes for this code: F3, F4, F16, F18, F24 (tracker 45.17) and F19.

## 2. Scope

**In scope**

- Installed: every app on the PC, with real icons and a safe uninstall for each app.
- Marketplace: the full Microsoft catalog (winget community source), with a home page, categories, popular apps, search, a details panel and real icons.
- Updates: a new screen design, Update all, one action at a time, progress for each app.
- Getting ready: a full scan at every start, all jobs at the same time, one row for each job, an overall percent.
- Original icons for every app on Installed, Updates, Marketplace and installs, with a measured coverage target.
- Optional sign-in: a "Sign in" link in the sidebar; reports only when signed in.
- A shared look for the three screens: rows, cards, chips, a bottom activity bar, dialogs.
- Saved results (cache), so each screen opens at once and refreshes in the background.
- One hardened winget runner (tracker 45.17).

**Out of scope (later phases)**

- Chocolatey as a second catalog (ADR 048 says later).
- The Disk and Storage screen (46.1) and the Cleanup set (46.2). Exception: Getting ready reads drive space and health (read only). The cleanup estimate job comes with 46.2.
- The installer and Store packaging (phase 47). The first public release waits for the installer (ADR 049, Q9).
- An admin helper or a start without admin. The admin prompt at every start stays (ADR 049, Q2).
- Skip a version, pin, or roll back an update.

## 3. What we measured on this PC (16 Nov 2026)

| Check | Result |
|---|---|
| winget version | v1.30.300 |
| `winget list` | 4.5 s, about 236 apps |
| Apps with an `ARP\` id in that list | 45 |
| Apps with an `MSIX\` id in that list | 96 |
| Apps that the current id rule drops | 141 of about 236 (about 60 percent) |
| `winget upgrade --include-unknown` | 6.4 s |
| `winget search --tag browser` | 3.3 s, about 500 rows |
| Registry uninstall entries with a name (PowerShell) | 98 in 1.1 s |
| Store apps, not frameworks (PowerShell) | 94 in 1.2 s |
| JSON output for list, search or upgrade | Not available in winget 1.30 |

Note: PowerShell adds about 1 s of start time. Direct C# reads are much faster.

## 4. Root causes

| Problem | Cause |
|---|---|
| Installed is short | `InstalledService` reads winget's text table. The id rule `^[A-Za-z0-9][A-Za-z0-9._+-]{0,180}$` drops every `ARP\...` and `MSIX\...` id. Apps that winget does not know never appear. The page also shows only the first 80 rows (`Take(80)`). |
| Marketplace is nearly empty | The page shows nothing before a search. Search uses the text table and the same id rule. The page shows only the first 40 rows. |
| Updates looks poor | One plain text line for each app. A confirm box at the top of the page. One status line for all messages. No icons, no progress, no Update all, no groups. |
| Slow screens | Each screen runs winget again (4 to 7 s). Getting ready runs three checks one way and gives no progress for each job. |
| Security (45.17) | winget is found through `PATH` (F3). Copied process code in three services (F24). Cancel does not stop winget (F16). Two actions can run at the same time (F18). |

---

## 5. Approach

### 5.1 How DeskVerve reads winget data (decision)

| Option | Good | Bad | Decision |
|---|---|---|---|
| **A. winget COM API** (`Microsoft.Management.Deployment`, the API that the `Microsoft.WinGet.Client` module uses) | Official. Structured data. Full ids. Local `ARP\` and `MSIX\` ids, product codes and package family names. Tag search. Icon data in the manifest. No text parsing. | Needs an interop NuGet package. An admin process can need a different start route (spike run 2). It depends on the App Installer version. | **Chosen for reading** |
| B. Read the winget source index file (`index.db`) | The full list offline. Tags. | A private file format that can change at any time. DeskVerve must download and check the file. It does not link installed apps. | Rejected |
| C. winget text or JSON from the command line | Already built. | winget 1.30 has no JSON for list, search or upgrade. `export` JSON skips apps that winget does not know. Text columns shift and cut. | **Fallback for reading only** |
| D. PowerShell `Microsoft.WinGet.Client` | Objects. | The module must come from the PowerShell Gallery. Each call starts PowerShell (1 to 2 s). One more dependency. | Rejected |

**Decision:**

- **Read** with the COM API (option A). This gives lists, search, tags and details.
- **Change** (install, upgrade, uninstall) with the hardened winget command (`WingetRunner`). The command can run as admin, DeskVerve can stop it, and it prints the installer percent.
- **Fallback:** if the COM API does not start, DeskVerve reads with the command line and one shared text parser. The Installed list stays complete, because the Windows list (5.2) does not need winget.
- **Spike first** (task 46.5.1). DeskVerve keeps the admin prompt at every start (ADR 049, Q2). So the COM read path must work in an admin process.
  - Run 1 (normal user) passed. It used the in-process factory (`FactoryLinkage` = `static`).
  - Run 2 (admin) is pending.
  - If COM fails in the admin process, DeskVerve reads with the text reader (`WingetCliReader`). The 5 s scan target then can fail (section 9).
- **Update data** (task 46.5.27). Run 1 did not get update data: the composite catalog returned `InvalidOptions`. Spike 46.5.27 finds a working way before task 46.5.13.

### 5.2 Installed shows every app (46.5)

**Data sources, read at the same time:**

| Source | What it gives | How |
|---|---|---|
| Registry, machine, 64-bit | Desktop apps for all users | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`, `RegistryView.Registry64` |
| Registry, machine, 32-bit | 32-bit desktop apps | Same key, `RegistryView.Registry32` (the `WOW6432Node` view) |
| Registry, user, 64-bit and 32-bit | Apps for this user only | `HKCU\...\Uninstall`, both views |
| Store apps for this user | Microsoft Store and MSIX apps | `Windows.Management.Deployment.PackageManager.FindPackagesForUser("")` |
| winget (COM) | Catalog id, available update, source | Composite catalog: local installed packages plus the winget source |

**Registry filter (skip the entry when):**

- `DisplayName` is empty.
- `SystemComponent` is 1.
- `ParentKeyName` has a value (a child update).
- `ReleaseType` is `Update`, `Hotfix` or `Security Update`.
- The name is `KB` plus digits, or starts with "Update for", "Security Update for" or "Hotfix for".

Keep entries with `NoRemove` = 1, but show them as "Protected".

**Source label for a Store package:** `SignatureKind` = `Store` gives "Microsoft Store". Other signatures (developer, enterprise) give "Installed directly".

**Store app filter (skip the package when):** it is a framework, a resource package, or it has no display name. Packages with `SignatureKind` = `System` stay hidden unless the user turns on "Show Windows parts". Packages with `NonRemovable` show as "Protected".

**Matching and de-duplication (in this order; stop at the first match):**

1. winget local id `ARP\{Machine|User}\{X64|X86}\{key}` equals the registry hive, view and key name. Exact.
2. winget product code equals the registry key name. Exact.
3. winget `MSIX\{full name}` or package family name equals the Store package. Exact.
4. Normalized name and publisher are equal. "Likely" match. Normalize: lower case, remove "(x64)", "(64-bit)", "x86", "(R)", "(TM)", version numbers at the end, and extra spaces.
5. No match. The row comes from one source only.

Two registry rows with the same normalized name, version and publisher in the 64-bit and 32-bit views become one row. DeskVerve keeps the 64-bit row.

**Uninstall route for each row:**

| Row | Route | Notes |
|---|---|---|
| Exact winget match (1 to 3) | `winget uninstall --id <id> --exact --silent` | Through `WingetRunner`. |
| MSI registry entry | `%SystemRoot%\System32\msiexec.exe /x{GUID} /passive` | The GUID comes from the key or the uninstall string. DeskVerve always has admin rights (Q2), so `/passive` is always added. |
| Other registry entry | `QuietUninstallString`, else `UninstallString` | Strict rules below. The app's own uninstaller can open a window. |
| Store app | `PackageManager.RemovePackageAsync(fullName)` | For this user only. |
| "Likely" match only (4) | The registry route, not winget | A name match never selects the winget route. |
| Protected or no safe route | No uninstall | Show "Open Windows Settings" (`ms-settings:appsfeatures`). |

**Strict rules for an app's own uninstall string** (idea from `archive/python-app/core/arp_inventory.py`; new C# code):

- The program path must be absolute and the file must exist.
- Machine entries (HKLM): the program must be under `%ProgramFiles%`, `%ProgramFiles(x86)%`, `%SystemRoot%\Installer` or `%ProgramData%\Package Cache`.
- User entries (HKCU): the program must be under `%LOCALAPPDATA%` or `%APPDATA%`. **Never run a user entry with admin rights.** A user can change these files, so admin rights here are a privilege risk. DeskVerve runs as admin (Q2). So it starts a user entry as the normal user through `NormalUserLauncher` (5.7, task 46.5.28). If the launcher fails, the row shows "Open Windows Settings".
- Block these programs: `cmd`, `powershell`, `pwsh`, `wscript`, `cscript`, `mshta`, `reg`, `regedit`, `rundll32`.
- Block arguments that contain `http://`, `https://`, `|`, `&`, `<`, `>`, a backtick, `$(`, `%` or `..`.
- Start the program with an argument list (`ArgumentList`), never with a shell.
- After the uninstall, read the registry key again. Success means the key is gone. Exit code 3010 means "restart needed".

### 5.3 Marketplace shows the full catalog (46.6)

**Home page (opens at once):**

- **Popular apps:** a curated list of about 120 app ids in a data file, `Assets/catalog-picks.json`. winget has no download counts, so DeskVerve chooses the picks. A script (`tools/check-catalog-picks.ps1`) checks every id before each release.
- **Categories:** Browsers, Productivity, Developer tools, Photo and video, Music, Communication, Utilities, Security, Gaming, AI tools. Each category has a few picks on the home page. "See all" runs a COM tag search (for example tag `browser`) and shows up to 200 apps.
- **State on each card:** Install, Installed, or Update. DeskVerve reads the state from the Installed data by catalog id.

**Search:**

- COM search on name, id, moniker and tag. Up to 100 results.
- The search starts 250 ms after the user stops typing. A new search cancels the previous one.
- Allowed text stays as today: letters, digits, space, `.`, `_`, `+`, `-`, up to 80 characters, not starting with `-`.
- Nothing is dropped: the COM API gives full ids and names.

**Details panel:** description, publisher, website, license and version. DeskVerve loads the details only when the user selects an app. It saves them for 7 days.

**Words on screen:** use friendly source names (ADR 028): "Microsoft catalog" for winget, "Microsoft Store" for msstore, "Installed directly" for apps from the Windows list only. The word "winget" appears only in Advanced text and logs.

### 5.4 Updates and the look and feel (46.7)

- **Updates screen:** header with "Last checked" time and a small "Checking" mark during a background check. Three summary tiles. A toolbar with select all, "Update selected", a filter box and sort. Groups: "Ready to update", "Version not known", "Updated today".
- **Row states:** Ready, Waiting (place in line), Working (installer percent only when printed; else a moving bar with the word "Working"), Updated (green check and new version), Failed (plain reason, Retry, Details).
- **One action at a time (F18):** one queue for the whole app. Update all puts every app in the queue. The queue runs them one after another. This also stops Windows Installer error 1618.
- **Activity bar:** a bar at the bottom of the window, on every screen. It shows "Updating Firefox · 1 of 3 · 45%", "View all" and "Cancel".
- **Cancel (F16):** Cancel stops the winget process tree at once.
- **Confirm (ADR 049, Q6):** remove the "I confirm" tick box on each page. One click starts Update (one app) and Install. A dialog opens for Remove, Update all, and Update selected with 2 or more apps. Mockups: `01-updates.html#confirm-all`, `02-installed.html#confirm`.
- **Sidebar:** icons, a 3 px accent mark on the active item, and an update count badge on Updates.
- **Lists:** virtualized lists (`ItemsRepeater`), so 2,000 rows scroll smoothly. No `Take(80)` or `Take(40)` limits.

### 5.5 Saved results and background refresh

- DeskVerve saves each result as JSON in `%LOCALAPPDATA%\QuantumUpdater\cache\`: `installed.json`, `upgrades.json`, `market-home.json`, `details\<hash>.json`. (The folder name stays `QuantumUpdater` per ADR 046.)
- Each file has a schema version and a saved time. DeskVerve writes to a temporary file, then replaces the old file. A bad file is deleted and ignored.
- A screen shows saved results at once, then refreshes in the background, then updates only the rows that changed.

| Data | Refresh on screen open when older than | Also refresh |
|---|---|---|
| Windows list (registry and Store apps) | Every open (it is fast) | After each install or uninstall |
| winget links and updates | 15 minutes | Check again; after each action |
| Marketplace home | 24 hours | Never during a search |
| Details for one app | 7 days | — |

- **Getting ready** runs a full scan at every start (5.8). The table above applies when a screen opens later in the same session.
- Saved results are the backup. When a scan job fails, the screen shows the saved result with its time, for example "From the scan at 10:31 yesterday".
- Saved icons (30 days), the Marketplace home (24 hours) and details (7 days) keep the scan near 5 s.

### 5.6 Icons (owner rule, ADR 049)

**Owner rule:** "Ensure each app has its specific original icon. The app icons are the most important things for any listing, updates, marketplace, new app installs."

An **original icon** is the icon that the app maker made for the app. The Windows blank-program icon and a letter tile are **not** original icons.

**Installed apps** (Installed, Updates). DeskVerve tries each source in this order and stops at the first original icon:

| Order | Source | How | Task |
|---|---|---|---|
| 1 | Registry `DisplayIcon` | Read `path,index`. Expand the environment names. Load an `.ico` file directly. For an `.exe` or `.dll`, take the icon resource at that index (`PrivateExtractIcons`, 256 px and 48 px). | 46.5.29 |
| 2 | Program file through the Windows shell | `IShellItemImageFactory.GetImage` (icon only, 256 px and 48 px), else `SHGetFileInfo`. Use the `DisplayIcon` file, else the main program in `InstallLocation`. Skip programs named `unins*`, `*setup*`, `*update*`, `*crash*`, `*helper*`. | 46.5.30 |
| 3 | MSI product icon (`ARPPRODUCTICON`) | `MsiGetProductInfo(productCode, "ProductIcon")` gives a file path, usually under `%SystemRoot%\Installer\{code}\`. | 46.5.31 |
| 4 | MSIX or AppX logo from the package manifest | Read `AppxManifest.xml` in the package folder. Take `uap:VisualElements` `Square44x44Logo`. Choose the file `targetsize-48_altform-unplated`, else `targetsize-48`, else `scale-200`, else `Package.Logo`. | 46.5.32 |
| 5 | Start menu shortcut (Design Architect addition; Manager to approve) | A `.lnk` file in the Start menu whose target is under the app's `InstallLocation`. Use the icon of the shortcut. | 46.5.33 |
| 6 | Catalog icon | When the row has an exact winget match, use the Marketplace sources below. | 46.6.5 |
| 7 | Letter tile | Last resort only. First letter of the name on a colour from the name. | 46.5.20 |

**Marketplace apps** (home, results, details, install cards):

| Order | Source | How | Task |
|---|---|---|---|
| 0 | The installed icon | When the app is on the PC, use the installed icon above. It is the original. | 46.6.5 |
| 1 | winget manifest `Icons` field | COM `CatalogPackageMetadata.Icons`: URL, file type, size, theme, SHA-256. Choose PNG or ICO, the default or dark theme, 64 px or larger. Check the SHA-256 when the manifest gives one. | 46.6.5 |
| 2 | Maker homepage | Start at `PackageUrl`, else `PublisherUrl`. Read the page (512 KB maximum). Take, in order: `<link rel="apple-touch-icon">`; the largest `<link rel="icon" sizes="...">`; the largest PNG in the `<link rel="manifest">` file (`manifest.json` `icons`); then `/favicon.ico`. | 46.6.13 |
| 3 | Letter tile | Last resort only. | 46.5.20 |

**Rules for internet icons** (task 46.6.12, `SafeIconFetcher`):

- HTTPS only. Up to 3 redirects, each one HTTPS.
- Maker domain only: the host must be in the same registered domain as `PackageUrl` or `PublisherUrl` (subdomains are allowed). No outside icon service.
- Images up to 256 KB. PNG, ICO or JPEG only. No SVG. A decode test runs before use.
- A 5 s limit for each request. No cookies. A plain `DeskVerve` client name with no personal data.
- Cached on disk for 30 days. A "not found" result is cached for 7 days.

**Cache and loading:**

- Memory cache: 500 icons. Disk cache: `%LOCALAPPDATA%\QuantumUpdater\icons\` (ADR 046 folder name).
- Each cache entry records the source that gave the icon (for example `DisplayIcon`, `Shell`, `MsiProductIcon`, `MsixManifest`, `Shortcut`, `WingetManifest`, `MakerSite`, `LetterTile`).
- A program file that has no icon resource gives the Windows blank-program icon. DeskVerve compares the result with the Windows stock icon (`SIID_APPLICATION`). A match counts as "not found", and DeskVerve tries the next source.
- Icons load after the row shows. An icon never holds a row or a screen. Getting ready has an "App icons" job (5.8).

**Coverage target (measured by task 46.7.21 on the owner's PC):**

| Surface | Target | Counts as a pass |
|---|---|---|
| Installed apps | **95 percent or more** with an original icon | Sources 1 to 6 |
| Marketplace popular picks (about 120) | **90 percent or more** with an original icon | Sources 0 to 2 |

DeskVerve writes a list of every app that falls back to a letter tile. The Manager records the list in section 12. Each fallback gets a reason (for example "no icon resource", "site icon on another domain").

### 5.7 Admin rights (ADR 049, Q2 = keep admin at every start)

- DeskVerve keeps `requireAdministrator` in `app.manifest`. Windows asks for admin one time, when the app opens.
- All reads and all changes run in the one DeskVerve process. There is **no admin helper** and no named pipe. Tasks 46.5.23 to 46.5.26 are dropped.
- **COM reads as admin:** spike run 2 (46.5.1) must prove this. If COM fails as admin, `WingetCliReader` reads instead.
- **User uninstall entries (HKCU):** DeskVerve must never run them as admin. `NormalUserLauncher` (task 46.5.28) starts the program with the rights of the signed-in Windows user. It copies the token of the Windows desktop shell (`GetShellWindow`, `OpenProcessToken`, `DuplicateTokenEx`, `CreateProcessWithTokenW`). This is the Microsoft-documented way to start a normal program from an admin program. If the shell is not running, or the launch fails, the row shows "Open Windows Settings".
- **Store apps:** `RemovePackageAsync` removes the app for the current Windows account. The admin rights do not change this.
- The remove dialog does not say "Windows can ask for permission", because DeskVerve already has permission. An app's own uninstaller can still show its own window.

### 5.8 Getting ready: a full scan at every start (owner rule, ADR 049, Q7)

**Owner rule (his words):** "the idea was to have a fully scanned ready system when the user starts up the app. The app will scan what updates are available, what marketplace apps are there, what system tools need improvement e.g. disk clear; every function should hold for app readiness, and once the scan completed the user can navigate and use its features. We need a way to show people progress."

**Mockup:** `design/mockups/phase46/00-getting-ready.html` (running state) and `00-getting-ready.html#done` (finished state).

**How it works:**

1. The app opens on Getting ready at every start. The sidebar is locked: the items are dimmed and show a lock.
2. `ScanOrchestrator` starts every job at the same time.
3. Each job has one row. The row shows the state and a one-line result.
4. An overall percent and a bar show at the top.
5. When every job is Done or Failed, the sidebar unlocks.
6. If every job is Done, Updates & Health opens by itself after 1 s. If a job failed, the screen stays and shows "Continue".

**Jobs:**

| Job | Service | Progress shown | Done result (example) | Status |
|---|---|---|---|---|
| App updates | `UpdatesCatalog` | Step N of 3 (connect, read installed apps, find new versions) | "7 updates ready" or "All apps are up to date" | Phase 46 |
| Installed apps | `InstalledCatalog` | Step N of 3 (registry, Store apps, catalog links) | "214 apps on this PC" | Phase 46 |
| App Marketplace | `MarketplaceCatalog` | Step N of 2 (picks, state of each pick) | "120 popular apps in 10 categories" | Phase 46 |
| App icons | `IconService` | Icons N of M, and the percent from that count | "325 of 334 original icons · 9 letter tiles" | Phase 46. Waits for the two app lists, then starts |
| Drives | `DiskService` (exists today) | Step N of 2 (space, health) | "2 drives healthy · 182 GB free on C:" | Phase 46, read only. The full Disk screen is 46.1 |
| Space you can free | Cleanup estimate (not built) | — | "About 3.4 GB in temporary files" | **Placeholder.** Task 46.2 adds the job. The row does not show until the job exists |
| DeskVerve version | `UpdateService` (exists today) | Working, no number | "DeskVerve is up to date" | Phase 46 |

**Row states:**

| State | What shows |
|---|---|
| Waiting | Grey chip "Waiting". |
| Working | Blue chip. A percent or a count shows only when the job counts real items or steps. Else the word "Working" and a moving bar. |
| Done | Green chip "Done" and the one-line result. |
| Failed | Red chip "Failed", the plain reason, and "Try again". If a saved result exists, the screen uses it and says its time. |

**Rules:**

- **A failed job never traps the app.** Each job has a 15 s limit (the icon job: 8 s, OQ1). After 15 s the job stops and shows "Failed · Took too long". An error in a job becomes "Failed" with a plain reason.
- **Overall percent** = the average of the job fractions. Done and Failed count as 1. Waiting counts as 0. Working counts as its real count or step fraction. The percent never comes from elapsed time (ADR 020, ADR 024). The owner asked for an overall percent (ADR 049), so this rule replaces the ADR 024 rule "a count is not drawn as a percent" for this screen only.
- **Target:** the full scan ends in **about 5 s** on the owner's PC, with a warm icon cache. Task 46.7.30 measures it.
- Getting ready is not a sidebar item. Each screen keeps its own "Check again".
- The heartbeat goes to the account service only when the user is signed in (5.9).

### 5.9 Optional sign-in (ADR 049, Q1)

- DeskVerve opens on Getting ready with no account. The sign-in gate in `MainWindow` goes away.
- The sidebar footer shows "Not signed in · Sign in". After sign-in it shows the email and "Sign out".
- "Sign in" opens the Sign in screen as a normal page. "Not now" goes back to the last screen.
- Heartbeat and activity reports go only when the user is signed in. `AccountClient` already checks `IsSignedIn`. Signed out, DeskVerve makes no account calls and shows no account errors.
- The public sign-in service is tracker 45.24. Until it is live, sign-in works only on the owner's PC.

---

## 6. Speed targets

Times are on this PC, from click to the result on screen.

| Moment | Today | Target |
|---|---|---|
| Admin prompt approved to the Getting ready screen | Not measured | **1.0 s** |
| Full scan at start, every job Done (warm icon cache) | About 7 s, 3 checks, no progress for each job | **5 s** (all jobs at the same time) |
| Full scan, first run (no saved icons) | About 7 s | **8 s**; icons that are not ready keep loading after the scan (section 10, OQ1) |
| Updates screen opens after the scan | Runs winget again when there is no snapshot | **0.3 s** |
| Fresh update check (background) | 6.4 s | **4 s** (COM); screen stays usable |
| Installed screen opens after the scan | Runs winget again (4.5 s) | **0.3 s** |
| Fresh Windows list (registry and Store apps) | Not read | **1.0 s** (registry 0.2 s; Store apps 0.8 s; at the same time) |
| winget links added to Installed | 4.5 s | **4 s**, in the background |
| Filter Installed while typing (1,000 apps) | — | **0.1 s** for each key |
| Marketplace home | Empty | **0.5 s**; saved icons in **1.0 s** |
| Search results after typing stops | 3 to 4 s after a click | **1.5 s** |
| Category "See all" (200 apps) | — | **2.0 s** |
| Details panel | — | **1.0 s**; saved: **0.1 s** |
| Click on Update, Install or Remove | — | Row shows "Waiting" in **0.1 s** |
| Scroll with 2,000 rows | 80 rows maximum | Smooth (60 frames each second) |
| Memory with 2,000 rows and icons | Not measured | **250 MB** or less |

Task 46.7.14 adds a speed log that records these times and the time of each scan job. The log has no personal data. Task 46.7.30 checks the 5 s scan target.

---

## 7. Screens, flows and files

**Screens touched:** Getting ready (full scan, `00-getting-ready.html`), Updates and Health, Installed, App Marketplace (home, results, details), the window shell (sidebar lock, sidebar footer, activity bar), Sign in (optional, Q1).

**Flows touched:** app start with a full scan, sign in and sign out (optional), check for updates, update one, update all, cancel, install, uninstall (four routes), search, browse a category, refresh after an action.

**New files** (all under `src/QuantumUpdater/` unless the path says otherwise):

| File | Job |
|---|---|
| `Services/Winget/WingetLocator.cs` | Find `winget.exe` through the App Installer package (F3) |
| `Services/Winget/WingetRunner.cs` | The one hardened command runner (F4, F16, F24) |
| `Services/Winget/PackageId.cs` | One id rule for actions, one for local ids (display only) |
| `Services/Winget/WingetTextTable.cs` | One shared text parser (fallback only) |
| `Services/Winget/WingetReader.cs` | COM reads: installed, updates, search, tag, details |
| `Services/Winget/WingetCliReader.cs` | The same reads through the command line (fallback) |
| `Services/ActionQueue.cs` | One action at a time for the whole app (F18) |
| `Services/SnapshotCache.cs` | Save and load results |
| `Services/Inventory/RegistryInventory.cs` | Read the four registry views |
| `Services/Inventory/StoreAppInventory.cs` | Read Store apps for this user |
| `Services/Inventory/InstalledMerger.cs` | Match and de-duplicate |
| `Services/Inventory/UninstallPlanner.cs` | Choose a safe uninstall route |
| `Services/Inventory/UninstallRunner.cs` | Run the route, then check the registry again |
| `Services/InstalledCatalog.cs` | Installed data: saved, then fresh |
| `Services/UpdatesCatalog.cs` | Updates data: saved, then fresh |
| `Services/Catalog/CatalogPicks.cs` | Load the curated picks |
| `Services/Catalog/MarketplaceCatalog.cs` | Home, search, category and details data |
| `Services/IconService.cs` | Icon source chain, cache, letter tile, source record |
| `Services/Icons/DisplayIconSource.cs`, `ShellIconSource.cs`, `MsiIconSource.cs`, `MsixLogoSource.cs`, `ShortcutIconSource.cs` | Installed icon sources 1 to 5 (5.6) |
| `Services/Icons/WingetManifestIconSource.cs`, `MakerSiteIconSource.cs` | Marketplace icon sources (5.6) |
| `Services/Net/SafeIconFetcher.cs` | HTTPS icon download with the 5.6 rules |
| `Services/Scan/ScanJob.cs`, `ScanOrchestrator.cs` | Full scan at start (5.8) |
| `Services/Scan/Jobs/UpdatesScanJob.cs`, `InstalledScanJob.cs`, `MarketplaceScanJob.cs`, `IconScanJob.cs`, `DriveScanJob.cs`, `SelfUpdateScanJob.cs` | One scan job each |
| `Services/Elevation/NormalUserLauncher.cs` | Start a user uninstaller without admin rights (5.7) |
| `Services/SpeedLog.cs` | Timings, no personal data |
| `Models/AppEntry.cs`, `CatalogApp.cs`, `UpgradeEntry.cs`, `ActionItem.cs` | Data records |
| `Views/Controls/AppIcon.cs`, `AppRow.cs`, `AppCard.cs`, `VirtualList.cs`, `ConfirmDialog.cs`, `ActivityBar.cs`, `DetailsPanel.cs`, `ScanJobRow.cs` | Shared screen parts |
| `Views/Marketplace/MarketplaceHome.cs`, `MarketplaceResults.cs` | Marketplace parts |
| `Assets/catalog-picks.json` | Curated picks and categories |
| `tools/check-catalog-picks.ps1` (repo root) | Check every pick id |
| `tools/spikes/WingetComSpike/` (repo root) | Spike project, not shipped |

**Changed files:** `Theme.cs`, `Chrome.cs`, `MainWindow.xaml.cs`, `Program.cs` (`--icon-report`), `QuantumUpdater.csproj`, `Services/SessionStore.cs`, `Views/SignInPage.cs`, `Services/AppServices.cs`, `Services/WingetService.cs`, `Services/InstalledService.cs`, `Services/MarketplaceService.cs`, `Views/UpdatesPage.cs`, `Views/InstalledPage.cs`, `Views/MarketplacePage.cs`, `Views/GettingReadyPage.cs`. `app.manifest` does not change (`requireAdministrator` stays, Q2).

**Retired:** `Services/CatalogSnapshot.cs` (replaced by `InstalledCatalog` and `UpdatesCatalog`). The three copies of process code and text parsing (F24).

---

## 8. Task list

Each task changes one file or makes one small change. Do the tasks in wave order. A task starts only when the tasks in "After" are done and verified. Ids follow the tracker: 46.5.x (Installed and shared base), 46.6.x (Marketplace), 46.7.x (Updates, Getting ready and look and feel).

**Before wave 1:** ADR 049 Q5 is answered. The Manager chose .NET 10 (LTS), Windows App SDK 1.8, `TargetPlatformMinVersion` 10.0.17763.0 (Windows 10 version 1809), and builds for win-x64 and win-arm64. The COM interop package needs `windows10.0.26100.0` in the target framework. That is allowed with the minimum version 10.0.17763.0. 45.18 (session check) is independent.

**Changes after ADR 049 (16 Nov 2026):**

- **Dropped:** 46.5.23, 46.5.24, 46.5.25, 46.5.26 (admin helper and `asInvoker`; Q2 keeps admin at start). 46.7.13 (Getting ready on the first run only; Q7 replaces it).
- **New:** 46.5.27 to 46.5.33 (update data spike, normal-user launcher, installed icon sources). 46.6.12 and 46.6.13 (safe icon download, maker site icons). 46.7.18 to 46.7.35 (scan orchestrator, scan jobs, Getting ready screen, icon coverage, optional sign-in).
- **Changed:** 46.5.5 (post delegate), 46.5.19 (normal-user launch), 46.5.20 (icon core only), 46.6.5 (manifest icons), 46.6.11 (icons on install cards).

### Wave progress

| Id | State | Notes |
|---|---|---|
| 46.5.1 | Run 1 pass; run 2 (admin) pending | See section 12. Run 2 did not complete on 16 Nov. |
| 46.7.1 | Pass (run 2) | 47 row elements, 120.3 MB. Smoothness: owner or Manager to look. |
| 46.5.3 | **Done** | `Services/Winget/PackageId.cs`. Tests in `tests/QuantumUpdater.Tests`. |
| 46.5.5 | **Done** | `Services/ActionQueue.cs`. The constructor takes an `Action<Action>` post delegate. The class has no WinUI types. Tests in `tests/QuantumUpdater.Tests`. |
| All tests | 93 passing | `tests/QuantumUpdater.Tests` |

### Wave 0: spikes (prove the risky parts)

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.1 | Make `tools/spikes/WingetComSpike/` (console). Reference the winget COM interop NuGet package. Print: COM start OK or error; count of installed packages; count with an `ARP\` or `MSIX\` local id; count with an update; time for each step. Run it as a normal user (run 1) and as admin (run 2). | — | Write both results in section 12 of this file. Pass = normal-user run lists about 236 packages in 4 s or less. **Run 2 pass = the same counts in an admin process** (Q2 keeps admin at start). |
| 46.7.1 | In the spike folder, make a small WinUI window with `ItemsRepeater` and a code-only `IElementFactory`. Show 2,000 fake rows with an image and two text lines. | — | Scroll is smooth. Memory 150 MB or less. Rows recycle (log count of created rows stays below 60). |
| 46.5.27 | In the spike: get update data. Try (a) the composite catalog with `CompositeSearchBehavior.LocalCatalogs` and correct options, then (b) two steps: the installed catalog, then the winget catalog by id for each package with a catalog id. Print the update count and the time. Run as admin. | 46.5.1 | Update count is within 2 of `winget upgrade --include-unknown`. Time 4 s or less. Write the way that works in section 12. |

### Wave 1: base services (security fixes 45.17) and the scan base

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.2 | `Services/Winget/WingetLocator.cs`: find the App Installer package `Microsoft.DesktopAppInstaller_8wekyb3d8bbwe` with `PackageManager`. Return `<InstalledLocation>\winget.exe` when the file exists and the path is under `%ProgramFiles%\WindowsApps`. Do not read `PATH`. (F3) | — | Unit: a fake path outside WindowsApps returns null. On PC: returns the App Installer path. A `winget.exe` put in a `PATH` folder is not used. |
| 46.5.3 | **Done.** `Services/Winget/PackageId.cs`: `IsCatalogId` (the current rule, for actions) and `IsLocalId` (`ARP\` or `MSIX\` prefix, no control characters or `"<>|`, up to 400 characters, display and matching only). | — | Unit: 10 good and 10 bad ids for each rule. A local id never passes `IsCatalogId`. |
| 46.5.4 | `Services/Winget/WingetRunner.cs`: one method `RunAsync(args, timeout, onLine, token)`. Argument list only. Streams each output line to `onLine`. Kills the process tree on cancel and on timeout (F16). Adds `--disable-interactivity` and `--accept-source-agreements`. Adds `--exact` whenever `--id` is in the arguments (F4). | 46.5.2 | Unit with a test program that sleeps 30 s: cancel ends it in 2 s or less. Lines arrive in order. Exit code returns. |
| 46.5.5 | **Done.** `Services/ActionQueue.cs`: first in, first out. One item runs at a time (F18). States: Waiting, Working, Done, Failed, Cancelled. Percent is null unless the work reports it. Events reach the screen thread through a post delegate (`Action<Action>`); `MainWindow` passes `DispatcherQueue.TryEnqueue`. No WinUI types in the class. Cancel a waiting item removes it; cancel the working item cancels its token. | — | Unit: 3 items never overlap; order kept; cancel both ways; a failed item does not stop the next one. |
| 46.5.6 | `Services/Winget/WingetTextTable.cs`: move the one text parser here. Keep `ARP\` and `MSIX\` rows as local ids. Mark a cut id (ends with "…" or "...") as `IsCut` and never use it for an action. | 46.5.3 | Unit with `_winget_sample.txt` and today's `winget list` text: no row is dropped; cut rows are marked. |
| 46.5.7 | `Services/WingetService.cs`: use `WingetLocator`, `WingetRunner` and `WingetTextTable`. Delete its own process and parse code (F24). | 46.5.4, 46.5.6 | Build passes. Update check still lists the same updates as `winget upgrade`. |
| 46.5.8 | `Services/InstalledService.cs`: the same change as 46.5.7. | 46.5.7 | Build passes. No duplicate process code (search for `BuildStartInfo` finds one file only). |
| 46.5.9 | `Services/MarketplaceService.cs`: the same change as 46.5.7. | 46.5.7 | Build passes. Search "firefox" returns Mozilla Firefox. |
| 46.5.28 | `Services/Elevation/NormalUserLauncher.cs`: start a program with an argument list as the normal Windows user, from the admin process (5.7). Copy the desktop shell token (`GetShellWindow`, `GetWindowThreadProcessId`, `OpenProcessToken`, `DuplicateTokenEx`, `CreateProcessWithTokenW`). Return the process, or a failure when the shell is not running. **Security Architect review before any live use.** | — | On PC: a test program started this way reports "not elevated" (`TokenElevation` = 0). A failed launch returns a failure, not an admin start. |
| 46.7.18 | `Services/Scan/ScanJob.cs`: `IScanJob` (Id, Title, `RunAsync(IProgress<ScanProgress>, token)` returns `ScanResult`). `ScanProgress`: step, step count, item count, item total. `ScanResult`: Done or Failed, one-line summary, plain reason. State enum: Waiting, Working, Done, Failed. No WinUI types. | — | Build passes. Unit: the fraction from steps and from items is correct; no items and no steps gives "no number". |
| 46.7.19 | `Services/Scan/ScanOrchestrator.cs`: start every registered job at the same time. A job can wait for other jobs (the icon job waits for the two app lists). 15 s limit for each job, then Failed "Took too long". An exception becomes Failed with a plain reason. Overall percent per 5.8. `Changed` and `Completed` events through an `Action<Action>` post delegate (like `ActionQueue`). `RetryAsync(jobId)`. Records the time of each job for `SpeedLog`. | 46.7.18 | Unit with fake jobs: 3 jobs of 1 s finish in about 1 s (parallel); a job that throws gives Failed and the others finish; a job of 20 s stops at 15 s; `Completed` fires one time; percent never goes down. |

### Wave 2: data and installed icons

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.10 | `Models/AppEntry.cs`: record with Key, Name, Publisher, Version, Source (`MicrosoftCatalog`, `MicrosoftStore`, `InstalledDirectly`), CatalogId, RegistryRef (hive, view, key), StoreFullName, Match (`Exact`, `Likely`, `None`), Route (`Winget`, `Msi`, `OwnUninstaller`, `Store`, `None`), IsProtected, SizeBytes, InstallDate, UpdateVersion, DisplayIcon, InstallLocation, ProductCode. | — | Build passes. |
| 46.5.11 | `Services/Inventory/RegistryInventory.cs`: read the four views (5.2). Apply the filter. Return rows with the registry reference, `DisplayIcon`, `InstallLocation` and product code. | 46.5.10 | On PC: 98 rows or more; contains a known desktop app; 0.2 s or less. |
| 46.5.12 | `Services/Inventory/StoreAppInventory.cs`: read Store apps for this user. Apply the filter (5.2). | 46.5.10 | On PC: about 90 rows; Calculator present; Settings hidden; 0.8 s or less. |
| 46.5.13 | `Services/Winget/WingetReader.cs`: COM reads. `ListInstalledAsync` (local id, catalog id, name, version, product codes, family names, update version, by the way from 46.5.27). `SearchAsync(text, limit)`, `ByTagAsync(tag, limit)`, `DetailsAsync(id)` (with the manifest `Icons` list). | 46.5.1 run 2 pass, 46.5.27 | On PC, as admin: installed count matches `winget list` within 2; update count matches `winget upgrade` within 2; search "zoom" returns Zoom.Zoom; 4 s or less. |
| 46.5.14 | `Services/Winget/WingetCliReader.cs`: the same four reads with `WingetRunner` and `WingetTextTable`. Used only when COM fails. | 46.5.6 | Force the fallback with a test switch. Same counts as 46.5.13 within 5. |
| 46.5.15 | `Services/Inventory/InstalledMerger.cs`: the match order and de-duplication in 5.2. | 46.5.11–46.5.13 | Unit with fixtures: ARP match; product code match; MSIX match; name-only match is `Likely`; 32-bit and 64-bit twin becomes one row; winget-only portable app stays. On PC: total rows 200 or more. |
| 46.5.16 | `Services/SnapshotCache.cs`: `Load<T>` and `Save<T>` with schema version, saved time, temporary file then replace, 20 MB limit, bad file deleted. | — | Unit: round trip; bad file; old schema ignored. |
| 46.5.17 | `Services/InstalledCatalog.cs`: `LoadSaved()` at once; `RefreshAsync(progress)` reads registry and Store apps at the same time, then winget links; reports steps; saves; raises `Changed`. | 46.5.15, 46.5.16 | Fresh Windows list in 1.0 s. Saved data loads in 0.3 s. |
| 46.5.18 | `Services/Inventory/UninstallPlanner.cs`: the routes and strict rules in 5.2. Returns a plan (program, arguments, run as admin or run as the normal user) or "no safe route". Every HKCU own-uninstaller plan is "run as the normal user". | 46.5.10 | Unit: 20 uninstall strings (MSI, quoted EXE, unquoted EXE with spaces, `cmd /c`, URL, `%TEMP%` path, HKCU path) give the expected plan or refusal. No HKCU plan says "run as admin". |
| 46.5.19 | `Services/Inventory/UninstallRunner.cs`: run a plan through `ActionQueue`; a "normal user" plan goes through `NormalUserLauncher`; 15 minute limit; kill on cancel; read the key again; 3010 = restart needed. | 46.5.5, 46.5.18, 46.5.28 | Unit with a fake plan. On PC: see the live-test rule in section 9. |
| 46.5.20 | `Services/IconService.cs` (core only): a chain of icon sources (`IIconSource`) in the order of 5.6; memory cache (500) and disk cache (30 days; "not found" 7 days); records the source used for each app; blank-program icon check against `SIID_APPLICATION`; letter tile last. | 46.5.10 | Unit with fake sources: the first source that gives an original icon wins; a blank-program icon goes to the next source; saved icon loads in 0.05 s. |
| 46.5.29 | `Services/Icons/DisplayIconSource.cs`: source 1 in 5.6 (registry `DisplayIcon`, `path,index`, `.ico`, `.exe`, `.dll`). Local files only. | 46.5.20 | Unit: `"C:\x\a.exe",2`, an unquoted path, a negative resource id, and a missing file. On PC: 10 known desktop apps give their own icon. |
| 46.5.30 | `Services/Icons/ShellIconSource.cs`: source 2 in 5.6 (`IShellItemImageFactory`, else `SHGetFileInfo`; main program search in `InstallLocation`; skip list). | 46.5.20 | On PC: an app with no `DisplayIcon` gives its own icon; `unins000.exe` is never chosen. |
| 46.5.31 | `Services/Icons/MsiIconSource.cs`: source 3 in 5.6 (`MsiGetProductInfo` "ProductIcon"). | 46.5.20 | On PC: 3 MSI apps give their own icon. An unknown product code gives "not found", not an error. |
| 46.5.32 | `Services/Icons/MsixLogoSource.cs`: source 4 in 5.6 (manifest `Square44x44Logo`, target size files, then `Package.Logo`). | 46.5.20 | On PC: Calculator, Photos and 3 Store apps give their own logo with no plate. |
| 46.5.33 | `Services/Icons/ShortcutIconSource.cs`: source 5 in 5.6 (Start menu shortcut into `InstallLocation`). Manager approves this addition first. | 46.5.20 | On PC: one app that fails sources 1 to 4 gets its icon from the shortcut. |

### Wave 3: shared screen parts (look and feel) and optional sign-in

| Id | Task | After | Test |
|---|---|---|---|
| 46.7.2 | `Theme.cs`: add tokens (section 11). | — | Build passes. Old tokens keep their values. |
| 46.7.3 | `Chrome.cs`: add `SecondaryButton`, `DangerButton`, `Chip`, `Caption`, `SectionHeader`, `SearchBox`, `LetterTile`. | 46.7.2 | Compare with the mockups at 100 percent zoom. |
| 46.7.4 | `Views/Controls/AppIcon.cs`: 32 px or 48 px image; letter tile until the icon loads; safe when the row recycles. | 46.5.20, 46.7.3 | Fast scroll shows no wrong icons. |
| 46.7.5 | `Views/Controls/AppRow.cs`: icon, name, publisher, version area, chips, action button, inline progress, states (5.4). | 46.7.4 | Each state matches `01-updates.html`. |
| 46.7.6 | `Views/Controls/VirtualList.cs`: `ItemsRepeater` with the factory from spike 46.7.1. | 46.7.1, 46.7.5 | 2,000 rows scroll smoothly. |
| 46.7.7 | `Views/Controls/ConfirmDialog.cs`: code-only `ContentDialog` with `XamlRoot`, title, body, an optional app list, Cancel and one main button. For Remove, the main button is red and the default button is Cancel. | 46.7.3 | Matches the dialogs in `02-installed.html#confirm` and `01-updates.html#confirm-all`. Esc cancels. |
| 46.7.8 | `Views/Controls/ActivityBar.cs`: bound to `ActionQueue`. Current item, "N of M", percent only when reported, "View all" flyout, "Cancel". Hidden when the queue is empty. | 46.5.5, 46.7.3 | Matches the bar in `01-updates.html`. |
| 46.7.9 | `MainWindow.xaml.cs`: sidebar icons, accent mark, update badge; add the activity bar row; remove "Getting ready" from the sidebar items. | 46.7.8 | Matches the shell in all mockups. |
| 46.7.20 | `Views/Controls/ScanJobRow.cs`: job icon, title, state line, small bar, state chip, "Try again" (5.8). | 46.7.3, 46.7.18 | Each state matches `00-getting-ready.html` and `00-getting-ready.html#done`. |
| 46.7.31 | `MainWindow.xaml.cs`: remove the sign-in gate. The app always opens on Getting ready. (Q1) | 46.7.9 | Delete the saved session. The app opens and works with no account. |
| 46.7.32 | `MainWindow.xaml.cs`: sidebar footer "Not signed in · Sign in"; signed in: the email and "Sign out". | 46.7.31 | Matches the footer in the mockups. Both states show. |
| 46.7.33 | `Views/SignInPage.cs`: opens from the footer link as a normal page; add "Not now"; after sign-in or "Not now", go back to the last screen. | 46.7.32 | Sign in from Installed returns to Installed. |
| 46.7.34 | `Services/SessionStore.cs` and the footer: "Sign out" calls `Clear()`; the footer changes to "Not signed in". | 46.7.32 | After sign-out, restart: the app opens signed out. |
| 46.7.35 | Signed-out check: heartbeat and activity calls (`GettingReadyPage`, `UpdatesPage`, `MarketplacePage`, `InstalledPage`) make no network call when signed out. `AccountClient` already checks `IsSignedIn`; add a unit test for each method. | 46.7.31 | Unit: signed out, a fake HTTP handler gets 0 calls. On PC: signed out, no account error text shows. |

### Wave 4: screens, Marketplace icons and scan jobs

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.21 | `Views/InstalledPage.cs`: rewrite per `02-installed.html`. Search box, source chips with counts, sort, virtual list, "Protected" rows, Uninstall button. | 46.5.17, 46.7.6 | All rows from 46.5.15 show. Typing filters in 0.1 s. |
| 46.5.22 | `Views/InstalledPage.cs`: Uninstall opens `ConfirmDialog` with the app name and the route text; then the queue. Row shows "Removing · Working"; the row leaves the list after success. | 46.5.19, 46.5.21, 46.7.7 | Live test per section 9. |
| 46.7.10 | `Services/UpdatesCatalog.cs`: saved updates, then a fresh check (COM, else command line) with step progress. Groups: ready, version not known, updated today. | 46.5.13, 46.5.16 | Saved open 0.3 s; fresh 4 s. |
| 46.7.11 | `Views/UpdatesPage.cs`: rewrite per `01-updates.html`: header, tiles, toolbar, groups, row states, empty state, error state. "Update" on one row starts at once. "Update all" and "Update selected" (2 or more) open `ConfirmDialog` with the app list. | 46.7.6, 46.7.7, 46.7.10 | Matches the mockup and `#confirm-all`. Update all fills the queue in order after the dialog. One-row Update shows no dialog. |
| 46.7.12 | `WingetRunner` progress: read the percent that winget prints for download and install. Pass it to the queue item. No guessed numbers (ADR 020, ADR 024). | 46.5.4, 46.5.5 | A real update shows the printed percent; an installer without numbers shows "Working". |
| 46.6.1 | `tools/check-catalog-picks.ps1`: read `catalog-picks.json`; run `winget show --id <id> --exact` for each id; print the ids that fail. | — | The script finds one planted bad id. |
| 46.6.2 | `Assets/catalog-picks.json`: about 120 ids in the 10 categories; 24 marked popular; tag for each category's "See all". Add the file to the csproj as content. | 46.6.1 | The script reports 0 failures. |
| 46.6.3 | `Services/Catalog/CatalogPicks.cs`: load and check the file; skip ids that fail `IsCatalogId`. | 46.6.2, 46.5.3 | Unit: bad id skipped; missing file gives an empty list, not a crash. |
| 46.6.4 | `Services/Catalog/MarketplaceCatalog.cs`: home model (picks plus Installed and Update state), search, tag list, details with a 7-day cache. Reports steps for the scan job. | 46.6.3, 46.5.13, 46.5.17 | Home in 0.5 s from saved data. Search 1.5 s. |
| 46.6.12 | `Services/Net/SafeIconFetcher.cs`: the internet icon rules in 5.6 (HTTPS, 3 redirects, maker domain, 256 KB image, 512 KB page, PNG, ICO or JPEG, no SVG, decode test, 5 s, no cookies). | — | Unit: HTTP refused; redirect to HTTP refused; other domain refused; 300 KB refused; SVG refused; a bad PNG refused; a subdomain of the maker domain accepted. |
| 46.6.5 | `Services/Icons/WingetManifestIconSource.cs`: Marketplace sources 0 and 1 in 5.6 (installed icon first; then the manifest `Icons` list, with the SHA-256 check). | 46.5.20, 46.5.13, 46.6.12 | Unit: a SHA-256 mismatch is refused. On PC: an installed pick shows its installed icon; a pick with manifest icons shows that icon. |
| 46.6.13 | `Services/Icons/MakerSiteIconSource.cs`: Marketplace source 2 in 5.6 (homepage `link` tags, `manifest.json` icons, `/favicon.ico`). Read the page as text only; never run scripts. | 46.6.12 | Unit with saved pages: apple-touch-icon wins; the largest `sizes` wins; manifest PNG used; favicon last. On PC: 20 picks show their own icon. |
| 46.6.6 | `Views/Controls/AppCard.cs`: card per `03-marketplace-home.html` with Install, Installed and Update states. | 46.7.4 | Matches the mockup. |
| 46.6.7 | `Views/Marketplace/MarketplaceHome.cs`: search box, category chips, popular grid, category rows with "See all". | 46.6.4, 46.6.6 | Matches the mockup. |
| 46.6.8 | `Views/Marketplace/MarketplaceResults.cs`: results list for search and "See all"; count line; sort; empty state. | 46.6.4, 46.7.6 | Matches `04-marketplace-search.html`. |
| 46.6.9 | `Views/Controls/DetailsPanel.cs`: icon 64 px, name, publisher, version, description, website, license, Install, Advanced (package id). | 46.6.4 | Matches the mockup. Opens in 1.0 s. |
| 46.6.10 | `Views/MarketplacePage.cs`: host for home and results; 250 ms typing pause; Esc clears and returns home. | 46.6.7–46.6.9 | Typing "zoo" then "zoom" runs one search only. |
| 46.6.11 | Install through `ActionQueue` with one click (no dialog, Q6); card and row states Waiting, Installing (percent when printed), Installed. The card keeps the catalog icon during the install, then changes to the installed icon. | 46.6.10, 46.7.12 | Live test per section 9. One click starts the install. |
| 46.7.22 | `Services/Scan/Jobs/UpdatesScanJob.cs`: wraps `UpdatesCatalog.RefreshAsync`; 3 steps; result "N updates ready" or "All apps are up to date". | 46.7.10, 46.7.19 | Unit with a fake catalog. On PC: the count equals the Updates screen. |
| 46.7.23 | `Services/Scan/Jobs/InstalledScanJob.cs`: wraps `InstalledCatalog.RefreshAsync`; 3 steps; result "N apps on this PC". | 46.5.17, 46.7.19 | Unit with a fake catalog. On PC: the count equals the Installed screen. |
| 46.7.24 | `Services/Scan/Jobs/MarketplaceScanJob.cs`: builds the home model; 2 steps; result "N popular apps in M categories". | 46.6.4, 46.7.19 | Unit with a fake catalog. |
| 46.7.25 | `Services/Scan/Jobs/IconScanJob.cs`: waits for the installed and Marketplace jobs; loads icons for every installed app and every pick; item count and percent; result "N of M original icons · K letter tiles". Job limit 8 s (OQ1 = A); icons that are not ready then keep loading after the scan. | 46.5.20, 46.6.13, 46.7.23, 46.7.24 | Unit: the count is real; the job ends at its limit. On PC with a warm cache: 1 s or less. |
| 46.7.26 | `Services/Scan/Jobs/DriveScanJob.cs`: `DiskService.Volumes()` and `PhysicalDisksAsync()`; 2 steps; result "N drives healthy · X GB free on C:". Read only. | 46.7.19 | On PC: the numbers match Windows Settings > Storage. |
| 46.7.27 | `Services/Scan/Jobs/SelfUpdateScanJob.cs`: wraps `UpdateService.CheckAsync`; no number; result "DeskVerve is up to date" or "DeskVerve X is ready". | 46.7.19 | Unit with a fake service. With the network off: Failed with a plain reason in 15 s or less. |

### Wave 5: start-up, icon coverage, finish

| Id | Task | After | Test |
|---|---|---|---|
| 46.7.28 | `Views/GettingReadyPage.cs`: rewrite per `00-getting-ready.html`: title, lead text, overall percent and bar, check count, time, one `ScanJobRow` for each job, finished state, "Continue". Announce changes to screen readers (live region). | 46.7.20, 46.7.22–46.7.27 | Matches both mockup states. A failed job shows its reason and "Try again". |
| 46.7.29 | `MainWindow.xaml.cs`: Getting ready at every start; sidebar locked until `Completed`; open Updates & Health after 1 s when every job is Done; stay with "Continue" when a job failed. | 46.7.28, 46.7.31 | Turn off the network: the scan ends, the sidebar unlocks, the app works. |
| 46.7.30 | Scan speed test: start DeskVerve 3 times on the owner's PC (warm cache). Record the full scan time and each job time from `SpeedLog`. | 46.7.29, 46.7.14 | Median full scan **5 s or less**. Write the times in section 12. |
| 46.7.21 | Icon coverage report: the start switch `--icon-report` writes `%LOCALAPPDATA%\QuantumUpdater\logs\icon-coverage.csv` (surface, app name, source used, size) and `icon-coverage.html` (a page with every icon, to look for wrong icons). On demand only. Local only; never sent. | 46.7.25 | On the owner's PC: **95 percent or more** of installed apps and **90 percent or more** of picks use an original source. Write the fallback list with reasons in section 12. |
| 46.7.14 | `Services/SpeedLog.cs`: write the section 6 timings and each scan job time to `%LOCALAPPDATA%\QuantumUpdater\logs\speed.log`. No names, no ids. | — | The log has one line for each moment in section 6. |
| 46.7.15 | Keyboard and screen reader pass: names on icon buttons; Ctrl+F to search; F5 to check again; Enter on a row opens details. | Wave 4 | Narrator reads each button and each scan row. |
| 46.7.16 | Words pass: friendly source names; no "winget" outside Advanced and logs; plain error text. | Wave 4 | Search the Views folder for "winget" in strings: Advanced only. |
| 46.7.17 | Remove `Services/CatalogSnapshot.cs` and its uses. | 46.5.21, 46.7.11, 46.7.29 | Build passes. |

**Later slices that add scan jobs:** 46.1 (Disk and Storage) can replace `DriveScanJob` with a full drive model. 46.2 (Cleanup) adds `CleanupEstimateScanJob` ("Space you can free"). The orchestrator does not change.

---

## 9. Risks

| Risk | Effect | Control |
|---|---|---|
| The COM API does not start in an admin process | No structured reads, because the app always runs as admin (Q2). With the text reader the scan takes about 6.5 s, so the 5 s target fails. | Spike 46.5.1 run 2 first. The command-line fallback (46.5.14) keeps the app working. If run 2 fails, the Manager tells the owner and asks again about Q2. |
| Update data does not come from COM | The Updates job uses the text reader | Spike 46.5.27 before 46.5.13. |
| A user uninstall entry runs with admin rights | Privilege risk | `NormalUserLauncher` (46.5.28); the planner never plans "run as admin" for HKCU; Security Architect review of 46.5.18, 46.5.19 and 46.5.28 before any live test. If the launcher fails, Windows Settings. |
| The full scan takes longer than 5 s | The owner waits at each start | All jobs at the same time; saved icons and Marketplace home; 15 s limit for each job; 46.7.30 measures it. |
| A scan job hangs | The app is trapped on Getting ready | 15 s limit for each job; Failed never blocks; the sidebar unlocks when every job is Done or Failed. |
| Icon coverage is below 95 or 90 percent | Listings look incomplete | Five installed sources and two Marketplace sources; 46.7.21 lists each fallback with a reason; the owner decides on more sources (section 10, open questions). |
| Maker sites keep icons on a CDN on another domain | Those Marketplace apps show letter tiles | Recorded in the 46.7.21 list. The domain rule stays (privacy). |
| A PC without App Installer (some Windows 10 1809 PCs) | No winget | Installed still works from Windows. The Updates and Marketplace jobs show Failed with "Microsoft App Installer is missing" and a link to get it. |
| The COM interop package does not match the App Installer version | Reads fail after a Windows update | Check the API version at start; use the fallback on error; log the error without personal data. |
| A code-only `ItemsRepeater` factory does not work | Long lists stay slow | Spike 46.7.1 first. Fallback: `ListView` with a `DataTemplate` from `XamlReader.Load`. |
| An app's own uninstall string runs a harmful program | Security risk | Strict rules in 5.2; never run user entries as admin; the Security Architect reviews 46.5.18 and 46.5.28 before any live test. |
| A "likely" name match picks the wrong app | Wrong app removed | A name match never selects the winget route. The dialog names the route. |
| Live tests remove the owner's real apps | Data loss | **CAUTION:** before each live install or uninstall test, run `winget list --id <id> --exact`. If the app is already installed, stop and pick another app. |
| Internet icons leak which apps a person looks at | Privacy | HTTPS only; the maker's own domain only; no outside icon service; 30-day cache (owner icon rule, ADR 049). |
| Curated picks go out of date | Broken cards | `tools/check-catalog-picks.ps1` before each release; a failed id is skipped, not shown. |
| Store app removal is for this user only | Other users keep the app | Dialog text says "for your account". |
| Sign-in on other PCs | The account service runs on this PC only (`http://127.0.0.1:8787`) | Q1 answered: sign-in is optional (46.7.31 to 46.7.35). Reports start when the public service is live (45.24). |

---

## 10. Fundamental changes for the owner

Each item below changes how DeskVerve works at its base. **The owner answered all 10 questions on 16 Nov 2026. ADR 049 is the source of truth.** The question text stays below as a record.

| Q | Owner answer (ADR 049) | Same as the recommendation? | Effect on this plan |
|---|---|---|---|
| Q1 | Sign-in is optional | Yes (A) | Tasks 46.7.31 to 46.7.35; section 5.9 |
| Q2 | Keep the admin prompt at every start | No (B) | 46.5.23 to 46.5.26 dropped; COM must work as admin (46.5.1 run 2); `NormalUserLauncher` 46.5.28; section 5.7 |
| Q3 | winget COM API for reads; text reader as the backup | Yes (A) | No change |
| Q4 | The app's own uninstaller, with strict rules and a dialog | Yes (A) | No change |
| Q5 | Stack delegated to the Manager: .NET 10, Windows App SDK 1.8, Windows 10 1809 or later, x64 and arm64 | Yes (A, and more) | "Before wave 1" note in section 8 |
| Q6 | One click for Update and Install; a dialog for Remove and Update all | Yes (A) | 46.7.7, 46.7.11, 46.6.11; mockup `01-updates.html#confirm-all` |
| Q7 | A full scan at every start, with progress for each job (owner rule) | No (new rule) | 46.7.13 dropped; 46.7.18 to 46.7.30; section 5.8; mockup `00-getting-ready.html` |
| Q8 | Answered by the icon rule: original icons from the app or the maker | Yes (A, and stricter) | Section 5.6; 46.5.20, 46.5.29 to 46.5.33, 46.6.5, 46.6.12, 46.6.13, 46.7.21 |
| Q9 | Wait for the installer before any public release | No (B) | No phase 46 task change. Phase 47 note below |
| Q10 | Close PR #480804 later; new id `DeskVerve.Utility` | Close: yes (B). Id: owner choice | Phase 47 note below |

### Q1. Sign in

**Owner answer:** A. Sign-in is optional.

**What it means:** Today DeskVerve does not open until a person signs in. The sign-in service runs only on this PC. On any other PC, sign-in cannot work, so a person who installs DeskVerve from winget cannot use it.

| Option | What it means for you |
|---|---|
| **A. Sign-in is optional (Recommended)** | DeskVerve opens and works at once. A "Sign in" button stays in the app. When the public sign-in service is ready (tracker 45.24), signed-in users send reports to your admin site. |
| B. Sign in one time on each PC, after the public service is ready | Nobody can use DeskVerve from winget until the service is live on quantumstep.in. That can take weeks. |
| C. Remove sign-in | The app is simplest. Your admin site gets no reports. |

**Why A:** It is the only option that lets other people use DeskVerve now, and it keeps your reports for later.

### Q2. Admin rights

**Owner answer:** B. Keep the admin prompt at every start. The COM read path must work as admin (spike 46.5.1 run 2). The text reader stays as the backup. The admin helper tasks are dropped.

**What it means:** Today Windows asks "Allow this app to make changes?" every time DeskVerve opens. Admin rights let DeskVerve install and remove apps for all users.

| Option | What it means for you |
|---|---|
| **A. Open as a normal app; ask for admin only when a change needs it (Recommended)** | No prompt at start, so the app opens fast. Windows asks one time when you first update, install or remove an app in a session. Reading and searching never ask. |
| B. Keep admin at every start (today) | One prompt each time you open the app. Some faster data methods may not work as admin, so lists can be slower. |
| C. Open as a normal app; Windows asks for each app | No admin helper to build. "Update all" can show one prompt for each app. |

**Why A:** The app opens with no delay, reading is fast and safe, and Update all still shows only one prompt.

### Q3. How DeskVerve reads app data

**Owner answer:** A.

**What it means:** Today DeskVerve reads the text that the winget tool prints, like reading a printed page. Long names get cut and many apps fall off the list. Microsoft also gives a direct data connection (the COM API) that gives exact data.

| Option | What it means for you |
|---|---|
| **A. Use Microsoft's direct data connection; keep text reading as a backup (Recommended)** | Exact lists, faster screens, no missing apps. If the connection fails on an old PC, the backup still works. |
| B. Keep text reading and fix the rules | Less work now. Lists can break again when winget changes its text. |
| C. Download and read the catalog file ourselves | Works offline. The file format is private and can change without notice. |

**Why A:** It removes the cause of the missing apps and it is the fastest option.

### Q4. Apps that winget does not know

**Owner answer:** A.

**What it means:** Many apps on a PC are not in the Microsoft catalog. DeskVerve will list them from the Windows installed-apps list. To remove them, DeskVerve must run the uninstaller that each app put on the PC.

| Option | What it means for you |
|---|---|
| **A. Remove them with the app's own uninstaller, with strict safety rules and a confirm dialog (Recommended)** | Every app on the PC can be removed from DeskVerve. Some uninstallers open their own window. |
| B. List them, but open Windows Settings to remove them | Safer and simpler. The person leaves DeskVerve to finish. |
| C. Remove only catalog apps | Many apps cannot be removed in DeskVerve. |

**Why A:** It gives one place to manage every app. The safety rules block unsafe uninstallers, and those apps fall back to option B.

### Q5. Move to .NET 10 now (security finding F1)

**Owner answer:** The owner gave the stack choice to the Manager. DeskVerve must run on every Windows desktop version from Windows 10 onward. Manager choice: .NET 10, Windows App SDK 1.8, minimum Windows 10 version 1809.

**What it means:** .NET is the base software that DeskVerve runs on. Microsoft stopped security fixes for .NET 8 on 10 Nov 2026. .NET 10 gets fixes until 2028.

| Option | What it means for you |
|---|---|
| **A. Move to .NET 10 before the Phase 46 work (Recommended)** | One extra day of work first. All new code is built and tested only once, on a safe base. |
| B. Move after Phase 46 | New features come a little sooner. Everything is tested two times. |
| C. Stay on .NET 8 | No security fixes for the base software. Not safe for a public release. |

**Why A:** Each later change is tested only once, and the public release is on a supported base.

### Q6. How DeskVerve asks before it changes apps

**Owner answer:** A. The mockups match: one-click Update and Install; dialogs at `02-installed.html#confirm` and `01-updates.html#confirm-all`. "Update selected" with 2 or more apps uses the Update all dialog.

**What it means:** Today each page has an "I confirm" tick box that you must tick before any button works. It is easy to miss, and it stays ticked.

| Option | What it means for you |
|---|---|
| **A. One click for Update and Install; a dialog for Remove and Update all (Recommended)** | Safe actions are fast. Actions that remove apps or change many apps always ask first. |
| B. A dialog for every action | Safest. More clicks. |
| C. Keep the tick box | No change. Slow and easy to get wrong. |

**Why A:** Updates and installs do not delete anything, so one click is safe. Removals always ask.

### Q7. The Getting ready screen

**Owner answer (new rule):** a full scan at every start. Every function waits for readiness. The screen shows progress for each job and an overall percent. See section 5.8 and `00-getting-ready.html`.

**What it means:** Today the app shows Getting ready and waits for every check before you can work.

| Option | What it means for you |
|---|---|
| **A. Show it only on the first run; later, open at once with saved results and refresh in the background (Recommended)** | The app is usable in about 1 second. A small "Checking" mark shows background work. |
| B. Show it every time, but skip it after 2 seconds | Faster than today, but still a wait. |
| C. Keep it as it is | About 7 seconds of waiting at each start. |

**Why A:** It matches your goal: fast and quick.

### Q8. App icons from the internet

**Owner answer:** the icon rule in ADR 049. Each app shows its own original icon. Marketplace icons come from the package or the maker's own site. A letter tile is the last resort. See section 5.6.

**What it means:** Apps that are not installed have no icon on the PC. DeskVerve can get the icon from the app maker's website.

| Option | What it means for you |
|---|---|
| **A. Get icons from the app maker's own website; save them for 30 days (Recommended)** | Real icons for most apps. DeskVerve contacts only the maker's own site, by a secure connection. |
| B. Ship icons for the popular picks inside DeskVerve; letters for all others | No internet use for icons. Search results show letter tiles. |
| C. Use an outside icon service | Easy, but a third company sees which apps people look at. |

**Why A:** Real icons make the store feel complete, and no third company sees the data.

### Q9. How DeskVerve is packaged for release

**Owner answer:** B. Wait for the installer. The first public release is a setup program or an MSIX package, not a portable zip. The Manager picks the installer tool in phase 47. Phase 46 tasks do not change. The portable zip stays only for private test copies and for the self-update path (ADR 026) until phase 47.

**What it means:** ADR 043 says the main download is an installer. An installer (MSIX or setup program) needs build tools that this PC does not have yet. A portable zip is a folder that runs without a setup step. winget can install a zip.

| Option | What it means for you |
|---|---|
| **A. Release a portable zip on winget now; add the installer later (Recommended)** | People can run `winget install` soon. The installer comes in phase 47 as a second choice. |
| B. Wait for the installer | Release waits for the build tools and more work. |
| C. Zip on GitHub only, no winget yet | Only people who find the GitHub page can get it. |

**Why A:** It is the fastest way to "anyone can run winget install", and the installer can come later without a break for users.

### Q10. The winget submission and the package name

**Owner answer:** close PR #480804 (the old Python 2.0.3) later. After the phase 46 fixes, the installer (phase 47) and owner acceptance, open a new request with:

- Package id: `DeskVerve.Utility` (winget ids cannot have spaces)
- Package name: "DeskVerve Utility"
- Moniker: `deskverve`, so a person can run `winget install deskverve`

The app window keeps the name "DeskVerve".

**What it means:** winget pull request #480804 adds `QuantumStep.QuantumUpdater` 2.0.3. That is the old Python app. The request passed the automatic checks and waits for a Microsoft reviewer. No public user has it yet. A package name (id) in winget is hard to change after it is public.

| Option | What it means for you |
|---|---|
| A. Change PR #480804 to version 3.0.0 (the new app) and keep the old id | Keeps the request open. The public name stays "QuantumUpdater" for good, or a rename later breaks updates for users. |
| **B. Close PR #480804. Open a new request for `QuantumStep.DeskVerve` 3.0.0 after the Phase 46 fixes and your acceptance (Recommended)** | The right name from the first day. The review restarts (new packages usually take some days). The first public version is the fixed, fast app. |
| C. Let 2.0.3 go public, then send 3.0.0 as an update | Faster first listing. The first public app is the old Python app, which is no longer the product. |

**Why B:** Nobody has the package yet, so a rename costs nothing today. Later it costs every user. The first public version should be the best one.

**Note (after the owner answers):** the recommended id `QuantumStep.DeskVerve` is replaced by the owner's id `DeskVerve.Utility`. Phase 47 must: build the installer (Q9); rename the program to `DeskVerve.exe`; keep the self-update path working for copies that use the old names (ADR 046); write the new manifests for `DeskVerve.Utility` (name "DeskVerve Utility", moniker `deskverve`); then close PR #480804 and open the new request. Q1 is A, so a public user can use DeskVerve with no account.

### Open questions after ADR 049

The Manager asks these one at a time. Each has a recommendation.

**OQ1. App icons on the first start.** On the first start, DeskVerve has no saved icons. It must get about 120 Marketplace icons from the internet. That can take more than 5 s.

| Option | What it means for you |
|---|---|
| **A. Wait up to 8 s for icons on the first start; the rest load in the next seconds (Recommended)** | The first start is a little longer. A few cards show a letter for some seconds, then the real icon. Later starts use saved icons and stay near 5 s. |
| B. Wait for every icon on the first start | Every icon is ready when the menu opens. The first start can take 15 s or more on a slow network. |
| C. Ship the icons of the popular picks inside DeskVerve | The first start is fast and works offline. The icons are the makers' artwork inside our download, so each pick needs a check that its maker allows this. |

**Why A:** it keeps the start fast and still shows each original icon within seconds.

**OQ2. The "Space you can free" check.** The cleanup estimate is part of Cleanup (46.2), which comes after this work.

| Option | What it means for you |
|---|---|
| **A. Hide the row until Cleanup (46.2) is built (Recommended)** | Getting ready shows only checks that work. The row appears when 46.2 is done. |
| B. Show the row now with "Coming soon" | You see the full plan on screen, but one row does nothing. |

**Why A:** a row that does nothing makes the app look unfinished.

**Manager decision (not for the owner):** approve or reject the Start menu shortcut icon source (5.6, source 5, task 46.5.33). It is a Design Architect addition to the owner's source list. It raises icon coverage. It reads only local shortcut files.

---

## 11. Design system additions (`Theme.cs`)

| Token | Value | Use |
|---|---|---|
| `CardHover` | `#333333` | Row and card hover |
| `Subtle` | `#1C1C1C` | List header, toolbar background |
| `Chip` | `#383838` | Chip background |
| `TextSecondary` | `#9E9E9E` | Publisher, captions |
| `AccentHover` | `#1A86D9` | Accent button hover |
| `AccentPressed` | `#006CBE` | Accent button pressed |
| `AccentText` | `#60B0F0` | Links and new version text on dark |
| `CriticalText` | `#FF99A4` | Error text and Remove button text on dark |
| Spacing | 4, 8, 12, 16, 24, 32 | Gaps and padding |
| Corner radius | 4 (buttons, chips), 8 (cards, dialogs) | Shapes |
| Type sizes | 28 title, 20 section, 14 body, 12 caption | Text |

Existing tokens (`Canvas #131313`, `Sidebar #272727`, `Card #2C2C2C`, `Stroke #383838`, `Accent #0078D4`, `Healthy #27AC62`, `Warning #D8A000`, `Critical #E81123`) do not change.

## 12. Spike and measurement results

| Check | Normal user | Admin |
|---|---|---|
| COM start | OK, 39 ms | Run 2 pending |
| Installed packages | 238 (installed catalog only) | Run 2 pending |
| With `ARP\` or `MSIX\` local id | ARP 98, MSIX 140 | Run 2 pending |
| With an update | Not measured (see notes below; spike 46.5.27) | Run 2 pending |
| Time | Total run 1868 ms; `winget list` 2377 ms | Run 2 pending |
| `ItemsRepeater` 2,000 rows: smooth, memory | Run 1: Rows created 1,654 (limit below 60). Memory 184.9 MB (limit 150 MB or less). Pass/Fail: Fail. Run 2: Rows created 47 (limit below 60). Memory 120.3 MB (limit 150 MB or less). Pass/Fail: Pass. Smoothness: owner or Manager to look. | n/a |


**46.5.1 result (16 Nov 2026). Normal user run only.**

- Package: `Microsoft.WindowsPackageManager.ComInterop` version `1.29.380` (latest stable).
- Target: `net8.0-windows10.0.26100.0`. The package does not support `net8.0-windows10.0.19041.0` (restore error NU1202).
- COM start: OK. The package default in-proc factory (`FactoryLinkage` = `static`) was used.
- Composite catalog (installed plus winget, `LocalCatalogs`): not usable. `FindPackages` returns `InvalidOptions`, with empty options and with an `Id` filter, in both catalog orders.
- Other `CompositeSearchBehavior` values: `RemotePackagesFromRemoteCatalogs` returns `Ok`, but it searches the winget source, not the installed apps. Not used.
- Result used: installed catalog only. It gives 238 packages: 98 with `ARP\`, 140 with `MSIX\`, 0 other.
- Update count: not measured. The installed catalog has no winget source data, so no available version. Task 46.5.13 must fix the composite.
- Times: COM start 39 ms; catalog connect 251 ms; list 159 ms; id count 173 ms; total run 1868 ms.
- `winget list --disable-interactivity --accept-source-agreements`: 2377 ms, 238 data rows, exit code 0.
- Pass/Fail: **Pass.** Count 238 is about 236. Time 1.9 s is 4 s or less. Update part still open.

**46.5.1 run 2 result (16 Nov 2026). Normal user run only.** Spike code: `tools/spikes/WingetComSpike/` on .NET 10.

- Target: `net10.0-windows10.0.26100.0`. Build passed with 0 warnings.
- Composite catalog: **Ok.** Only the remote `OpenWindowsCatalog` source was added. `CompositeSearchBehavior` is `LocalCatalogs`. Connect took 1851 ms.
- Find attempts. All returned Ok:
  - A, sync, no filter: 214 matches, 710 ms. **Used.**
  - B, async, no filter: 214 matches, 510 ms.
  - C, sync, Id contains empty text: 214 matches, 520 ms.
  - D, sync, Id contains `.`: 180 matches, 248,522 ms (about 4 min). Do not use.
  - E, async, Id contains empty text: 214 matches, 555 ms.
  - F, async, Id contains `.`: 180 matches, 243,039 ms (about 4 min). Do not use.
- Counts from attempt A:
  - Total packages: 214. Local id `ARP\`: 45. `MSIX\`: 96. Other: 73. Run 1 (installed only) gave 238.
  - `IsUpdateAvailable`: **1**.
  - Known to winget (`DefaultInstallVersion` not null): 73. Not known: 141.
- First packages with an update (only one row):
  - Java(TM) SE Development Kit 25 | Oracle.JDK.25 | installed 25.0.5.0 | available 25.0.5.0.3
- `winget upgrade --include-unknown --disable-interactivity --accept-source-agreements`: exit code 0. 1 data row. Footer: `1 upgrades available.` Same package and versions. Wall time 2649 ms.
- Times from attempt A: COM start 451 ms; composite connect 1851 ms; find 710 ms; id count 368 ms; update count 3364 ms. Total run 499,901 ms, because of attempts D and F.
- Read path without attempts D and F: about 6.7 s. Target for 46.5.13 is 4 s or less. The update count step is the largest part.
- Pass/Fail for the update part: update count matches winget (1 and 1): **Pass.**
- Count check: 214 is not about 236. Cause not checked: **Manager to check.**
- Time check: about 6.7 s is over 4 s: **Manager to check.**
- Problem: the `.` selector attempts take about 4 min each. Do not use a `.` selector.

- Run 2 (admin): **pending.** It did not complete on 16 Nov 2026. Q2 keeps admin at every start, so run 2 is required before task 46.5.13.

**Later results (fill in):**

| Check | Result |
|---|---|
| 46.5.27 update data way and count | Pending |
| 46.7.30 full scan time (median of 3 starts) | Pending |
| 46.7.21 icon coverage, installed (target 95 percent) | Pending |
| 46.7.21 icon coverage, picks (target 90 percent) | Pending |
| 46.7.21 apps that fall back to a letter tile, with reasons | Pending |

## 13. Diagram updates after this work

- System picture: add `WingetLocator`, `WingetRunner`, `WingetReader` (COM) with `WingetCliReader` fallback, `ActionQueue`, `SnapshotCache`, the inventory group, `NormalUserLauncher`, the icon pipeline (`IconService`, five installed sources, two Marketplace sources, `SafeIconFetcher`), and `ScanOrchestrator` with its six jobs. No admin helper (Q2).
- Screen flow: admin prompt, then Getting ready at every start; the sidebar unlocks when every job is Done or Failed; Updates opens by itself when every job is Done; the activity bar on every screen; the uninstall dialog with four routes; the Update all dialog; optional sign-in from the sidebar footer.
- Remove `CatalogSnapshot` from the picture after 46.7.17.
- Update the "Live colours" section with the new tokens in section 11.
- After 46.2: add `CleanupEstimateScanJob` to the scan picture.

The planned diagrams are in `docs/ARCHITECTURE.md`, section "Planned: Phase 46 priority". Move them to the built section after the Design Architect gives PASS.

## 14. Validation checklist (Design Architect, after the build)

1. Installed shows 200 or more rows on this PC, with `ARP` and Store apps.
2. Each source chip count adds up to the total.
3. Each uninstall route shows the right dialog text.
4. Marketplace home shows popular picks and 10 categories before any search.
5. Search "zoom" and the category "Browsers" show full names and no cut rows.
6. Updates matches `01-updates.html` in each row state.
7. One action runs at a time; Cancel stops it.
8. The speed log meets section 6.
9. No "winget" in normal screen text.
10. Colours and sizes match section 11 and the mockups.
11. Getting ready matches `00-getting-ready.html` in both states. All jobs run at the same time. The median full scan is 5 s or less (46.7.30).
12. With the network off, every job ends in 15 s or less, and the sidebar unlocks.
13. Icon coverage: 95 percent or more of installed apps and 90 percent or more of picks show an original icon (46.7.21). The fallback list is in section 12.
14. The app opens and works signed out. Signed out, it makes no account calls.
15. One click starts Update and Install. Remove, Update all and Update selected (2 or more) open a dialog.
16. A user uninstall entry runs without admin rights (`TokenElevation` = 0).
