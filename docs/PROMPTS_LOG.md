# Prompts & Requirement History Log
## Quantum Updater
**Repository:** [https://github.com/merchanthasan2/AudoChocolatey](https://github.com/merchanthasan2/AudoChocolatey)  
**Maintained by:** Quantum Step ([quantumstep.in](https://quantumstep.in))

---

## 1. Comprehensive Prompt Traceability Matrix

### Prompt 1: Typography, UX & Cryptographic Security
> *"Can you amend the typesetting fonts to look more human designed, I want the look and fel to be enhanced. Use your stitch skills work out designs based on popular best looking apps in the industry i want you to ensure the design, look and feel & the Unser Interaction is done best fewer clicks, more transparancy on what is hapennin from their apps. Used Signed updates so we are not messing up someone's system or installing something that has a virus or a malware, please"*
- **Implementation:**
  - Upgraded typography tokens to human-designed fonts: `Outfit` (headings/display), `Plus Jakarta Sans` & `Inter` (body/UI), and `JetBrains Mono` (code/versions).
  - Enhanced frosted acrylic glassmorphism, ambient mesh orbs, and specular borders.
  - Added Security & Cryptographic Trust Strip highlighting SHA-256 validation, Authenticode digital signatures, and Microsoft SmartScreen compliance.
  - Reduced friction: 1-click update triggers and streamlined selection.

### Prompt 2: Childish Emojis Elimination
> *"Can you get rid of all the childish emojee's across the application, please"*
- **Implementation:**
  - Purged 100% of emojis across the entire codebase (`index.html`, `style.css`, `app.js`).
  - Replaced all visual icons with crisp, professional inline vector SVGs matching the 24x24 Lucide / Heroicons standard.
  - Updated buttons, tabs, Bento tiles, trust pills, modals, toasts, and console logs.

### Prompt 3: Official Brand Icons Overlay & Update Extraction Fix
> *"The official icons seem to be dark like a 50% transparent black overlay on them. Or they have been created dark and black. I want you to ensure this is fixed. Also The updater app is not able to extract those applications on the system that need updates. It's failing for some reason and completing. Have all the updates done once the... already installed apps in explorer should show installed icon below the install. and install should be grayed out."*
- **Implementation:**
  - **Icon Overlay Root Cause & Fix:** Discovered raw npm Simple Icons SVGs on jsDelivr use hardcoded black `#000` path fills. Switched to `https://cdn.simpleicons.org/{slug}` which delivers authentic brand colors. Added dynamic contrast/white fallbacks for dark brands (GitHub, Apple, Steam, etc.).
  - **Update Extraction Fix:** Rewrote winget upgrade parser into a multi-table, fixed-column positional slicer in both Rust (`src-tauri/src/lib.rs`) and Python (`core/winget_engine.py`). Correctly captures both normal upgrades and explicit targeting upgrades (e.g. Discord, WSL) without dropping or misaligning columns.
  - **Explore Installed State:** Integrated `isPackageInstalled()` check. When an app is detected on the system, renders an "Installed" badge, grays out the install action button, and offers a secondary "Reinstall" action.

### Prompt 4: AI Software Category
> *"We're missing a "AI" catagory in the explore option"*
- **Implementation:**
  - Added dedicated "AI & Machine Learning" category pill in the Explore tab.
  - Curated top AI desktop software: Ollama, LM Studio, Jan AI, OpenAI ChatGPT Desktop, Claude Desktop, Cursor AI Editor, Superwhisper, Upscayl, and Pinokio.

### Prompt 5: Global Error Handling & Architectural Documentation
> *"I now want you to go through the entire app and ensure we have error handling wherever necessary. Ensure we are documenting the architecture, the design, the decissions, the full PRD, the projects and the prompt documentationm in a seperate docs folder. Ensure I want all this done proactively so have this as your Global..."*
- **Implementation:**
  - Added defensive error handling across frontend (`app.js`), Tauri IPC (`lib.rs`), and Python backend (`web_api.py`, `core/`).
  - Created complete `docs/` folder: `PRD.md`, `ARCHITECTURE.md`, `DESIGN_SYSTEM.md`, `DECISIONS.md`, `PROJECTS.md`, and `PROMPTS_LOG.md`.

### Prompt 6: Global Status Bar & Sequential Operation Queue
> *"Few bugs I found. When installing the status does not show there. Ideally if the filter changes one can lose site of the status, have a global install status on each tab, so whatever is hapenning across the tabs can be known. we also need queuing so that we ave better prespective and control over installation or uninstallation for slower..."*
- **Implementation:**
  - Built sticky, cross-tab **Global Operation Dock** visible across all 5 navigation tabs.
  - Implemented an asynchronous **Sequential Operation Queue Manager** (`enqueueTask()`, `processNextQueueItem()`). Prevents Windows Installer MSI mutex collisions (Error 1618).
  - Added expandable **Queue Drawer** flyout with real-time status pills and "Clear Done" action.

### Prompt 7: GitHub Repository & CI/CD Pipeline
> *"https://github.com/merchanthasan2/AudoChocolatey Github repo for you. Ensure you are doing CI/CD"*
- **Implementation:**
  - Connected repository to remote `https://github.com/merchanthasan2/AudoChocolatey.git`.
  - Created `.github/workflows/ci.yml` (multi-step validation: Rust `cargo check`, Python compilation, Web assets).
  - Created `.github/workflows/release.yml` (automated packaging of Tauri v2 NSIS setup wizard and portable standalone executable on GitHub release tags).
