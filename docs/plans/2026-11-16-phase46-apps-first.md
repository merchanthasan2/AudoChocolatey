# Phase 46 priority plan: apps first (46.5, 46.6, 46.7)

- **Status:** Proposed, 16 Nov 2026. Author: Design Architect.
- **Needs:** Manager review. Owner answers to section 10, one question at a time.
- **Rules:** ADR 048 (scope and order), ADR 043 (WinUI, Stitch shell, one service layer), ADR 020 and ADR 024 (show only numbers that an installer printed), ADR 046 (compatibility names stay until phase 47).
- **Mockups:** `design/mockups/phase46/index.html`.
- **Architecture:** `docs/ARCHITECTURE.md`, section "Planned: Phase 46 priority".

---

## 1. Goal

The owner wants the best-functioning app: fast, quick and easy to use.

This plan fixes the three problems that the owner reported:

1. **Installed** does not show all apps on the PC. (Task 46.5.)
2. **Marketplace** shows only a few apps. (Task 46.6.)
3. **Updates** looks and feels poor. (Task 46.7.)

The plan also folds in the security fixes for this code: F3, F4, F16, F18, F24 (tracker 45.17) and F19.

## 2. Scope

**In scope**

- Installed: every app on the PC, with real icons and a safe uninstall for each app.
- Marketplace: the full Microsoft catalog (winget community source), with a home page, categories, popular apps, search, a details panel and real icons.
- Updates: a new screen design, Update all, one action at a time, progress for each app.
- A shared look for the three screens: rows, cards, chips, a bottom activity bar, dialogs.
- Saved results (cache), so each screen opens at once and refreshes in the background.
- One hardened winget runner (tracker 45.17).

**Out of scope (later phases)**

- Chocolatey as a second catalog (ADR 048 says later).
- Disk and Storage (46.1) and the Cleanup set (46.2).
- Installer and Store packaging (phase 47).
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
| Slow screens | Each screen runs winget again (4 to 7 s). Getting ready waits for every scan before the app opens. |
| Security (45.17) | winget is found through `PATH` (F3). Copied process code in three services (F24). Cancel does not stop winget (F16). Two actions can run at the same time (F18). |

---

## 5. Approach

### 5.1 How DeskVerve reads winget data (decision)

