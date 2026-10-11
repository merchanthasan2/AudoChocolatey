# Quantum Updater (v2.0)
### An Official Utility by [Quantum Step](https://quantumstep.in)

[![CI Pipeline](https://github.com/merchanthasan2/AudoChocolatey/actions/workflows/ci.yml/badge.svg)](https://github.com/merchanthasan2/AudoChocolatey/actions/workflows/ci.yml)
[![Release](https://github.com/merchanthasan2/AudoChocolatey/actions/workflows/release.yml/badge.svg)](https://github.com/merchanthasan2/AudoChocolatey/actions/workflows/release.yml)
[![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)](https://github.com/merchanthasan2/AudoChocolatey/releases)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**Quantum Updater** is a modern, high-performance Windows Package & Software Update Manager with a native **Frosted Glass (Glassmorphism)** user interface. Built with a **Tauri v2 (Rust) native backend** and hardware-accelerated **Microsoft Edge WebView2**, powered by a **Dual-Engine Architecture** supporting both **Windows Package Manager (Winget)** and **Chocolatey**.

- **Repository**: [https://github.com/merchanthasan2/AudoChocolatey](https://github.com/merchanthasan2/AudoChocolatey)
- **Company Website**: [quantumstep.in](https://quantumstep.in)
- **Product & Download Portal**: [quantumstep.in/quantum-updater](https://quantumstep.in/quantum-updater)
- **Distribution**: Standard Windows NSIS Setup Installer (`QuantumUpdater_2.0.0_x64-setup.exe`), Portable Standalone Executable (`QuantumUpdater.exe`), and Microsoft Winget Community Repository package (`QuantumStep.QuantumUpdater`).

---

## Features & Architecture

- **Industry-Standard Native Stack**: Tauri v2 + Rust backend with embedded Windows application manifest (`requireAdministrator`).
- **Acrylic Frosted Glass UI**: Fluid 28px blur backdrop filters, specular 1px ambient border lights, and deep obsidian/pearl backdrops replacing generic AI-generated aesthetics.
- **Top Glass Navigation Bar**: Sleek horizontal pill navigation (`Updates`, `Explore`, `Installed`, `Console`, `Settings`), live engine indicator, elevated admin badge, Quantum Step brand pill, and 1-click **Dark / Light Mode switcher**.
- **Zero-Emoji Policy**: Precision vector inline SVG icons and typographic hierarchy across every view, button, dialog, and badge.
- **Sequential Installation Queue**: Global FIFO operation queue preventing Windows Installer MSI mutex collisions (`Error 1618`) and persistent global installation status dock visible across all tabs.
- **Official Brand Logos**: High-fidelity full-color SVG brand identities for all software in the catalog and update feeds.
- **Dedicated AI Software Category**: Curated local and cloud AI desktop applications (Ollama, LM Studio, Jan, ChatGPT, Claude, Cursor, Superwhisper, Upscayl, Pinokio) with automatic installed detection and reinstall controls.
- **Multi-Table Winget Parser**: Positional column slicing capable of extracting both standard upgrades and secondary explicit-targeting packages (e.g., Discord, WSL).
- **Bento Metric Grid**: 4 interactive glass stat tiles (`Available Updates`, `Selected to Upgrade`, `Packages Up-to-Date`, `Active Package Engine`) providing instant system clarity.
- **Dynamic Floating Progress Island**: Inspired by modern dynamic mobile capsules; expands with an animated status pulse, real-time percentage badge, active ticker stage (`Downloading 45% (35 MB / 78 MB)`), and smooth gradient progress track.
- **100% Silent Background Execution**: Built with `CREATE_NO_WINDOW` (`0x08000000`) and hidden process flags so native consoles or black terminal flashing never appear to the user.
- **Single-Prompt UAC Elevation**: Embedded manifest requests administrative privileges at application launch, completely eliminating repetitive UAC elevation prompts during package installations.
- **Clean Abort on Exit**: Closes all background installers and process trees cleanly upon window close.

---

## Project Structure

```
d:\Vibe Code\Auto Chocolatey\
├── .github/
│   └── workflows/
│       ├── ci.yml                   # CI pipeline (linting, zero-emoji, python, tauri check & test)
│       └── release.yml              # Release automation (Tauri NSIS installer & portable exe)
├── src-tauri\                       # Native Rust Tauri v2 Backend
│   ├── Cargo.toml                   # Rust metadata (quantum_updater, authors: Quantum Step)
│   ├── tauri.conf.json              # Window geometry, NSIS installer config, publisher
│   ├── build.rs                     # Tauri build script
│   ├── windows\manifest.xml         # Embedded UAC manifest (QuantumStep.QuantumUpdater)
│   ├── icons\                       # Application icons (icon.ico, icon.png)
│   └── src\
│       ├── main.rs                  # Windows entry point
│       └── lib.rs                   # Dual-engine orchestrator (Winget + Chocolatey)
├── web\                             # True Glassmorphic Frontend
│   ├── index.html                   # Semantic layout, Bento Grid & Floating Island
│   ├── style.css                    # Acrylic backdrop-filters, mesh orbs, specular borders
│   └── app.js                       # Universal bridge (Tauri v2 IPC + pywebview fallback)
├── core\                            # Python Dual-Engine Implementation
│   ├── winget_engine.py             # Python Winget parser & execution orchestrator
│   ├── choco_engine.py              # Python Chocolatey parser
│   └── catalog.py                   # Software catalog definition
├── docs\                            # Comprehensive Documentation Suite
│   ├── PRD.md                       # Product Requirements Document
│   ├── ARCHITECTURE.md              # Technical Architecture & Mutex Strategy
│   ├── DESIGN_SYSTEM.md             # Glassmorphism, Typography & Zero-Emoji Standard
│   ├── DECISIONS.md                 # Architecture Decision Records (ADRs)
│   ├── PROJECTS.md                  # Build Instructions & Winget Distribution
│   └── PROMPTS_LOG.md               # User Prompts Traceability Log
├── manifests\                       # Winget Community Repository Package Manifests
│   └── w\QuantumStep\QuantumUpdater\2.0.0\
│       ├── QuantumStep.QuantumUpdater.yaml
│       ├── QuantumStep.QuantumUpdater.installer.yaml
│       └── QuantumStep.QuantumUpdater.locale.en-US.yaml
├── Run_QuantumUpdater.vbs           # Zero-flash elevated VBScript launcher (Quantum Step)
├── Run_QuantumUpdater.bat           # Silent PowerShell elevated batch launcher
└── requirements.txt
```

---

## Installation & Distribution Options

### Option 1: Standard Windows Installer (NSIS Setup Wizard)
Install via `QuantumUpdater_2.0.0_x64-setup.exe`:
- Installs to `C:\Program Files\Quantum Updater\`.
- Adds Start Menu shortcuts, Desktop icon, and registers in Windows **Add/Remove Programs**.
- Supports silent enterprise deployment:
  ```powershell
  QuantumUpdater_2.0.0_x64-setup.exe /S
  ```

### Option 2: Portable Standalone Executable
- Run `QuantumUpdater.exe` directly from any folder, USB flash drive, or network share.
- Zero installation or extraction required.

### Option 3: Install via Winget (Windows Package Manager)
Once published to the winget-pkgs repository:
```powershell
winget install QuantumStep.QuantumUpdater
```

---

## Launchers (Local Workspace)

### Zero-Flash Silent Launch (Recommended)
Double-click [Run_QuantumUpdater.vbs](file:///d:/Vibe%20Code/Auto%20Chocolatey/Run_QuantumUpdater.vbs)
- Starts the elevated app with **zero console window flash**.
- Fully headless script execution using Windows Script Host.

### PowerShell / Batch Launcher
Double-click [Run_QuantumUpdater.bat](file:///d:/Vibe%20Code/Auto%20Chocolatey/Run_QuantumUpdater.bat)
- Uses PowerShell `-WindowStyle Hidden` and `-Verb RunAs`.

---

## Building From Source & CI/CD

### Prerequisites
- [Rust](https://rustup.rs/) (1.80+ or latest stable)
- Microsoft C++ Build Tools (MSVC)
- Node.js (v20+)

### Automated GitHub Actions CI/CD
Every push and pull request to `main` runs the automated CI workflow:
- **Web Frontend**: Validates JS syntax and enforces Zero-Emoji policy.
- **Python Backend**: Compiles and verifies all Python modules.
- **Rust Backend**: Runs `cargo check` and `cargo test` across all crates.

Publishing a version tag (`git tag v2.0.0 && git push origin v2.0.0`) automatically builds the NSIS installer and portable executable and publishes them to GitHub Releases.

### Local Build Commands
```powershell
# Build Tauri desktop bundle
cd src-tauri
cargo build --release

# Run unit tests
cargo test
```
