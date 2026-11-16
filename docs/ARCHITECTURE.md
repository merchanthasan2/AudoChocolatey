# DeskVerve — system picture

**Product:** DeskVerve for Windows

**Publisher:** Quantum Step (quantumstep.in)

**Stack:** C# + WinUI 3 (ADR 043)

**Reference:** Python app in `archive/python-app`. Phase 1 WinUI seed in `archive/winui-phase1`.

**This picture:** refreshed 2026-11-16 after the DeskVerve rename and logo (ADR 046, ADR 047). It shows the Phase A app as built. The section "Planned: Phase 46 priority" shows proposed changes only. Those parts are not built.

**Update 2026-11-16 (re-order):** the built screen flow now shows optional sign-in (46.7.31 to 46.7.34 are built). The section "Re-order 16 Nov: design base, then Updates" shows the new build order and the planned Updates flows.

## Protective middle layer

**Required:** one C# service layer under `src/QuantumUpdater/Services/`.

- UI (`Views/`) calls services only. `AppServices` is the facade.
- Services own winget, the GitHub release check, and the account client. Allow-lists and confirm gates stay in the services.
- No second bridge. Page code does not start processes.
- The account service is a separate program, `src/QuantumUpdater.Portal`. The desktop app never requests admin routes.

## Live colours

**Current rule:** ADR 043 Q4 (2026-11-15). The Stitch dark shell is live. It replaces the Cream, Orange, and Black theme of 12 Nov and the Park direction.

WinUI uses the Stitch dark shell (`Theme.cs`): canvas `#131313`, sidebar `#272727`, cards `#2C2C2C`, stroke `#383838`, accent `#0078D4`. Status colours: healthy `#27AC62`, warning `#D8A000`, critical `#E81123`.

The cobalt and orange in the DeskVerve V are artwork colours only. They are not UI tokens.

## Brand assets

- Transparent V-only master: `brand/desk-verve-emblem.png` (449 x 301). Sized square PNGs: `brand/png/desk-verve-emblem-{32,64,128,256,512}.png`. Windows icon: `brand/desk-verve-icon.ico`.
- The build copies the master and the icon to `Assets/Brand/` next to the executable. `BrandAssets.Emblem(size)` loads the master from that file path, not from `ms-appx:///`.
- The csproj sets `ApplicationIcon` to `brand/desk-verve-icon.ico`.
- Full lockups: `brand/desk-verve-lockup-transparent.png` and `brand/desk-verve-lockup-white.png`. Owner originals: `brand/source/`.
- Old file names `brand/quantum-updater*.{svg,ico,png}` stay for compatibility. They now contain the DeskVerve V.
- Use the transparent V-only emblem on dark surfaces and the full lockup on light surfaces. Never place the full black wordmark directly on dark surfaces.

| Surface | Emblem box | Where |
|---|---|---|
| Title bar | 24 x 24 | `MainWindow.xaml.cs` |
| Sign in | 64 x 64 | `Views/SignInPage.cs` |
| About This PC | 40 x 40 | `Views/AboutPage.cs` |
| Tracker header | 52 x 52, 4px padding | `tracker.html` |
| README | White lockup, 320px wide | `README.md` |

## Releases