| Option | Good | Bad | Decision |
|---|---|---|---|
| **A. winget COM API** (`Microsoft.Management.Deployment`, the API that the `Microsoft.WinGet.Client` module uses) | Official. Structured data. Full ids. Local `ARP\` and `MSIX\` ids, product codes and package family names. Tag search. One call gives installed apps and updates. No text parsing. | Needs an interop NuGet package. An elevated (admin) process needs a different start route. It depends on the App Installer version. | **Chosen for reading** |
| B. Read the winget source index file (`index.db`) | The full list offline. Tags. | A private file format that can change at any time. DeskVerve must download and check the file. It does not link installed apps. | Rejected |
| C. winget text or JSON from the command line | Already built. | winget 1.30 has no JSON for list, search or upgrade. `export` JSON skips apps that winget does not know. Text columns shift and cut. | **Fallback for reading only** |
| D. PowerShell `Microsoft.WinGet.Client` | Objects. | The module must come from the PowerShell Gallery. Each call starts PowerShell (1 to 2 s). One more dependency. | Rejected |

**Decision:**

- **Read** with the COM API (option A). This gives lists, search, tags and details.
- **Change** (install, upgrade, uninstall) with the hardened winget command (`WingetRunner`). The command can run as admin, DeskVerve can stop it, and it prints the installer percent.
- **Fallback:** if the COM API does not start, DeskVerve reads with the command line and one shared text parser. The Installed list stays complete, because the Windows list (5.2) does not need winget.
- **Spike first** (task 46.5.1). The spike proves the COM API in a normal-user process and in an admin process. The result can change the admin model answer (section 10, Q2).

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
| MSI registry entry | `%SystemRoot%\System32\msiexec.exe /x{GUID}` | The GUID comes from the key or the uninstall string. Add `/passive` only when DeskVerve has admin rights. |
| Other registry entry | `QuietUninstallString`, else `UninstallString` | Strict rules below. The app's own uninstaller can open a window. |
| Store app | `PackageManager.RemovePackageAsync(fullName)` | For this user only. |
| "Likely" match only (4) | The registry route, not winget | A name match never selects the winget route. |
| Protected or no safe route | No uninstall | Show "Open Windows Settings" (`ms-settings:appsfeatures`). |

**Strict rules for an app's own uninstall string** (idea from `archive/python-app/core/arp_inventory.py`; new C# code):

- The program path must be absolute and the file must exist.
- Machine entries (HKLM): the program must be under `%ProgramFiles%`, `%ProgramFiles(x86)%`, `%SystemRoot%\Installer` or `%ProgramData%\Package Cache`.
- User entries (HKCU): the program must be under `%LOCALAPPDATA%` or `%APPDATA%`. **Never run a user entry with admin rights.** A user can change these files, so admin rights here are a privilege risk.
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
- **Confirm:** remove the confirm box at the top of each page. Use a dialog (section 10, Q6).
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

- **Getting ready** shows only on the first run (no saved results). Later starts open on Updates at once (section 10, Q7).

### 5.6 Icons

| Row type | Icon source | Rules |
|---|---|---|
| Registry app | `DisplayIcon` path, else the main program in `InstallLocation` | Windows file thumbnail (`StorageFile.GetThumbnailAsync`). Local files only. |
| Store app | Package logo file (`Package.Logo`) | Local file only. |
| Catalog app (not installed) | Manifest icon when the manifest has one; else the publisher website icon | HTTPS only. Same website as the package or publisher address. Up to 3 HTTPS redirects. Up to 256 KB. PNG or ICO only, no SVG. Decode test before use. |
| No icon found | Letter tile | First letter of the name on a colour from the name. |

- Cache: in memory (500 icons) and on disk in `%LOCALAPPDATA%\QuantumUpdater\icons\` for 30 days. A "not found" result is saved for 7 days.
- Icons load after the row shows. An icon never holds a row or a screen.
- Owner choice on internet icons: section 10, Q8.

### 5.7 Admin rights (depends on section 10, Q2)

The recommended model (Q2 = A):

- DeskVerve opens as a normal user (`asInvoker`). No admin prompt at start.
- Reading lists, searching, Store app removal and user-scope uninstalls run in the normal window.
- Install, upgrade and machine-scope uninstall go to an **admin helper**. The helper is the same program started with `--worker`. Windows asks for admin one time. The helper stays open until DeskVerve closes or for 10 idle minutes.
- The helper talks to the window through a named pipe. Only the same Windows user can open the pipe. The helper checks that the caller is DeskVerve from the same folder.
- The helper checks every request again. It accepts only catalog ids that pass the id rule. For an uninstall it gets a registry reference, reads the key itself, and makes its own plan. It never runs a command line that the window sends.

---

## 6. Speed targets

Times are on this PC, from click to the result on screen.

| Moment | Today | Target |
|---|---|---|
| App start to a usable screen (with saved results) | Admin prompt, then Getting ready waits for all scans (about 7 s) | **1.0 s** |
| App start, first run (no saved results) | About 7 s | Windows list in **1.5 s**; updates in **5 s** |
| Updates screen opens (saved results) | Runs winget again when there is no snapshot | **0.3 s** |
| Fresh update check (background) | 6.4 s | **4 s** (COM); screen stays usable |
| Installed screen opens (saved results) | Runs winget again (4.5 s) | **0.3 s** |
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

Task 46.7.14 adds a speed log that records these times. The log has no personal data.

---

## 7. Screens, flows and files

**Screens touched:** Updates and Health, Installed, App Marketplace (home, results, details), Getting ready, the window shell (sidebar, activity bar). Sign in only if Q1 changes it.

**Flows touched:** app start, check for updates, update one, update all, cancel, install, uninstall (four routes), search, browse a category, refresh after an action.

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
| `Services/IconService.cs` | Icons with cache |
| `Services/SpeedLog.cs` | Timings, no personal data |
| `Services/Elevation/WorkerProtocol.cs`, `WorkerClient.cs`, `ElevatedWorker.cs` | Admin helper (only if Q2 = A) |
| `Models/AppEntry.cs`, `CatalogApp.cs`, `UpgradeEntry.cs`, `ActionItem.cs` | Data records |
| `Views/Controls/AppIcon.cs`, `AppRow.cs`, `AppCard.cs`, `VirtualList.cs`, `ConfirmDialog.cs`, `ActivityBar.cs`, `DetailsPanel.cs` | Shared screen parts |
| `Views/Marketplace/MarketplaceHome.cs`, `MarketplaceResults.cs` | Marketplace parts |
| `Assets/catalog-picks.json` | Curated picks and categories |
| `tools/check-catalog-picks.ps1` (repo root) | Check every pick id |
| `tools/spikes/WingetComSpike/` (repo root) | Spike project, not shipped |

**Changed files:** `Theme.cs`, `Chrome.cs`, `MainWindow.xaml.cs`, `Program.cs`, `app.manifest` (only if Q2 = A), `QuantumUpdater.csproj`, `Services/AppServices.cs`, `Services/WingetService.cs`, `Services/InstalledService.cs`, `Services/MarketplaceService.cs`, `Views/UpdatesPage.cs`, `Views/InstalledPage.cs`, `Views/MarketplacePage.cs`, `Views/GettingReadyPage.cs`.

**Retired:** `Services/CatalogSnapshot.cs` (replaced by `InstalledCatalog` and `UpdatesCatalog`). The three copies of process code and text parsing (F24).

---

## 8. Task list

Each task changes one file or makes one small change. Do the tasks in wave order. A task starts only when the tasks in "After" are done and verified. Ids follow the tracker: 46.5.x (Installed and shared base), 46.6.x (Marketplace), 46.7.x (Updates and look and feel).

**Before wave 1:** 45.20 (.NET 10) is done or the owner chose to wait (Q5). 45.18 (session check) is independent.

### Wave 0: spikes (prove the risky parts)

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.1 | Make `tools/spikes/WingetComSpike/` (console). Reference the winget COM interop NuGet package. Print: COM start OK or error; count of installed packages; count with an `ARP\` or `MSIX\` local id; count with an update; time for each step. Run it as a normal user and as admin. | — | Write both results in section 12 of this file. Pass = normal-user run lists about 236 packages in 4 s or less. |
| 46.7.1 | In the spike folder, make a small WinUI window with `ItemsRepeater` and a code-only `IElementFactory`. Show 2,000 fake rows with an image and two text lines. | — | Scroll is smooth. Memory 150 MB or less. Rows recycle (log count of created rows stays below 60). |

### Wave 1: base services (security fixes 45.17)

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.2 | `Services/Winget/WingetLocator.cs`: find the App Installer package `Microsoft.DesktopAppInstaller_8wekyb3d8bbwe` with `PackageManager`. Return `<InstalledLocation>\winget.exe` when the file exists and the path is under `%ProgramFiles%\WindowsApps`. Do not read `PATH`. (F3) | — | Unit: a fake path outside WindowsApps returns null. On PC: returns the App Installer path. A `winget.exe` put in a `PATH` folder is not used. |
| 46.5.3 | `Services/Winget/PackageId.cs`: `IsCatalogId` (the current rule, for actions) and `IsLocalId` (`ARP\` or `MSIX\` prefix, no control characters or `"<>|`, up to 400 characters, display and matching only). | — | Unit: 10 good and 10 bad ids for each rule. A local id never passes `IsCatalogId`. |
| 46.5.4 | `Services/Winget/WingetRunner.cs`: one method `RunAsync(args, timeout, onLine, token)`. Argument list only. Streams each output line to `onLine`. Kills the process tree on cancel and on timeout (F16). Adds `--disable-interactivity` and `--accept-source-agreements`. Adds `--exact` whenever `--id` is in the arguments (F4). | 46.5.2 | Unit with a test program that sleeps 30 s: cancel ends it in 2 s or less. Lines arrive in order. Exit code returns. |
| 46.5.5 | `Services/ActionQueue.cs`: first in, first out. One item runs at a time (F18). States: Waiting, Working, Done, Failed, Cancelled. Percent is null unless the work reports it. Events reach the screen thread through `DispatcherQueue`. Cancel a waiting item removes it; cancel the working item cancels its token. | — | Unit: 3 items never overlap; order kept; cancel both ways; a failed item does not stop the next one. |
| 46.5.6 | `Services/Winget/WingetTextTable.cs`: move the one text parser here. Keep `ARP\` and `MSIX\` rows as local ids. Mark a cut id (ends with "…" or "...") as `IsCut` and never use it for an action. | 46.5.3 | Unit with `_winget_sample.txt` and today's `winget list` text: no row is dropped; cut rows are marked. |
| 46.5.7 | `Services/WingetService.cs`: use `WingetLocator`, `WingetRunner` and `WingetTextTable`. Delete its own process and parse code (F24). | 46.5.4, 46.5.6 | Build passes. Update check still lists the same updates as `winget upgrade`. |
| 46.5.8 | `Services/InstalledService.cs`: the same change as 46.5.7. | 46.5.7 | Build passes. No duplicate process code (search for `BuildStartInfo` finds one file only). |
| 46.5.9 | `Services/MarketplaceService.cs`: the same change as 46.5.7. | 46.5.7 | Build passes. Search "firefox" returns Mozilla Firefox. |

### Wave 2: data

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.10 | `Models/AppEntry.cs`: record with Key, Name, Publisher, Version, Source (`MicrosoftCatalog`, `MicrosoftStore`, `InstalledDirectly`), CatalogId, RegistryRef (hive, view, key), StoreFullName, Match (`Exact`, `Likely`, `None`), Route (`Winget`, `Msi`, `OwnUninstaller`, `Store`, `None`), IsProtected, SizeBytes, InstallDate, UpdateVersion. | — | Build passes. |
| 46.5.11 | `Services/Inventory/RegistryInventory.cs`: read the four views (5.2). Apply the filter. Return rows with the registry reference. | 46.5.10 | On PC: 98 rows or more; contains a known desktop app; 0.2 s or less. |
| 46.5.12 | `Services/Inventory/StoreAppInventory.cs`: read Store apps for this user. Apply the filter (5.2). | 46.5.10 | On PC: about 90 rows; Calculator present; Settings hidden; 0.8 s or less. |
| 46.5.13 | `Services/Winget/WingetReader.cs`: COM reads. `ListInstalledAsync` (local id, catalog id, name, version, product codes, family names, update version). `SearchAsync(text, limit)`, `ByTagAsync(tag, limit)`, `DetailsAsync(id)`. | 46.5.1 pass | On PC: installed count matches `winget list` within 2; search "zoom" returns Zoom.Zoom; 4 s or less. |
| 46.5.14 | `Services/Winget/WingetCliReader.cs`: the same four reads with `WingetRunner` and `WingetTextTable`. Used only when COM fails. | 46.5.6 | Force the fallback with a test switch. Same counts as 46.5.13 within 5. |
| 46.5.15 | `Services/Inventory/InstalledMerger.cs`: the match order and de-duplication in 5.2. | 46.5.11–46.5.13 | Unit with fixtures: ARP match; product code match; MSIX match; name-only match is `Likely`; 32-bit and 64-bit twin becomes one row; winget-only portable app stays. On PC: total rows 200 or more. |
| 46.5.16 | `Services/SnapshotCache.cs`: `Load<T>` and `Save<T>` with schema version, saved time, temporary file then replace, 20 MB limit, bad file deleted. | — | Unit: round trip; bad file; old schema ignored. |
| 46.5.17 | `Services/InstalledCatalog.cs`: `LoadSaved()` at once; `RefreshAsync()` reads registry and Store apps at the same time, then winget links; saves; raises `Changed`. | 46.5.15, 46.5.16 | Open with saved data in 0.3 s. Fresh Windows list in 1.0 s. |
| 46.5.18 | `Services/Inventory/UninstallPlanner.cs`: the routes and strict rules in 5.2. Returns a plan (program, arguments, run as admin yes or no) or "no safe route". | 46.5.10 | Unit: 20 uninstall strings (MSI, quoted EXE, unquoted EXE with spaces, `cmd /c`, URL, `%TEMP%` path, HKCU path with admin) give the expected plan or refusal. |
| 46.5.19 | `Services/Inventory/UninstallRunner.cs`: run a plan through `ActionQueue`; 15 minute limit; kill on cancel; read the key again; 3010 = restart needed. | 46.5.5, 46.5.18 | Unit with a fake plan. On PC: see the live-test rule in section 9. |
| 46.5.20 | `Services/IconService.cs` (installed icons): `DisplayIcon` or main program thumbnail; Store logo; memory and disk cache. | 46.5.10 | On PC: 30 known apps show real icons; saved icon loads in 0.05 s. |

### Wave 3: shared screen parts (look and feel)

| Id | Task | After | Test |
|---|---|---|---|
| 46.7.2 | `Theme.cs`: add tokens (section 11). | — | Build passes. Old tokens keep their values. |
| 46.7.3 | `Chrome.cs`: add `SecondaryButton`, `DangerButton`, `Chip`, `Caption`, `SectionHeader`, `SearchBox`, `LetterTile`. | 46.7.2 | Compare with the mockups at 100 percent zoom. |
| 46.7.4 | `Views/Controls/AppIcon.cs`: 32 px or 48 px image; letter tile until the icon loads; safe when the row recycles. | 46.5.20, 46.7.3 | Fast scroll shows no wrong icons. |
| 46.7.5 | `Views/Controls/AppRow.cs`: icon, name, publisher, version area, chips, action button, inline progress, states (5.4). | 46.7.4 | Each state matches `01-updates.html`. |
| 46.7.6 | `Views/Controls/VirtualList.cs`: `ItemsRepeater` with the factory from spike 46.7.1. | 46.7.1, 46.7.5 | 2,000 rows scroll smoothly. |
| 46.7.7 | `Views/Controls/ConfirmDialog.cs`: code-only `ContentDialog` with `XamlRoot`, title, body, Cancel and one main button. For Remove, the main button is red and the default button is Cancel. | 46.7.3 | Matches the dialog in `02-installed.html`. Esc cancels. |
| 46.7.8 | `Views/Controls/ActivityBar.cs`: bound to `ActionQueue`. Current item, "N of M", percent only when reported, "View all" flyout, "Cancel". Hidden when the queue is empty. | 46.5.5, 46.7.3 | Matches the bar in `01-updates.html`. |
| 46.7.9 | `MainWindow.xaml.cs`: sidebar icons, accent mark, update badge; add the activity bar row; open on Updates when saved results exist. | 46.7.8 | Matches the shell in all mockups. |

### Wave 4: screens

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.21 | `Views/InstalledPage.cs`: rewrite per `02-installed.html`. Search box, source chips with counts, sort, virtual list, "Protected" rows, Uninstall button. | 46.5.17, 46.7.6 | All rows from 46.5.15 show. Typing filters in 0.1 s. |
| 46.5.22 | `Views/InstalledPage.cs`: Uninstall opens `ConfirmDialog` with the app name and the route text; then the queue. Row shows "Removing · Working"; the row leaves the list after success. | 46.5.19, 46.5.21, 46.7.7 | Live test per section 9. |
| 46.7.10 | `Services/UpdatesCatalog.cs`: saved updates, then a fresh check (COM, else command line). Groups: ready, version not known, updated today. | 46.5.13, 46.5.16 | Saved open 0.3 s; fresh 4 s. |
| 46.7.11 | `Views/UpdatesPage.cs`: rewrite per `01-updates.html`: header, tiles, toolbar, groups, row states, empty state, error state. | 46.7.6, 46.7.10 | Matches the mockup. Update all fills the queue in order. |
| 46.7.12 | `WingetRunner` progress: read the percent that winget prints for download and install. Pass it to the queue item. No guessed numbers (ADR 020, ADR 024). | 46.5.4, 46.5.5 | A real update shows the printed percent; an installer without numbers shows "Working". |
| 46.6.1 | `tools/check-catalog-picks.ps1`: read `catalog-picks.json`; run `winget show --id <id> --exact` for each id; print the ids that fail. | — | The script finds one planted bad id. |
| 46.6.2 | `Assets/catalog-picks.json`: about 120 ids in the 10 categories; 24 marked popular; tag for each category's "See all". Add the file to the csproj as content. | 46.6.1 | The script reports 0 failures. |
| 46.6.3 | `Services/Catalog/CatalogPicks.cs`: load and check the file; skip ids that fail `IsCatalogId`. | 46.6.2, 46.5.3 | Unit: bad id skipped; missing file gives an empty list, not a crash. |
| 46.6.4 | `Services/Catalog/MarketplaceCatalog.cs`: home model (picks plus Installed and Update state), search, tag list, details with a 7-day cache. | 46.6.3, 46.5.13, 46.5.17 | Home in 0.5 s from saved data. Search 1.5 s. |
| 46.6.5 | `Services/IconService.cs` (catalog icons): manifest icon, else publisher site icon, with the rules in 5.6. | 46.5.20 | Unit: HTTP refused; other domain refused; 300 KB refused; SVG refused. On PC: 20 picks show icons. |
| 46.6.6 | `Views/Controls/AppCard.cs`: card per `03-marketplace-home.html` with Install, Installed and Update states. | 46.7.4 | Matches the mockup. |
| 46.6.7 | `Views/Marketplace/MarketplaceHome.cs`: search box, category chips, popular grid, category rows with "See all". | 46.6.4, 46.6.6 | Matches the mockup. |
| 46.6.8 | `Views/Marketplace/MarketplaceResults.cs`: results list for search and "See all"; count line; sort; empty state. | 46.6.4, 46.7.6 | Matches `04-marketplace-search.html`. |
| 46.6.9 | `Views/Controls/DetailsPanel.cs`: icon 64 px, name, publisher, version, description, website, license, Install, Advanced (package id). | 46.6.4 | Matches the mockup. Opens in 1.0 s. |
| 46.6.10 | `Views/MarketplacePage.cs`: host for home and results; 250 ms typing pause; Esc clears and returns home. | 46.6.7–46.6.9 | Typing "zoo" then "zoom" runs one search only. |
| 46.6.11 | Install through `ActionQueue`; card and row states Waiting, Installing (percent when printed), Installed. | 46.6.10, 46.7.12 | Live test per section 9. |

### Wave 5: admin model, start-up, finish

| Id | Task | After | Test |
|---|---|---|---|
| 46.5.23 | `Services/Elevation/WorkerProtocol.cs`: request and reply records (upgrade id, install id, uninstall registry reference, cancel); JSON with a strict schema. Only if Q2 = A. | — | Unit: an unknown field or a bad id is refused. |
| 46.5.24 | `Services/Elevation/ElevatedWorker.cs` and the `--worker` branch in `Program.cs`: pipe for this user only; check the caller path; check every request again; plan uninstalls itself. Only if Q2 = A. | 46.5.23 | Unit: a request with a command line is refused. A second program cannot connect. |
| 46.5.25 | `Services/Elevation/WorkerClient.cs`: start the helper with one admin prompt; reuse it; restart it if it stops. Only if Q2 = A. | 46.5.24 | Update all with 3 machine apps shows one admin prompt. |
| 46.5.26 | `app.manifest`: `asInvoker`. Only if Q2 = A, and only after 46.5.25 passes. | 46.5.25 | The app opens with no admin prompt. |
| 46.7.13 | `Views/GettingReadyPage.cs` and `MainWindow.xaml.cs`: Getting ready shows only on the first run (Q7). | 46.5.17, 46.7.10 | Second start opens on Updates in 1.0 s. |
| 46.7.14 | `Services/SpeedLog.cs`: write the section 6 timings to `%LOCALAPPDATA%\QuantumUpdater\logs\speed.log`. No names, no ids. | — | The log has one line for each moment in section 6. |
| 46.7.15 | Keyboard and screen reader pass: names on icon buttons; Ctrl+F to search; F5 to check again; Enter on a row opens details. | Wave 4 | Narrator reads each button. |
| 46.7.16 | Words pass: friendly source names; no "winget" outside Advanced and logs; plain error text. | Wave 4 | Search the Views folder for "winget" in strings: Advanced only. |
| 46.7.17 | Remove `Services/CatalogSnapshot.cs` and its uses. | 46.5.21, 46.7.11 | Build passes. |

---

## 9. Risks

| Risk | Effect | Control |
|---|---|---|
| The COM API does not start in an admin process | No structured reads while the app runs as admin | Spike 46.5.1 first. Q2 = A removes the problem. The command-line fallback (46.5.14) keeps the app working. |
| The COM interop package does not match the App Installer version | Reads fail after a Windows update | Check the API version at start; use the fallback on error; log the error without personal data. |
| A code-only `ItemsRepeater` factory does not work | Long lists stay slow | Spike 46.7.1 first. Fallback: `ListView` with a `DataTemplate` from `XamlReader.Load`. |
| An app's own uninstall string runs a harmful program | Security risk | Strict rules in 5.2; never run user entries as admin; the Security Architect reviews 46.5.18 and 46.5.24 before any live test. |
| A "likely" name match picks the wrong app | Wrong app removed | A name match never selects the winget route. The dialog names the route. |
| Live tests remove the owner's real apps | Data loss | **CAUTION:** before each live install or uninstall test, run `winget list --id <id> --exact`. If the app is already installed, stop and pick another app. |
| Internet icons leak which apps a person looks at | Privacy | HTTPS only; the publisher's own site only; owner choice Q8. |
| Curated picks go out of date | Broken cards | `tools/check-catalog-picks.ps1` before each release; a failed id is skipped, not shown. |
| Store app removal is for this user only | Other users keep the app | Dialog text says "for your account". |
| Mandatory sign-in on other PCs | The account service runs on this PC only (`http://127.0.0.1:8787`), so every other PC stops at Sign in | Owner choice Q1 before any public release. |

