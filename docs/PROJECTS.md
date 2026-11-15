# Projects, Build Workflows & Distribution Guide
## Quantum Updater (v2.0)
**Product Owner:** Quantum Step ([quantumstep.in](https://quantumstep.in))

---

## 1. Directory Structure

```
d:\Vibe Code\Auto Chocolatey\
├── .github/
│   └── workflows/
│       ├── ci.yml                     # Checks the screens and the Python app
│       └── release.yml                # Builds QuantumUpdater.exe
├── docs/                              # Comprehensive Architectural & Product Docs
│   ├── PRD.md                         # Product Requirements Document
│   ├── ARCHITECTURE.md                # System Architecture & Dual-Engine IPC
│   ├── DESIGN_SYSTEM.md               # Glassmorphism, Typography & Zero-Emoji Standard
│   ├── DECISIONS.md                   # Architecture Decision Records (ADRs)
│   ├── PROJECTS.md                    # This document
│   └── PROMPTS_LOG.md                 # Detailed Prompt History & Audit Log
├── web/                               # Shared screens. Same look for a future Mac app.
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
├── brand/                             # Shared emblem
├── design/                            # Approved mockups
├── dist/QuantumUpdater/               # QuantumUpdater.exe after a build
├── Run_QuantumUpdater.vbs             # Headless elevated launcher
├── Run_QuantumUpdater.bat             # Silent PowerShell elevated launcher
├── web_api.py                         # Python pywebview RPC bridge
├── main.py                            # Standalone Python desktop launcher
└── requirements.txt                   # Python dependencies
```

---

## 2. Build Workflows

```powershell
cd "d:\Vibe Code\Auto Chocolatey"
pip install -r requirements.txt
pip install pyinstaller
python build_exe.py QuantumUpdater
```

The app to open is `dist\QuantumUpdater\QuantumUpdater.exe`. Mac packaging stays on `channel/macos`.

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
