# Product Requirements Document (PRD)
## Quantum Updater (v2.0)
**Product Owner:** Quantum Step ([quantumstep.in](https://quantumstep.in))  
**Document Status:** Approved & Implemented  
**Classification:** Enterprise Desktop Utility Software  

---

## 1. Executive Summary
**Quantum Updater** is an enterprise-grade, glassmorphic Windows package and software update management platform. It bridges native Windows package managers (**Windows Package Manager / Winget** and **Chocolatey**) with a modern, hardware-accelerated user interface. Designed to eliminate command-line friction and visual terminal noise, Quantum Updater provides deterministic update detection, authenticated cryptographic package verification, an asynchronous execution queue, and an intuitive software discovery marketplace.

---

## 2. Target Personas
1. **Power Users & Developers**: Require batch update workflows, quiet execution, version auditing, and curated developer toolchains (Git, Docker, VS Code, Cursor, Python, Rust, Node.js).
2. **System Administrators**: Require unattended silent installation (`CREATE_NO_WINDOW`), single-prompt UAC elevation, SHA-256 verification, Authenticode validation, and zero telemetry bloat.
3. **Everyday Desktop Users**: Require clean visual clarity, 1-click installations, clear system health indicators, and complete protection against malware or unverified binary sources.

---

## 3. Core Problem Statements & Solutions
| Problem | Root Cause | Quantum Updater Solution |
|---|---|---|
| **Terminal Window Flashing** | Windows spawns interactive black command prompt consoles when calling subprocesses. | Enforces `CREATE_NO_WINDOW` (`0x08000000`) and hidden process flags across Tauri (Rust) and Python engines for 100% silent background execution. |
| **Repeated UAC Elevation Popups** | Each individual installer attempts to prompt Windows UAC permissions. | Embedded application manifest (`requireAdministrator`) elevates once at application launch, granting blanket execution rights to child installers. |
| **Installer Mutex Collisions (Error 1618)** | Windows Installer (MSI) only permits one active installation transaction at any given moment. Parallel execution triggers immediate fatal aborts. | Asynchronous sequential FIFO Operation Queue coordinates jobs sequentially, maintaining reactive progress across all views. |
| **Dropped / Misaligned Package Updates** | Winget outputs multiple tables (regular upgrades + explicit targeting packages) with variable column widths. `split_whitespace()` fails when package names have spaces or when the `Source` column is blank. | Multi-table fixed-column positional slicing aligns text against header column indices, capturing all upgrades (e.g. Discord, WSL). |
| **Dark / Inverted Brand Icons** | Raw npm SVGs from jsDelivr default to `#000` monochrome black paths, causing brand logos to look like dark silhouettes on dark backgrounds. | Direct integration with `cdn.simpleicons.org` delivers authentic, full-color brand vector SVGs with automated white/contrast enhancements for dark brand marks. |
| **Amateurish UI / Emoji Overuse** | Ubiquitous use of childish emojis across desktop software creates an unprofessional appearance and renders inconsistently across Windows builds. | Strict Zero-Emoji Design Policy replacing all emojis with clean, crisp, unified vector SVGs (Lucide/Heroicons standard). |

---

## 4. Key Functional Requirements

### 4.1 Update Management (Updates Tab)
- **Deep System Audit**: Scans local applications and compares against official repositories using `--include-unknown`, `--disable-interactivity`, and `--accept-source-agreements`.
- **Bento Metric Grid**: Real-time interactive tiles displaying Updates Available, Selected to Upgrade, Software Health status, and Active Package Engine.
- **Batch & Selective Upgrades**: Checkbox selection for granular updating or single-click "Update All Packages".
- **Real-Time Progress Island**: Live progress capsule with percentage indicator, gradient track, and animated status ticker.
- **Cryptographic Trust Strip**: Highlights SHA-256 validation, Authenticode digital signatures, and Microsoft SmartScreen compliance.

### 4.2 Software Catalog & Discovery (Explore Tab)
- **Curated Categorization**: Structured filters for `All`, `Most Popular`, `AI & Machine Learning`, `Development`, `Browsers`, `Productivity`, `Music & Media`, `Addons & Utilities`, and `Gaming & Social`.
- **System Installed Detection**: Automatically checks if a package is already installed on the user's system.
- **Installed State Representation**: Renders an "Installed" badge and grays out the install action button, preventing duplicate executions while retaining an advanced "Reinstall" option.
- **Live Online Repository Search**: Queries official package repositories for any unlisted packages on demand.

### 4.3 Installed Software Audit (Installed Tab)
- **Inventory Auditing**: Enumerates all installed software and installed version numbers across system registries, ARP, and package repositories.
- **Instant Search & Filter**: Real-time client-side substring search across application names and IDs.
- **Clean Uninstallation**: Invokes native unattended uninstall routines (`winget uninstall --id ... --silent` / `choco uninstall ... -y`).

### 4.4 Diagnostic Terminal (Console Tab)
- **Streaming Live Logs**: Real-time STDOUT and STDERR streaming capturing exact command strings and subprocess outputs.
- **Log Management**: One-click "Copy All" and "Clear" actions.

### 4.5 Global Operation Dock & Queue System
- **Cross-Tab Visibility**: Sticky operational bar visible across all 5 navigation tabs.
- **Status Reporting**: Displays active job name, package ID, percentage completion, and pending queue count.
- **Queue Drawer**: Expandable glass panel listing running, queued, and completed jobs with status pills and clear options.

---

## 5. Non-Functional Requirements
- **Performance**: Sub-100ms UI responsiveness powered by Microsoft Edge WebView2 hardware acceleration.
- **Memory Footprint**: Native Rust Tauri v2 backend operating under ~60MB RAM.
- **Security & Sandboxing**: Strict origin boundaries, no unauthorized remote script execution, local asset packaging.
- **Compatibility**: Windows 10 (Build 1809+) and Windows 11 (all builds), x64 architecture.