---

## 10. Fundamental changes for the owner

Each item below changes how DeskVerve works at its base. The Manager asks the owner one question at a time, in this order. Each question has a recommendation.

### Q1. Sign in

**What it means:** Today DeskVerve does not open until a person signs in. The sign-in service runs only on this PC. On any other PC, sign-in cannot work, so a person who installs DeskVerve from winget cannot use it.

| Option | What it means for you |
|---|---|
| **A. Sign-in is optional (Recommended)** | DeskVerve opens and works at once. A "Sign in" button stays in the app. When the public sign-in service is ready (tracker 45.24), signed-in users send reports to your admin site. |
| B. Sign in one time on each PC, after the public service is ready | Nobody can use DeskVerve from winget until the service is live on quantumstep.in. That can take weeks. |
| C. Remove sign-in | The app is simplest. Your admin site gets no reports. |

**Why A:** It is the only option that lets other people use DeskVerve now, and it keeps your reports for later.

### Q2. Admin rights

**What it means:** Today Windows asks "Allow this app to make changes?" every time DeskVerve opens. Admin rights let DeskVerve install and remove apps for all users.

| Option | What it means for you |
|---|---|
| **A. Open as a normal app; ask for admin only when a change needs it (Recommended)** | No prompt at start, so the app opens fast. Windows asks one time when you first update, install or remove an app in a session. Reading and searching never ask. |
| B. Keep admin at every start (today) | One prompt each time you open the app. Some faster data methods may not work as admin, so lists can be slower. |
| C. Open as a normal app; Windows asks for each app | No admin helper to build. "Update all" can show one prompt for each app. |

