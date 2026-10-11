# Findings & Technical Decisions

## Requirements Consolidated from Prompts
1. **Typesetting & UX Enhancements**:
   - Modern, human-designed typography: Outfit + Plus Jakarta Sans + Inter with JetBrains Mono for monospaced elements.
   - Fewer clicks, maximum operational transparency, high-contrast crisp readability.
   - Cryptographic trust & signed updates presentation (Authenticode, SHA-256 checksums, SmartScreen compliance).
2. **Zero Emojis**:
   - Total elimination of emojis across the application (nav, bento grid, pills, buttons, placeholders, dialogs, toasts, terminal).
   - Replaced by unified, crisp, professional inline vector SVGs.
3. **Official App Icons Fix**:
   - Fix dark / 50% transparent black overlay look on software logos.
   - Root cause identified: jsDelivr npm package for Simple Icons serves raw monochromatic `#000` SVGs. Switching to `https://cdn.simpleicons.org/{slug}` provides authentic brand-color SVGs natively.
4. **Winget Update Extraction Fix**:
   - Root cause identified: `winget upgrade` divides output into regular upgrades and "The following packages have an upgrade available, but require explicit targeting for upgrade:" (e.g., Discord, WSL).
   - `src-tauri/src/lib.rs` and `core/winget_engine.py` stopped parsing after the first table or failed on column width mismatches.
   - Both parsers need to extract all sections and handle column header positions dynamically.
5. **Explore Tab Enhancements**:
   - Dedicated "AI" software category with curated tools (Ollama, LM Studio, Jan, ChatGPT, Claude, Cursor, Superwhisper, etc.).
   - Installed apps detection: compare against installed software list. If installed, display "Installed" badge with checkmark and disable the install button.
6. **Global Install Status & Sequential Queue**:
   - Status lost when changing filters or switching tabs.
   - Solution: Global status bar / dock visible across all 5 tabs.
   - Queue system: Sequential FIFO execution prevents Windows Installer Error 1618 (Another installation is in progress).
7. **Error Handling & Docs**:
   - End-to-end exception handling with user feedback toasts.
   - New `docs/` folder containing PRD, Architecture, Design System, ADRs, Projects guide, and Prompts log.
8. **GitHub & CI/CD**:
   - Remote repository: `https://github.com/merchanthasan2/AudoChocolatey`.
   - Workflows: `ci.yml` (validation) and `release.yml` (automated packaging of NSIS installer and portable exe).

## Technical Decisions
| Decision | Rationale |
|---|---|
| Vector SVGs over emojis | Emojis look amateurish and render inconsistently across Windows versions. Inline SVGs provide crisp, scalable, theme-aware icons. |
| `cdn.simpleicons.org` | Serves official vector SVGs pre-colored with authentic brand hex codes, resolving the "dark overlay" visual artifact. |
| Positional + Regex Winget Parser | Column positions vary depending on terminal width and package names; combined positional + token validation ensures zero dropped packages. |
| Client-Side Operation Queue | Coordinates sequential execution across Tauri IPC and pywebview, maintaining reactive state regardless of active view or filter. |
