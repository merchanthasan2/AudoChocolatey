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

## 3. Official 6-Color Palette Tokens & Glassmorphic Surfaces

Quantum Updater uses the **Uxintace 6-Color Design System** harmonized with frosted glassmorphism:

### 3.1 Master 6-Color Palette
| Token | Hex Value | Semantic Role |
|---|---|---|
| `--c-midnight-teal` | `#073A4B` | **Canvas & Deep Glass Surface**: Application backdrop, frosted panels, navigation chrome |
| `--c-ocean-blue` | `#108AB1` | **Primary Brand & Interactive Accent**: Navigation active pills, focus rings, tech links |
| `--c-emerald-mint` | `#06D7A0` | **System Health & Installed Status**: Up-to-date indicators, Authenticode badges, success states |
| `--c-golden-yellow` | `#FFD167` | **Pending Operations & Diff Callouts**: Update count badges, pending queue items, version diffs |
| `--c-coral-peach` | `#F78C6A` | **Categorization & Architecture Tags**: Category pills (`.badge-cat`), `x64`/`ARM64` tags, repo tags |
| `--c-coral-pink` | `#F04770` | **Executive CTAs & High-Priority Actions**: "Update All" (`.btn-coral`), Explore install triggers |

### 3.2 Dark Mode Surface Mapping (Default)
| Token | Value | Purpose |
|---|---|---|
| `--bg-canvas` | `#073A4B` | Deep midnight teal backdrop |
| `--bg-canvas-deep` | `#00161f` | Ultra-deep abyss layer |
| `--bg-surface` | `rgba(7, 58, 75, 0.78)` | Floating glass header and docks |
| `--bg-card` | `rgba(11, 47, 60, 0.72)` | Bento cards and package tiles |
| `--bg-card-hover` | `rgba(16, 68, 87, 0.85)` | Interactive card hover surface |
| `--border-glass` | `rgba(255, 255, 255, 0.10)` | Ambient 1px perimeter border |
| `--border-glass-hover` | `rgba(16, 138, 177, 0.50)` | Ocean blue focus ring on hover |
| `--border-card-specular` | `inset 0 1px 0 0 rgba(255, 255, 255, 0.14)` | Top specular rim highlight |
| `--primary` | `#108AB1` | Ocean Cerulean Blue |
| `--coral-pink` | `#F04770` | Vibrant Coral Pink CTA accent |
| `--coral-peach` | `#F78C6A` | Warm Coral Peach category accent |
| `--golden-yellow` | `#FFD167` | Sunny Golden Yellow diff accent |
| `--emerald-mint` | `#06D7A0` | Electric Emerald Mint status accent |

### 3.3 Light Mode (1-Click Switchable)
| Token | Value | Purpose |
|---|---|---|
| `--bg-canvas` | `#f1f7fa` | Crisp glacial pearl backdrop |
| `--bg-surface` | `rgba(255, 255, 255, 0.85)` | Semi-opaque frosted navigation |
| `--bg-card` | `rgba(255, 255, 255, 0.80)` | Glass card containers |
| `--border-glass` | `rgba(7, 58, 75, 0.12)` | Subtle deep teal perimeter divider |
| `--text-main` | `#073A4B` | Midnight teal high-contrast text |

---

## 4. Google Stitch MCP Integration & Design Assets

Quantum Updater layout and component styling is generated and maintained via Google Stitch MCP:
- **Project**: `projects/4437627295113959804` (`Quantum Updater - Desktop Client`)
- **Design System Asset**: `assets/5754614782694399956` (`Quantum Modern Prism`)
- **Screen Mockups**:
  - Screen 1: `stitch_screen_updates.png` — Updates Bento Dashboard & Quick Triage
  - Screen 2: `stitch_screen_explore.png` — Curated Explore Marketplace with AI & ML Software
  - Screen 3: `stitch_screen_security_queue.png` — Security Inspector & Sequential Operation Queue Drawer

---

## 5. Component Library Standards

### 5.1 Bento Metric Grid
Interactive 4-tile glass matrix:
- **Card 1 (Golden Yellow Glow - `#FFD167`)**: Updates Available (`#bento-updates-val`) with rotating refresh icon.
- **Card 2 (Ocean Blue Glow - `#108AB1`)**: Selected to Upgrade (`#bento-selected-val`) with zap glyph.
- **Card 3 (Emerald Mint Glow - `#06D7A0`)**: Software Health (`#bento-health-val`) with shield glyph.
- **Card 4 (Coral Peach Glow - `#F78C6A`)**: Package Engine (`#bento-engine-val`) with settings glyph.

### 5.2 Cross-Tab Global Operation Dock
- Sticky bar anchored to the bottom of the viewport (`z-index: 90`).
- Left: Animated pulse dot (Ocean Blue for active, Emerald Mint for done, Coral Pink for error) + Title + Ticker.
- Center: Track fill (`linear-gradient(90deg, #108AB1, #06D7A0)`) + Percent indicator.
- Right: "Queue" toggle button with counter badge.

### 5.3 Expandable Queue Drawer
- Sliding glass flyout anchored above the dock (`z-index: 100`).
- Displays running tasks with active Ocean Blue glow, pending tasks with Golden Yellow status badges, and completed tasks with Emerald Mint checkmarks.
- Includes "Clear Done" action and close control. Mutex collision safeguards guarantee strictly sequential execution.

### 5.4 Software Badges & Installed Representation
- **Not Installed**: Clean Coral Pink action button (`Install` `.btn-coral` with plus glyph).
- **Installed**:
  - `badge-installed-tag`: High-contrast Emerald Mint badge (`Installed` with checkmark glyph) displayed next to the app title.
  - Disabled Button: Grayed out `Installed` button (`btn-installed-disabled`) preventing redundant installation triggers.
  - Secondary Action: Compact `Reinstall` button for power user repair workflows.
- **Category Badges**:
  - `.badge-cat`: Styled with Warm Coral Peach (`#F78C6A`) pill background and border.