**Why A:** The app opens with no delay, reading is fast and safe, and Update all still shows only one prompt.

### Q3. How DeskVerve reads app data

**What it means:** Today DeskVerve reads the text that the winget tool prints, like reading a printed page. Long names get cut and many apps fall off the list. Microsoft also gives a direct data connection (the COM API) that gives exact data.

| Option | What it means for you |
|---|---|
| **A. Use Microsoft's direct data connection; keep text reading as a backup (Recommended)** | Exact lists, faster screens, no missing apps. If the connection fails on an old PC, the backup still works. |
| B. Keep text reading and fix the rules | Less work now. Lists can break again when winget changes its text. |
| C. Download and read the catalog file ourselves | Works offline. The file format is private and can change without notice. |

**Why A:** It removes the cause of the missing apps and it is the fastest option.

### Q4. Apps that winget does not know

**What it means:** Many apps on a PC are not in the Microsoft catalog. DeskVerve will list them from the Windows installed-apps list. To remove them, DeskVerve must run the uninstaller that each app put on the PC.

| Option | What it means for you |
|---|---|
| **A. Remove them with the app's own uninstaller, with strict safety rules and a confirm dialog (Recommended)** | Every app on the PC can be removed from DeskVerve. Some uninstallers open their own window. |
| B. List them, but open Windows Settings to remove them | Safer and simpler. The person leaves DeskVerve to finish. |
| C. Remove only catalog apps | Many apps cannot be removed in DeskVerve. |