- Asset name stays `QuantumUpdater-Windows.zip` (self-contained win-x64 folder) until phase 47 (ADR 046).
- `.github/workflows/release.yml` still builds the Python zip. Phase 47 changes it to WinUI.
- `dist\QuantumUpdater\` holds the old Python build. The current WinUI build is `src/QuantumUpdater/bin/Release/net8.0-windows10.0.19041.0/win-x64/QuantumUpdater.exe`.
- Winget portable zip continues to point at that asset name.
- Self-update: About checks the GitHub latest release. "Install update" stays off until the installer exists.

## System picture (WinUI — Phase A built)

```mermaid
flowchart TD
  exe["QuantumUpdater.exe · DeskVerve · WinUI 3 · admin"]
  shell["MainWindow · 40px title bar · 240px sidebar"]
  views["Views · Sign in, Getting ready, Updates and Health, App Marketplace, Installed, About This PC"]
  brand["BrandAssets · V emblem from Assets/Brand"]
  facade["AppServices facade"]
  winget["WingetService · list and run upgrades"]
  installed["InstalledService · list and uninstall"]
  market["MarketplaceService · search and install"]
  upd["UpdateService · GitHub release check"]
  acct["AccountClient · register, login, heartbeat, activity"]
  session["SessionStore · LOCALAPPDATA/QuantumUpdater"]
  snap["CatalogSnapshot · shared scan result"]
  machine["MachineSummary · Windows, CPU, memory, disk"]
  pc["winget on this PC"]
  gh["GitHub Releases"]
  portal["QuantumUpdater.Portal · 127.0.0.1:8787 · separate program"]
  admin["Admin pages /quantumupdater/admin"]

  exe --> shell
  shell --> brand
  shell --> views
  views --> facade
  views --> machine
  facade --> winget
  facade --> installed
  facade --> market
  facade --> upd
  facade --> acct
  facade --> session
  facade --> snap
  winget --> pc
  installed --> pc
  market --> pc
  upd --> gh
  acct -->|"/api/* only"| portal
  portal --> admin
```

`DiskService` and `StartupService` exist in `Services/` but no screen uses them yet. Tracker phase 46 decides their use.

## Screen flow (Phase A — built)

Design source: Stitch project Modern Windows System Toolkit (`11437327097635848724`).

```mermaid
flowchart TD
  open["Open app · UAC approve"] --> ready["Getting ready · no sign-in wall (46.7.31)"]
  ready -- "scans settle · auto" --> updates["Updates and Health"]
  nav["Sidebar · any screen"] --> ready
  nav --> updates
  nav --> market["App Marketplace"]
  nav --> installed["Installed"]
  nav --> about["About This PC"]
  nav -- "footer · Not signed in · Sign in (46.7.32)" --> signin["Sign in · Not now returns to the last screen (46.7.33)"]
  signin -- "Sign in or Not now" --> back["Last screen"]
  nav -- "footer · Sign out (46.7.34)" --> out["Session cleared · footer shows Not signed in"]
  signin -. "/api/register, /api/login" .-> account["Account service on this PC"]
  ready -. "heartbeat · signed in only" .-> account
  updates -. "activity: upgrade · signed in only" .-> account
  market -. "activity: install · signed in only" .-> account
  installed -. "activity: remove · signed in only" .-> account
  account --> admin["Admin site in a browser, separate from the window"]
