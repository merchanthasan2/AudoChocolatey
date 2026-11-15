# Quantum Updater (WinUI Phase 1 shell)

Minimal C# WinUI 3 project for ADR 041 dual-track work. Python app remains the ship path until this shell is accepted on-PC.

## Prerequisites

- [.NET SDK 8](https://dotnet.microsoft.com/download) (repo task used 8.0.428)
- Windows 10 19041+ (or Windows 11)
- [Windows App SDK / WinUI 3](https://learn.microsoft.com/windows/apps/windows-app-sdk/) (restored via NuGet when building)
- Visual Studio 2022 with the **Windows application development** workload (optional; recommended for XAML designer and F5 debug)

## Scaffold note

`dotnet new install Microsoft.WindowsAppSDK.ProjectTemplates` failed on this PC (no NuGet sources configured). This folder was created manually with SDK-style `QuantumUpdater.csproj` and `Microsoft.WindowsAppSDK` package references.

To use the official template when NuGet is available:

```powershell
dotnet new install Microsoft.WindowsAppSDK.ProjectTemplates
dotnet new winui -n QuantumUpdater -o src/QuantumUpdater
```

## Build (CLI)

Refresh PATH if needed, then from repo root:

```powershell
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
dotnet build "src/QuantumUpdater/QuantumUpdater.csproj" -c Release
```

If restore fails with “No NuGet sources”, add nuget.org once:

```powershell
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```

## Open in Visual Studio

1. File → Open → Project/Solution
2. Select `src/QuantumUpdater/QuantumUpdater.csproj`
3. Set platform (x64) and run (F5)

## UI shell

- `NavigationView` placeholders: **Getting ready**, **Updates**, **Installed**
- Version label: **Quantum Updater 2.0.3 (WinUI Phase 1 shell)**