**Why A:** It gives one place to manage every app. The safety rules block unsafe uninstallers, and those apps fall back to option B.

### Q5. Move to .NET 10 now (security finding F1)

**What it means:** .NET is the base software that DeskVerve runs on. Microsoft stopped security fixes for .NET 8 on 10 Nov 2026. .NET 10 gets fixes until 2028.

| Option | What it means for you |
|---|---|
| **A. Move to .NET 10 before the Phase 46 work (Recommended)** | One extra day of work first. All new code is built and tested only once, on a safe base. |
| B. Move after Phase 46 | New features come a little sooner. Everything is tested two times. |
| C. Stay on .NET 8 | No security fixes for the base software. Not safe for a public release. |

**Why A:** Each later change is tested only once, and the public release is on a supported base.

### Q6. How DeskVerve asks before it changes apps

**What it means:** Today each page has an "I confirm" tick box that you must tick before any button works. It is easy to miss, and it stays ticked.

| Option | What it means for you |
|---|---|
| **A. One click for Update and Install; a dialog for Remove and Update all (Recommended)** | Safe actions are fast. Actions that remove apps or change many apps always ask first. |
| B. A dialog for every action | Safest. More clicks. |
| C. Keep the tick box | No change. Slow and easy to get wrong. |

**Why A:** Updates and installs do not delete anything, so one click is safe. Removals always ask.