```

Getting ready runs three checks at the same time: winget upgrades, installed packages, and the DeskVerve release check. It then sends a heartbeat (signed in only) and opens Updates and Health one time.

Known open problem (16 Nov): some starts end with 0xC000027B (a stowed WinUI error). Plan section 15.4 gives the test. TEMP start marks are in `App.xaml.cs`, `MainWindow.xaml.cs` and `GettingReadyPage.cs` (commit `a4956ce`).

Disk & Storage, Cleanup, Settings waves, and Chocolatey are discussed in tracker phase 46 before they are built.

## Planned: Phase 46 priority (NOT BUILT)

> **PLANNED.** This section is a proposal from 16 Nov 2026, revised after the owner answers in ADR 049. Plan: `docs/plans/2026-11-16-phase46-apps-first.md`. Mockups: `design/mockups/phase46/` (Getting ready: `00-getting-ready.html`). Built so far (16 Nov, not yet used by every screen): `WingetLocator`, `WingetRunner`, `PackageId`, `WingetTextTable`, `ActionQueue` (no screen uses it yet), `AppEntry`, `RegistryInventory`, `StoreAppInventory`, `WingetReader` (COM), `ScanJob`, `ScanOrchestrator` (no screen uses it yet), and optional sign-in. The rest is not built. Move this section into the built picture only after the Design Architect gives PASS.

### Planned system picture

```mermaid
flowchart TD
  exe["DeskVerve window · admin at every start (ADR 049 Q2)"]
  views["Views · Getting ready, Updates and Health, App Marketplace, Installed, About, Sign in (optional) · shared rows, cards, dialogs, activity bar"]
  facade["AppServices facade"]
  scan["ScanOrchestrator · all jobs at the same time · 15 s limit each"]
  queue["ActionQueue · one action at a time (F18) · built"]
  inst["InstalledCatalog · saved, then fresh"]
  upd["UpdatesCatalog · saved, then fresh"]
  mkt["MarketplaceCatalog · picks, search, tags, details"]
  picks["Assets/catalog-picks.json · curated picks"]
  disk["DiskService · space and health (read only)"]
  self["UpdateService · DeskVerve version"]
  reg["RegistryInventory · HKLM 64/32, HKCU 64/32"]
  store["StoreAppInventory · Store apps for this user"]
  merge["InstalledMerger · exact match, then likely"]
  plan["UninstallPlanner · winget, MSI, own uninstaller, Store, none"]
  urun["UninstallRunner · run, then read the key again"]
  normal["NormalUserLauncher · HKCU uninstallers without admin"]
  reader["WingetReader · COM API reads (in-proc factory)"]
  cli["WingetCliReader · text fallback"]
  runner["WingetRunner · argv only, --exact, kill on cancel"]
  locator["WingetLocator · App Installer package path (F3)"]
  cache["SnapshotCache · LOCALAPPDATA/QuantumUpdater/cache"]
  icons["IconService · icon pipeline (see below)"]
  acct["AccountClient · only when signed in"]
  com["winget COM server"]
  pc["winget.exe on this PC"]
  win["Windows registry and package manager"]

  exe --> views
  views --> facade
  facade --> scan
  facade --> inst
  facade --> upd
  facade --> mkt
  facade --> queue
  facade --> icons
  facade -. "signed in only" .-> acct
  scan --> upd
  scan --> inst
  scan --> mkt
  scan --> icons
  scan --> disk
  scan --> self
  inst --> reg
  inst --> store
  inst --> reader
  inst --> merge
  inst --> cache
  upd --> reader
  upd --> cache
  mkt --> picks
  mkt --> reader
  mkt --> cache
  reader -. "if COM fails" .-> cli
  cli --> runner
  reader --> com
  reg --> win
  store --> win
  queue --> runner
  queue --> urun
  urun --> plan
  urun -. "HKCU entry" .-> normal
  runner --> locator
  locator --> pc
```

### Planned scan at start (ScanOrchestrator)

```mermaid
flowchart LR
  start["App opens · admin prompt"] --> ready["Getting ready · sidebar locked"]
  ready --> orch["ScanOrchestrator · start all jobs"]
  orch --> j1["App updates · UpdatesCatalog · step N of 3"]
  orch --> j2["Installed apps · InstalledCatalog · step N of 3"]
  orch --> j3["App Marketplace · MarketplaceCatalog · step N of 2"]
  orch --> j5["Drives · DiskService · step N of 2"]
  orch --> j6["DeskVerve version · UpdateService"]
  orch -. "46.2 later slice" .-> j7["Space you can free · cleanup estimate · not built"]
  j2 --> j4["App icons · IconService · N of M"]
  j3 --> j4
  j1 & j2 & j3 & j4 & j5 & j6 --> done{"Every job Done or Failed?"}
  done -- "all Done" --> updates["Open Updates and Health after 1 s"]
  done -- "a job Failed" --> cont["Stay · show reason, Try again, Continue"]
  updates --> unlock["Sidebar unlocked"]
  cont --> unlock
