# Projects, Build Workflows & Distribution Guide
## DeskVerve (formerly Quantum Updater) (v3.0.0)
**Product Owner:** Quantum Step ([quantumstep.in](https://quantumstep.in))

---

## 1. Directory Structure

```
.
├── .github/
│   └── workflows/
│       ├── ci.yml                     # Builds WinUI 3 app & tests .NET 10 suite
│       └── release.yml                # Builds DeskVerve installer, portable zip & release assets
├── docs/                              # Comprehensive Architectural & Product Docs
│   ├── PRD.md                         # Product Requirements Document
│   ├── ARCHITECTURE.md                # System Architecture & Service Layer
│   ├── DESIGN_SYSTEM.md               # Glassmorphism, Typography & Zero-Emoji Standard
│   ├── DECISIONS.md                   # Architecture Decision Records (ADRs)
│   ├── PROJECTS.md                    # This document
│   └── PROMPTS_LOG.md                 # Detailed Prompt History & Audit Log
├── src/QuantumUpdater/                # WinUI 3 desktop client (.NET 10, self-contained win-x64)
├── tests/                             # Unit tests and test harness
│   ├── QuantumUpdater.Tests/          # xUnit test suite
│   └── FakeWinget/                    # Test mock runner
├── installer/                         # Inno Setup 6 packaging
│   └── DeskVerve.iss                  # Installer specification
├── manifests/                         # Microsoft Winget Community Package Manifests
│   └── q/QuantumStep/QuantumUpdater/3.0.0/
│       ├── QuantumStep.QuantumUpdater.yaml
│       ├── QuantumStep.QuantumUpdater.installer.yaml
│       └── QuantumStep.QuantumUpdater.locale.en-US.yaml
├── brand/                             # DeskVerve icons and brand assets
├── design/                            # Approved mockups
├── dist/release/                      # DeskVerve-Setup-3.0.0-x64.exe and DeskVerve-Windows.zip
├── Run_QuantumUpdater.vbs             # Headless elevated launcher
├── Run_QuantumUpdater.bat             # Elevated launcher
└── scripts/
    ├── build_installer.ps1            # Publishes, zips, compiles Inno Setup, computes SHA256
    └── publish_winui.ps1              # Publishes self-contained WinUI
```

---

## 2. Build Workflows

```powershell
# Build WinUI app
dotnet build src\QuantumUpdater\QuantumUpdater.csproj -c Release -r win-x64

# Run test suite
dotnet test tests\QuantumUpdater.Tests -c Release -- -parallel none

# Produce release artifacts (portable zip + setup exe + checksums)
.\scripts\build_installer.ps1
```

The app executable is `dist\QuantumUpdater-WinUI\DeskVerve.exe`. Release artifacts are in `dist\release\`.

---

## 3. Microsoft Winget Package Submission
Manifest files are maintained under `manifests/q/QuantumStep/QuantumUpdater/3.0.0/` (winget-pkgs partition `q`):
- `QuantumStep.QuantumUpdater.yaml` (Version manifest)
- `QuantumStep.QuantumUpdater.installer.yaml` (Inno installer type, machine scope, x64, SHA-256 hash, and download URL)
- `QuantumStep.QuantumUpdater.locale.en-US.yaml` (Metadata, description, publisher details, moniker: deskverve)

To validate locally with `winget-cli`:
```powershell
winget validate "manifests\q\QuantumStep\QuantumUpdater\3.0.0"
winget install --manifest "manifests\q\QuantumStep\QuantumUpdater\3.0.0"
```