### Q7. The Getting ready screen

**What it means:** Today the app shows Getting ready and waits for every check before you can work.

| Option | What it means for you |
|---|---|
| **A. Show it only on the first run; later, open at once with saved results and refresh in the background (Recommended)** | The app is usable in about 1 second. A small "Checking" mark shows background work. |
| B. Show it every time, but skip it after 2 seconds | Faster than today, but still a wait. |
| C. Keep it as it is | About 7 seconds of waiting at each start. |

**Why A:** It matches your goal: fast and quick.

### Q8. App icons from the internet

**What it means:** Apps that are not installed have no icon on the PC. DeskVerve can get the icon from the app maker's website.

| Option | What it means for you |
|---|---|
| **A. Get icons from the app maker's own website; save them for 30 days (Recommended)** | Real icons for most apps. DeskVerve contacts only the maker's own site, by a secure connection. |
| B. Ship icons for the popular picks inside DeskVerve; letters for all others | No internet use for icons. Search results show letter tiles. |
| C. Use an outside icon service | Easy, but a third company sees which apps people look at. |

**Why A:** Real icons make the store feel complete, and no third company sees the data.

### Q9. How DeskVerve is packaged for release

**What it means:** ADR 043 says the main download is an installer. An installer (MSIX or setup program) needs build tools that this PC does not have yet. A portable zip is a folder that runs without a setup step. winget can install a zip.