```

Rules: each job has a 15 s limit (the icon job 8 s on the first start); a failed job never traps the app; the overall percent comes only from real counts and steps; target 5 s on the owner's PC with a warm icon cache.

### Planned icon pipeline (IconService)

```mermaid
flowchart TD
  ask["AppIcon asks for an icon"] --> memc{"In memory or disk cache (30 days)?"}
  memc -- yes --> show["Show the icon"]
  memc -- no --> kind{"Installed app?"}
  kind -- yes --> s1["1 Registry DisplayIcon · path,index"]
  s1 -- "not found or blank-program icon" --> s2["2 Program file via Shell · IShellItemImageFactory, SHGetFileInfo"]
  s2 -- "not found" --> s3["3 MSI ARPPRODUCTICON · MsiGetProductInfo"]
  s3 -- "not found" --> s4["4 MSIX or AppX logo · AppxManifest Square44x44Logo"]
  s4 -- "not found" --> s5["5 Start menu shortcut (Manager to approve)"]
  s5 -- "not found, exact catalog match" --> m1
  kind -- "no · Marketplace app" --> m1["1 winget manifest Icons · SHA-256 check"]
  m1 -- "not found" --> m2["2 Maker homepage · apple-touch-icon, link rel icon, manifest.json, favicon"]
  m2 -- "not found" --> tile["Letter tile · last resort"]
  s5 -- "not found, no catalog match" --> tile
  s1 & s2 & s3 & s4 & s5 & m1 & m2 -- "original icon" --> save["Save in cache with the source name"] --> show
  m1 & m2 -.-> fetch["SafeIconFetcher · HTTPS, maker domain, 256 KB, PNG ICO JPEG, no SVG"]
```

A Marketplace app that is on the PC uses its installed icon first. Task 46.7.21 measures coverage: target 95 percent of installed apps and 90 percent of popular picks with an original icon.

Planned changes to the built picture:

- `WingetService`, `InstalledService` and `MarketplaceService` share one `WingetRunner` and one `WingetLocator` (tracker 45.17: F3, F4, F16, F18, F24).
- `CatalogSnapshot` is retired. `InstalledCatalog` and `UpdatesCatalog` replace it.
- Reads use the winget COM API. Changes (install, upgrade, uninstall) use the hardened winget command.
- The Installed list comes from Windows first (registry and Store apps). winget adds the catalog id and the update state.
- `GettingReadyPage` runs `ScanOrchestrator` at every start. It replaces the three fixed checks.
- The sign-in gate goes away. Sign-in is optional (ADR 049 Q1).
- No admin helper. The app keeps `requireAdministrator` (ADR 049 Q2). `NormalUserLauncher` runs user uninstall entries without admin rights.
- Protective middle layer: still one C# service layer. `NormalUserLauncher` and `SafeIconFetcher` are part of it. The Security & Code Architect reviews both.

### Planned screen flow

```mermaid
flowchart TD
  open["Open app · UAC approve"] --> ready["Getting ready · full scan · every start · sidebar locked"]
  ready -- "every job Done" --> updates["Updates and Health"]
  ready -- "a job Failed · Continue" --> updates
  nav["Sidebar · unlocked after the scan"] --> updates
  nav --> market["App Marketplace home · popular and categories"]
  nav --> installed["Installed · every app on the PC"]
  nav --> about["About This PC"]
  nav -- "footer · Sign in (optional)" --> signin["Sign in · Not now returns"]
  market -- "type · 250 ms pause" --> results["Search results and details panel"]
  market -- "See all" --> results
  results -- "Esc or cross" --> market
  updates -- "Update · one click" --> queue["ActionQueue"]
  updates -- "Update all or 2+ selected" --> dlgall["Update all dialog · app list"]
  dlgall -- "Update all" --> queue
  market -- "Install · one click" --> queue
  results -- "Install · one click" --> queue
  installed -- Uninstall --> dialog["Remove dialog · names the route"]
  dialog -- Remove --> queue
  queue --> bar["Activity bar · every screen · N of M · Cancel"]
  queue -- "done" --> refresh["Refresh the changed rows"]
  signin -. "signed in only" .-> account["Account service · reports"]
