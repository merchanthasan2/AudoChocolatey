# Development Channels & Multi-Platform Architecture

**Product:** Quantum Updater  
**Publisher:** Quantum Step ([quantumstep.in](https://quantumstep.in))  
**Design System:** Quantum Modern Prism (Apple Obsidian Charcoal & Silver Snow)  
**Policy:** Strict Zero-Emoji • 0.5px Specular Glassmorphism • Unified Look & Feel  

---

## 1. Multi-Channel Branching Strategy

To enable independent development and release cycles without cross-platform friction, Quantum Updater utilizes a structured multi-channel branching architecture:

```
                  ┌───────────────────────────────────────────────┐
                  │                 main (Trunk)                  │
                  │   Unified Web Frontend (web/)                 │
                  │   Shared CI/CD Workflows (.github/workflows/) │
                  │   Cross-Platform Bridge & Design System       │
                  └──────────────────────┬────────────────────────┘
                                         │
                 ┌───────────────────────┴───────────────────────┐
                 ▼                                               ▼
┌─────────────────────────────────┐             ┌─────────────────────────────────┐
│     channel/macos (Branch)      │             │    channel/windows (Branch)     │
│ Native Cocoa/WebKit Host        │             │ Tauri v2 (Rust Core)            │
│ Apple Silicon + Intel Universal │             │ Python pywebview Bridge         │
│ IOKit & AVFoundation Hardware   │             │ Winget & Chocolatey Engines     │
│ Storage Reclaimer & Deep Clean  │             │ Windows MSI Mutex Safe Queue    │
│ build_dmg.sh & DMG Distribution │             │ NSIS Setup & Portable Exe       │
└─────────────────────────────────┘             └─────────────────────────────────┘
```

### Channel Definitions

| Channel Branch | Primary Target OS | Core Engine & Backend | Packaging Output |
|---|---|---|---|
| `channel/macos` | macOS (11.0 Big Sur to 15.x Sequoia) | Native Objective-C Cocoa / WebKit host (`src-macos/main.m`), IOKit, AVFoundation, Homebrew, Apple Software Update | Universal 2 Binary (`arm64` + `x86_64`), `dist/QuantumUpdater_2.0.0_macOS.dmg`, `dist/QuantumUpdater_macOS_Update.zip` |
| `channel/windows` | Windows 10 & 11 (x64 / ARM64) | Tauri v2 (`src-tauri/src/lib.rs`), Python WebAPI (`web_api.py`), Winget Engine, Chocolatey Engine | NSIS Installer (`QuantumUpdater_2.0.0_x64-setup.exe`), Portable executable |
| `main` | Production & Integration Trunk | Shared web frontend (`web/index.html`, `web/style.css`, `web/app.js`), documentation suite (`docs/`), CI/CD | Automated multi-target releases via GitHub Actions |

---

## 2. Guaranteed Look & Feel Uniformity

Both the macOS and Windows channels render the **exact same human-designed interface**:

### A. Design Tokens & Color Harmony
- **Dark Mode Canvas:** Apple Obsidian Charcoal substrate (`#161618`, `#121214`) with glassmorphic cards (`rgba(28, 28, 32, 0.78)`).
- **Light Mode Canvas:** Apple Silver Snow substrate (`#f5f5f7`, `#ffffff`) with ultra-fine contrast.
- **Vibrancy & Frosted Blur:** `backdrop-filter: blur(48px) saturate(200%)`.
- **Specular Hairlines:** 0.5px border (`rgba(255, 255, 255, 0.08)`) with inset 0.5px top highlight (`inset 0 1px 0 rgba(255, 255, 255, 0.08)`).
- **HIG System Accents:**
  - Apple Blue (`#0A84FF`) - Focus, active navigation, selections.
  - Apple Mint (`#30D158`) - Healthy system status, completed installs, battery integrity.
  - Apple Amber (`#FF9F0A`) - Pending updates, review notices.
  - Apple Red (`#FF453A`) - Critical patches, high-priority actions, uninstall triggers.

### B. Typography Stack
- **Headings & Brand Title:** `Outfit` (600, 700) with `-0.02em` tracking.
- **UI Body & Labels:** Native Apple San Francisco (`-apple-system, BlinkMacSystemFont, "SF Pro Display", "SF Pro Text"`) on macOS; `Plus Jakarta Sans` and `Inter` on Windows.
- **Code, Versions & Hashes:** `JetBrains Mono` with tabular numerals enabled.

### C. Zero-Emoji Standard
- Zero Unicode emojis across the entire UI.
- All icons are precision inline vector SVGs or authentic brand SVGs streamed from `cdn.simpleicons.org/{slug}`.

---

## 3. Dynamic Platform Adaptability (`adaptPlatformUI`)

The shared web engine in `web/app.js` dynamically detects the runtime host and adapts labels without altering layout or styling:

```javascript
adaptPlatformUI() {
  this.isMac = (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.quantum) ||
               (navigator.userAgent && navigator.userAgent.includes('Mac')) ||
               (navigator.platform && navigator.platform.includes('Mac'));

  document.body.classList.toggle('platform-mac', !!this.isMac);
  document.body.classList.toggle('platform-windows', !this.isMac);

  // Keyboard shortcut indicators
  const spotlightBadge = document.getElementById('spotlight-shortcut-badge');
  if (spotlightBadge) {
    spotlightBadge.textContent = this.isMac ? '⌘K' : 'Ctrl+K';
  }

  // Windows-specific label harmonization
  if (!this.isMac) {
    document.querySelectorAll('.shortcut-pill').forEach(pill => {
      pill.textContent = pill.textContent.replace(/⌘/g, 'Ctrl+');
    });
    // Adapts Gatekeeper -> SmartScreen, APFS -> NTFS, Mac Health -> PC Health
  }
}
```

---

## 4. Development Workflow by Channel

### To Develop on macOS:
```bash
# 1. Switch to the macOS channel
git checkout channel/macos

# 2. Compile and package the Universal 2 DMG installer
./build_dmg.sh

# 3. Launch application directly
open "dist/Quantum Updater.app"
```

### To Develop on Windows:
```bash
# 1. Switch to the Windows channel
git checkout channel/windows

# 2. Run with Tauri dev mode
npm run tauri dev
# OR run with Python pywebview backend
python main.py --engine winget
```

### To Sync Shared UI Changes to Both Channels:
```bash
# Commit shared UI improvements in main or channel, then merge/rebase:
git checkout channel/macos
git merge main
git push origin channel/macos

git checkout channel/windows
git merge main
git push origin channel/windows
```