| Option | What it means for you |
|---|---|
| **A. Release a portable zip on winget now; add the installer later (Recommended)** | People can run `winget install` soon. The installer comes in phase 47 as a second choice. |
| B. Wait for the installer | Release waits for the build tools and more work. |
| C. Zip on GitHub only, no winget yet | Only people who find the GitHub page can get it. |

**Why A:** It is the fastest way to "anyone can run winget install", and the installer can come later without a break for users.

### Q10. The winget submission and the package name

**What it means:** winget pull request #480804 adds `QuantumStep.QuantumUpdater` 2.0.3. That is the old Python app. The request passed the automatic checks and waits for a Microsoft reviewer. No public user has it yet. A package name (id) in winget is hard to change after it is public.

| Option | What it means for you |
|---|---|
| A. Change PR #480804 to version 3.0.0 (the new app) and keep the old id | Keeps the request open. The public name stays "QuantumUpdater" for good, or a rename later breaks updates for users. |
| **B. Close PR #480804. Open a new request for `QuantumStep.DeskVerve` 3.0.0 after the Phase 46 fixes and your acceptance (Recommended)** | The right name from the first day. The review restarts (new packages usually take some days). The first public version is the fixed, fast app. |
| C. Let 2.0.3 go public, then send 3.0.0 as an update | Faster first listing. The first public app is the old Python app, which is no longer the product. |

