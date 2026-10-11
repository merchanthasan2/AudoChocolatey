# Projects, Build Workflows & Distribution Guide
## Quantum Updater (v2.0)
**Product Owner:** Quantum Step ([quantumstep.in](https://quantumstep.in))

---

## 1. Directory Structure

```
d:\Vibe Code\Auto Chocolatey\
├── .github/
│   └── workflows/
│       ├── ci.yml                     # Continuous Integration (Rust, Python, Web lint)
│       └── release.yml                # Continuous Delivery (NSIS installer & portable exe release)
├── docs/                              # Comprehensive Architectural & Product Docs
│   ├── PRD.md                         # Product Requirements Document
│   ├── ARCHITECTURE.md                # System Architecture & Dual-Engine IPC
│   ├── DESIGN_SYSTEM.md               # Glassmorphism, Typography & Zero-Emoji Standard
│   ├── DECISIONS.md                   # Architecture Decision Records (ADRs)
│   ├── PROJECTS.md                    # This document
│   └── PROMPTS_LOG.md                 # Detailed Prompt History & Audit Log
├── src-tauri/                         # Native Rust Tauri v2 Engine
│   ├── Cargo.toml                     # Rust manifest (quantum_updater v2.0.0)
│   ├── tauri.conf.json                # Tauri v2 bundle configuration
│   ├── windows/manifest.xml           # UAC Administrator manifest
│   ├── icons/                         # Windows application icon set
│   └── src/
│       ├── main.rs                    # Entry point
│       └── lib.rs                     # Dual-engine orchestrator & IPC commands
├── web/                               # Hardware-Accelerated Frontend
│   ├── index.html                     # Semantic layout, Bento Grid, Global Dock
│   ├── style.css                      # Master Glassmorphism design tokens
│   └── app.js                         # Universal bridge, Queue Manager & state
├── core/                              # Python Engine Implementation
│   ├── winget_engine.py               # Streaming Winget process manager
│   ├── choco_engine.py                # Chocolatey engine manager
│   ├── manager.py                     # Central coordinator
│   └── models.py                      # Data models
├── manifests/                         # Microsoft Winget Community Package Manifests
│   └── w/QuantumStep/QuantumUpdater/2.0.0/
│       ├── QuantumStep.QuantumUpdater.yaml
│       ├── QuantumStep.QuantumUpdater.installer.yaml
│       └── QuantumStep.QuantumUpdater.locale.en-US.yaml
├── dist/                              # Output build artifacts
│   ├── installer/                     # NSIS Setup Wizard executable
│   └── portable/                      # Standalone zero-install executable
├── Run_QuantumUpdater.vbs             # Headless elevated launcher
├── Run_QuantumUpdater.bat             # Silent PowerShell elevated launcher
├── web_api.py                         # Python pywebview RPC bridge
├── main.py                            # Standalone Python desktop launcher
└── requirements.txt                   # Python dependencies
```

---

## 2. Build Workflows

### 2.1 Build Native Tauri v2 Production Release
Prerequisites: Rust 1.80+, Microsoft C++ Build Tools (MSVC), Node.js.
```powershell
# Navigate to project root
cd "d:\Vibe Code\Auto Chocolatey"

# Compile release binary and build NSIS installer
npx @tauri-apps/cli build
```
Outputs:
- **NSIS Setup Wizard**: `dist\installer\QuantumUpdater_2.0.0_x64-setup.exe`
- **Portable Standalone**: `dist\portable\QuantumUpdater.exe`

### 2.2 Local Development Mode (Tauri v2 Dev Server)
```powershell
npx @tauri-apps/cli dev
```

### 2.3 Standalone Python Runtime (PyWebView)
```powershell
pip install -r requirements.txt
python main.py
```

---

## 3. Microsoft Winget Package Submission
Manifest files are maintained under `manifests/w/QuantumStep/QuantumUpdater/2.0.0/`:
- `QuantumStep.QuantumUpdater.yaml` (Version manifest)
- `QuantumStep.QuantumUpdater.installer.yaml` (Installer architecture, SHA-256 hash, and download URL)
- `QuantumStep.QuantumUpdater.locale.en-US.yaml` (Metadata, description, publisher details)

To validate and test locally with `winget-cli`:
```powershell
winget validate "manifests\w\QuantumStep\QuantumUpdater\2.0.0"
winget install --manifest "manifests\w\QuantumStep\QuantumUpdater\2.0.0"
```
