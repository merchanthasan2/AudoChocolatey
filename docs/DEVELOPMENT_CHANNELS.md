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
│ Mac app, left untouched         │             │ Windows app we are building     │
│ until Mac work starts again     │             │ Python window plus WebView2     │
│                                 │             │ Winget and Chocolatey           │
│                                 │             │ QuantumUpdater.exe              │
└─────────────────────────────────┘             └─────────────────────────────────┘
```

### Channel Definitions

| Channel Branch | Primary Target OS | Core Engine & Backend | Packaging Output |
|---|---|---|---|
| `channel/macos` | macOS (11.0 Big Sur to 15.x Sequoia) | Native Objective-C Cocoa / WebKit host (`src-macos/main.m`), IOKit, AVFoundation, Homebrew, Apple Software Update | Universal 2 Binary (`arm64` + `x86_64`), `dist/QuantumUpdater_2.0.0_macOS.dmg`, `dist/QuantumUpdater_macOS_Update.zip` |
| `channel/windows` | Windows 10 and 11 | Python window (`main.py`, `web_api.py`), shared screens in `web/`, Winget and Chocolatey | `dist\QuantumUpdater\QuantumUpdater.exe` |
| `main` | Production & Integration Trunk | Shared web frontend (`web/index.html`, `web/style.css`, `web/app.js`), documentation suite (`docs/`), CI/CD | Automated multi-target releases via GitHub Actions |

---

## 2. Shared look

Windows and Mac use the same screens, colours, type, and brand. The normal theme is Direction B (Deep Teal). Dark mode is Direction A (Graphite Prism). The emblem comes from `brand/`. App icons are the real program icon or the publisher's own icon. Each system adds only its own window material: WebView2 on Windows, and the Mac window material when that branch is picked up again. Mockups live in `design/mockups/`.

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

Mac stays on `channel/macos` and is not part of the current work. It starts after the Windows app is launch ready.

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

# 2. Build and start the Windows app
python build_exe.py QuantumUpdater
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