**Why B:** Nobody has the package yet, so a rename costs nothing today. Later it costs every user. The first public version should be the best one.

**Note for B:** phase 47 must rename the program to `DeskVerve.exe` and the zip to `DeskVerve-Windows.zip`, and keep the self-update path working for copies that use the old names (ADR 046). Q1 must be A or C before the public release.

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

## 12. Spike results (fill in after 46.5.1 and 46.7.1)

| Check | Normal user | Admin |
|---|---|---|
| COM start | OK, 39 ms | Not done, needs owner UAC |
| Installed packages | 238 (installed catalog only) | Not done, needs owner UAC |
| With `ARP\` or `MSIX\` local id | ARP 98, MSIX 140 | Not done, needs owner UAC |
| With an update | Not measured (see notes below) | Not done, needs owner UAC |
| Time | Total run 1868 ms; `winget list` 2377 ms | Not done, needs owner UAC |
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
- admin run: not done, needs owner UAC.

## 13. Diagram updates after this work

- System picture: add `WingetLocator`, `WingetRunner`, `WingetReader` (COM) with `WingetCliReader` fallback, `ActionQueue`, `SnapshotCache`, the inventory group, `IconService`, and the admin helper (Q2 = A).
- Screen flow: start opens Updates with saved results; Getting ready on the first run only; the activity bar on every screen; the uninstall dialog with four routes.
- Remove `CatalogSnapshot` from the picture after 46.7.17.
- Update the "Live colours" section with the new tokens in section 11.

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
