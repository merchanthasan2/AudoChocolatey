# Handover — 15 Nov 2026

## Latest continuation: 16 Nov 2026, Track B

Current source is on `channel/windows`; ADR 048/049 and plan section 15 supersede the older Phase 46 choices-pending notes below. Latest completed slice: 46.7.58, `CliPhase`, the English CLI fallback phase reader. Saved output fixtures are copied to test output. This helper is not yet wired into update actions (46.7.69); percentages remain absent on the CLI route.

Verification: .NET SDK 10.0.404 at `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`; 397 tests passed, 0 failed/skipped; Debug x64 app build passed with 0 warnings/errors. No live install/update or owner acceptance was performed. Independent security/design review remains pending. Continue plan section 15.6 with eligible Updates services; 46.7.89 live COM update spike still requires the owner's explicit yes. Do not start the COM installer route before that spike passes.
## 16 Nov 2026 update

The owner resumed work on 16 Nov 2026.

The new app closed right after the admin prompt. The cause was two things: a missing resource index (`resources.pri`) and a missing XAML type link in the code-only `App` class.

The fix moved the app to Windows App SDK 1.8.260921001, turned on PRI generation, and added a crash log at `%LOCALAPPDATA%\QuantumUpdater\winui-crash.log`.

The packaging workload is no longer needed to run the app.

Phase 46 decisions are in `docs/DECISIONS.md` ADR 048.

> **Current name:** as of 15 Nov 2026, the product is **DeskVerve for Windows**; the later Mac product is **DeskVerve for Mac**. Technical paths and identifiers below retain legacy names for compatibility. The owner resumed on 15 Nov 2026 for the rename only; Phase 46 decisions remain owner-later.

Work stopped here because the owner asked for a handover and a stop.

## Product

DeskVerve 3.0.0 is a new C# WinUI 3 app. The Python app is reference only, in `archive/python-app`. The old WinUI seed is in `archive/winui-phase1`. Do not extend either one.

The window that was opened is the Release build:

`src/QuantumUpdater/bin/Release/net8.0-windows10.0.19041.0/win-x64/QuantumUpdater.exe`

It asks for administrator every launch. The first screen is **Sign in**. **Create account** takes any email and a password of at least 8 characters. That account stays on this PC. Do not paste the password into chat.

## Brand assets

The transparent V-only master is `brand/desk-verve-emblem.png`; sized PNGs are under `brand/png/`, and the Windows icon is `brand/desk-verve-icon.ico`. Full lockups are `brand/desk-verve-lockup-transparent.png` and `brand/desk-verve-lockup-white.png`. Use the V-only emblem on dark surfaces and the full lockup on light surfaces; never put the full black wordmark directly on dark.

## Admin page

The account service is a separate program:

`src/QuantumUpdater.Portal/bin/Release/net8.0/QuantumUpdater.Portal.exe`

Admin address on this PC: `http://127.0.0.1:8787/quantumupdater/admin`

Counts at zero mean no one has finished sign-in in the Windows app yet. The app sends a heartbeat after Getting ready. The desktop app cannot open the admin pages. Publishing this site to quantumstep.in has not been done.

## Built

- Dark shell, sidebar, Getting ready, Updates & Health, App Marketplace, Installed, About This PC.
- Winget list, search, upgrade, install, and uninstall. A confirm checkbox starts unchecked.
- Self-update check against the GitHub release. It does not download or apply an installer.
- Local sign-in and the admin counts, updates, apps, and system summary.
- Portable zip: `dist/QuantumUpdater-Windows-3.0.0.zip` (self-contained, not uploaded).

## Not built

- Phase 46 is still a discussion: how deep Disk & Storage goes, which Cleanup tools, which Settings waves, and whether Chocolatey is a second catalog. The catalog stays winget only.
- `DiskService.cs`, `StartupService.cs`, and `ActivityLog.cs` are on disk and are not shown in the window.
- No MSIX installer. The packaging workload (PRI) is not installed. That install needs elevation.
- No GitHub release of 3.0.0, no winget change, no Store, Chocolatey, or download-site submission. Winget PR #480804 is still the old Python 2.0.3 zip.
- Nothing has been committed for this greenfield work unless a later commit says so.

## Next session

Work is active. Read tracker.html first. The next product slice is phase 46, after they choose. On-PC acceptance of sign-in through Updates is still theirs.