```

### Planned uninstall routes

```mermaid
flowchart LR
  row["Installed row"] --> m{"Exact winget match?"}
  m -- yes --> w["winget uninstall --id --exact --silent"]
  m -- no --> k{"Row type"}
  k -- "MSI key" --> msi["msiexec /x{GUID} /passive"]
  k -- "Other registry key" --> own{"Passes strict rules?"}
  own -- "yes · HKLM" --> exe["App's own uninstaller · admin"]
  own -- "yes · HKCU" --> nu["App's own uninstaller · NormalUserLauncher · no admin"]
  nu -- "launch fails" --> settings
  own -- no --> settings["Open Windows Settings"]
  k -- "Store app" --> appx["Remove for this user"]
  k -- "Protected" --> settings
```

## Re-order 16 Nov: design base, then Updates (PLANNED)

> **PLANNED.** Owner order of 16 Nov 2026. Plan: `docs/plans/2026-11-16-phase46-apps-first.md`, section 15. New mockup: `design/mockups/phase46/05-updates-states.html`. Installed and Marketplace rewrites come after Track B. Phase 49 needs its own plan.

### Build order

```mermaid
flowchart LR
  a0["A0 · start crash test · 46.7.36 to 46.7.40"] --> a9["46.7.9 shell rewrite · no Mica · SetTitleBar after Content"]
  a1["A · tokens and parts · 46.7.2, 46.7.3, 46.7.41 to 46.7.43, 46.7.4 to 46.7.7"] --> gal["46.7.48 TEMP parts gallery"]
  aq["A · queue key, kind, phase, batch · 46.7.44, 46.7.45 · ActivitySummary 46.7.46"] --> bar["46.7.8 ActivityBar"] --> a9
  a9 --> cpa{"Checkpoint A · owner looks at shell and gallery"}
  gal --> cpa
  cpa --> b1["B · runner W7, W18, W8 · error text · RunOutcome · CLI phase words · COM update spike, WingetInstaller (real percent)"]
  cpa --> b2["B · WingetReader W20 (and W21, W22)"]
  cpa --> b3["B · SnapshotCache · UpdatesGrouping · sources · UpdatesCatalog"]
  b1 --> b4["B · UpdateActions · queue hold (W18) · old pages through the queue (W2)"]
  b2 --> b3
  b3 --> b4
  b4 --> b5["B · Updates screen parts · UpdatesPage rewrite · badge · Getting ready interim"]
  b5 --> cpb{"Checkpoint B · owner runs real updates"}
  cpb --> later["Later · Installed, Marketplace, icons, Getting ready rewrite · phase 49 plan"]
```

### Planned Updates data flow

```mermaid
flowchart TD
  ready["Getting ready (interim 46.7.83)"] --> cat["UpdatesCatalog · saved, then fresh · one refresh at a time"]
  page["UpdatesPage · header, tiles, toolbar, VirtualList, state card"] --> cat
  cat --> cache["SnapshotCache · upgrades.json"]
  cat --> com["ComUpdatesSource · WingetReader · Microsoft catalog only"]
  cat -. "COM fails" .-> cli["CliUpdatesSource · WingetService text list · --source winget"]
  com --> reader["WingetReader · async COM with token · one call at a time (W20)"]
  cli --> runner["WingetRunner · allowlist (W8)"]
  cat --> group["UpdatesGrouping · Ready, Version not known, Updated today"]
  cat -- Changed --> page
  cat -- Changed --> badge["Sidebar badge · Ready + Version not known"]
  page --> model["UpdatesListModel · flat list, counts, labels"]
