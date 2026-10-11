# Design System & UI/UX Specification: Quantum Updater
**Company:** Quantum Step ([quantumstep.in](https://quantumstep.in))  
**Design Philosophy:** Modern Glassmorphism & Enterprise Clarity

---

## 1. Design Philosophy & Aesthetic Core
Quantum Updater rejects generic templates in favor of a handcrafted, hardware-accelerated **Frosted Glass (Acrylic Glassmorphism)** aesthetic inspired by Windows 11 Fluent 2 and macOS Sonoma.

### Guiding Principles:
1. **Zero-Emoji Policy**: No emojis are permitted anywhere in the user interface. All visual signifiers utilize precision inline vector SVGs (24x24 viewBox, stroke-based, 2px stroke-width).
2. **Authentic Brand Representation**: Logos use authentic vector assets served pre-colored via `cdn.simpleicons.org`. Dark brand marks receive high-contrast treatment to avoid blending with dark backgrounds.
3. **Information Density with Breathing Room**: The Bento Grid architecture segments complex status metrics into digestible, high-contrast glass cards with distinct ambient glows.
4. **Immediate Spatial Orientation**: The persistent Global Operation Dock ensures users never lose sight of active operations, regardless of tab navigation or search filtering.

---

## 2. Typography Specification

### Font Families:
- **Display Headings (`--font-display`)**: `Outfit`, `Plus Jakarta Sans`, sans-serif.
  - Used for: Brand title, Bento metrics, section titles, modal headers.
  - Characteristics: Geometric elegance, tight tracking (`letter-spacing: -0.025em`), bold weights (600, 700, 800).
- **Body & Controls (`--font-sans`)**: `Plus Jakarta Sans`, `Inter`, -apple-system, BlinkMacSystemFont, sans-serif.
  - Used for: Paragraph descriptions, table rows, button labels, badges.
  - Characteristics: Optical sizing, high legibility, `-webkit-font-smoothing: antialiased`, `line-height: 1.55`.
- **Code & Versioning (`--font-mono`)**: `JetBrains Mono`, Consolas, monospace.
  - Used for: Package versions, package IDs, hashes, terminal logs.
  - Characteristics: Distinctive ligatures, crisp tabular numerals.

---

## 3. Color Tokens & Glassmorphic Surfaces

### 3.1 Dark Mode (Default)
| Token | Value | Purpose |
|---|---|---|
| `--bg-canvas` | `#07090e` | Deep obsidian backdrop |
| `--bg-surface` | `rgba(14, 20, 34, 0.75)` | Floating glass header and docks |
| `--bg-card` | `rgba(19, 27, 46, 0.65)` | Bento cards and package tiles |
| `--bg-card-hover`| `rgba(28, 40, 68, 0.75)` | Interactive hover surface |
| `--border-glass` | `rgba(255, 255, 255, 0.08)` | Ambient 1px perimeter border |
| `--border-card-specular` | `inset 0 1px 0 0 rgba(255, 255, 255, 0.12)` | Specular ambient top rim light |
| `--primary` | `#6366f1` | Electric Indigo brand accent |
| `--cyan` | `#06b6d4` | Secondary technology accent |
| `--emerald` | `#10b981` | Verification & success accent |
| `--amber` | `#f59e0b` | Update pending & warning accent |
| `--rose` | `#f43f5e` | Failure & destructive accent |

### 3.2 Light Mode (1-Click Switchable)
| Token | Value | Purpose |
|---|---|---|
| `--bg-canvas` | `#f1f5f9` | Clean pearl backdrop |
| `--bg-surface` | `rgba(255, 255, 255, 0.82)` | Semi-opaque frosted navigation |
| `--bg-card` | `rgba(255, 255, 255, 0.75)` | Glass card containers |
| `--border-glass` | `rgba(0, 0, 0, 0.08)` | Subtle perimeter divider |
| `--text-main` | `#0f172a` | Deep slate high-contrast text |

---

## 4. Component Library Standards

### 4.1 Bento Metric Grid
Interactive 4-tile glass matrix:
- **Card 1 (Amber Glow)**: Updates Available (`#bento-updates-val`) with rotating refresh icon.
- **Card 2 (Indigo Glow)**: Selected to Upgrade (`#bento-selected-val`) with zap glyph.
- **Card 3 (Emerald Glow)**: Software Health (`#bento-health-val`) with shield glyph.
- **Card 4 (Cyan Glow)**: Package Engine (`#bento-engine-val`) with settings glyph.

### 4.2 Cross-Tab Global Operation Dock
- Sticky bar anchored to the bottom of the viewport (`z-index: 90`).
- Left: Animated pulse dot (Indigo for active, Emerald for done, Rose for error) + Title + Ticker.
- Center: Track fill (`linear-gradient(90deg, #6366f1, #06b6d4)`) + Percent indicator.
- Right: "Queue" toggle button with counter badge.

### 4.3 Expandable Queue Drawer
- Sliding glass flyout anchored above the dock (`z-index: 100`).
- Displays running tasks with an active glow, pending tasks with clean status badges, and completed tasks.
- Includes "Clear Done" action and close control.

### 4.4 Software Badges & Installed Representation
- **Not Installed**: Clean primary action button (`Install` with plus glyph).
- **Installed**:
  - `badge-installed-tag`: High-contrast emerald badge (`Installed` with checkmark glyph) displayed next to the app title.
  - Disabled Button: Grayed out `Installed` button (`btn-installed-disabled`) preventing redundant installation triggers.
  - Secondary Action: Compact `Reinstall` button for power user repair workflows.
