# Architecture Decision Records (ADRs)
## Quantum Updater
**Company:** Quantum Step ([quantumstep.in](https://quantumstep.in))

---

### ADR 001: Dual-Engine Architecture (Tauri v2 + Python Fallback)
- **Status:** Accepted
- **Context:** The application must run with maximum native performance, minimal memory footprint, and high reliability across Windows 10 and 11 environments.
- **Decision:** Implement Tauri v2 with Rust as the primary native release runtime (~9MB binary, <60MB RAM usage). Retain Python + pywebview as a local standalone development and scripting runtime sharing identical `web/` frontend assets.
- **Consequences:** Provides native compilation speed, embedded UAC manifest integration, and zero dependency on Node runtime at execution.

---

### ADR 002: Direct Brand-Colored CDN Integration for Simple Icons
- **Status:** Accepted
- **Context:** Icons loaded via raw npm package distributions (e.g., jsDelivr Simple Icons npm bundle) are hardcoded with monochrome `#000` (black) path fills. On dark-mode glass cards, this created an unreadable "50% dark overlay" appearance.
- **Decision:** Switch all icon resolutions to `https://cdn.simpleicons.org/{slug}`. This service dynamically delivers authentic brand-colored SVGs (e.g. green Spotify, blurple Discord, blue VS Code). For dark brands (e.g. GitHub, Apple, Steam), provide high-contrast white fallbacks.
- **Consequences:** Eliminates dark overlays, produces vibrant visual clarity, and maintains deterministic brand representation without local icon asset bloat.

---

### ADR 003: Sequential Asynchronous Operation Queue for Installations
- **Status:** Accepted
- **Context:** The Windows Installer MSI engine enforces a strict single-mutex constraint (`_MSIExecute`). Triggering parallel installations crashes background installers with fatal Error 1618. Furthermore, user feedback noted that active statuses were lost when navigating between tabs or filtering search results.
- **Decision:** Implement a client-side FIFO Sequential Operation Queue in `web/app.js` combined with a sticky, cross-tab Global Operation Dock and expandable Queue Drawer.
- **Consequences:** Guarantees zero MSI mutex collisions, ensures operations run reliably in sequence, and provides persistent visibility across all 5 navigation tabs.

---

### ADR 004: Manifest-Driven Single-Prompt UAC Elevation
- **Status:** Accepted
- **Context:** Installing packages typically triggers repetitive Windows User Account Control (UAC) prompts for every application.
- **Decision:** Embed an application manifest with `<requestedExecutionLevel level="requireAdministrator"/>` directly into the binary and provide zero-flash launchers (`Run_QuantumUpdater.vbs`).
- **Consequences:** Users authenticate elevation once upon application launch. All downstream package operations run seamlessly unattended in the background.

---

### ADR 005: Strict Zero-Emoji Enterprise UI Policy
- **Status:** Accepted
- **Context:** Emojis across system utility software look amateurish, inconsistently rendered across Windows builds, and lack precise alignment.
- **Decision:** Ban all emojis from code and layout. Replace every icon with tailored inline vector SVGs matching the Lucide / Heroicons standard (24x24 viewBox, stroke-based, 2px stroke-width).
- **Consequences:** Professional, uniform, enterprise-grade aesthetic that renders identically across Windows 10, Windows 11, and high-DPI displays.