```

### Planned update action flow (one action at a time)

```mermaid
sequenceDiagram
  participant P as UpdatesPage
  participant D as ConfirmDialog
  participant A as UpdateActions
  participant Q as ActionQueue (app-wide)
  participant I as WingetInstaller (COM, main)
  participant W as WingetService + WingetRunner (CLI, backup)
  participant B as ActivityBar
  P->>A: Update (one row, one click)
  P->>D: Update all or 2+ selected
  D-->>P: confirmed
  P->>A: UpdateMany (list order)
  A->>Q: Enqueue(key = catalog id, kind Update)
  Q-->>B: Changed (Waiting, Working, batch "1 of N")
  Q->>A: run the work
  A->>I: UpgradeAsync(id, progress, token)
  I->>I: FindForActionAsync (exact id, one match) · UpgradePackageAsync (AllowHashMismatch false)
  I-->>Q: ActionProgress (Downloading: real bytes percent · Installing: number only if reported)
  I-->>A: ActionResult (ComResultMap: status, installer code, reboot)
  alt COM result NotStarted (COM could not start)
    A->>W: UpgradeAsync(id) · upgrade --id ID --exact --source winget
    W-->>Q: ActionProgress (phase words only, no percent)
    W-->>A: ActionResult (RunOutcome: exit code, ErrorLines)
  end
  A->>A: success: UpdatesCatalog.MarkUpdated, report if signed in
  alt Cancel or 10-minute limit
    A->>I: Cancel() · wait 10 s for an end state
    I-->>A: no end state: StillRunning (W18 on the COM route)
  end
  alt StillRunning (COM operation open, or CLI process not stopped)
    A-->>Q: ActionOutcome with StillRunning
    Q-->>B: held: "could not stop ... next action starts when it ends"
    Q->>Q: wait for the end state, then continue
  end
```

Rules: `InstalledPage` and `MarketplacePage` (old pages) also put their actions into the same queue (W2). The CLI runs only when the COM route did not start, never after a COM operation started (plan D9). DeskVerve never kills an installer on the COM route (plan D10). Closing the window during an action asks first (plan D11). Spike 46.7.89 must pass before the COM route goes live; if it fails, the CLI is the only route, with phase words. The shell drops `MicaBackdrop`: the opaque canvas `#131313` covers the window, so the look does not change (plan D1).

Diagram updates after Track B: move the two Updates diagrams, `ActionQueue`, `ActivityBar` and `UpdatesCatalog` into the built picture after the Design Architect gives PASS. Remove `CatalogSnapshot` from the Updates path (it stays for Installed until 46.7.17).

## Phase map

| Phase | Screens / work | When |
|---|---|---|
| 44 | Packaging tools, archive Python, empty greenfield project | Done |
| 45 | Sign-in, Getting ready, Updates & Health, Marketplace, Installed, About, self-update check, DeskVerve rename and logo | Built. Owner on-PC check and validation open |
| 46 | ADR 048 order: first Installed, Marketplace, Updates, Getting ready full scan and original icons (46.5 to 46.7, planned in `docs/plans/2026-11-16-phase46-apps-first.md`, ADR 049); then Disk (46.1); then Cleanup (46.2); then old tool groups. Chocolatey later | Wave 1 done; Wave 2 partly done; optional sign-in built. Re-order 16 Nov: Track A design base, then Track B Updates (plan section 15) |
| 47 | Installer first: the first public release is a setup program or MSIX (ADR 049 Q9). DeskVerve exe name. winget: close PR #480804, then a new request for `DeskVerve.Utility` ("DeskVerve Utility", moniker `deskverve`) (ADR 049 Q10). Then Store, Chocolatey, GitHub, download libraries | Planned. Submissions are owner later |
| 48 | Admin portal | Local service built. Domain deploy is owner later |

## Parts

| Part | Role |
|---|---|
| `src/QuantumUpdater/` | Windows app (DeskVerve) |
| `src/QuantumUpdater/Services/` | Protective middle layer |
| `src/QuantumUpdater/Views/` | Screens |
| `src/QuantumUpdater/Theme.cs`, `Chrome.cs`, `BrandAssets.cs` | Colours, shared controls, emblem loader |
| `src/QuantumUpdater.Portal/` | Account service and admin pages |
| `brand/` | DeskVerve artwork and compatibility copies |
| `archive/python-app/` | Old Python app, reference only |
| `.github/workflows/release.yml` | Still the Python zip until phase 47 |
