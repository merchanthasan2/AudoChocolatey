---
name: design-system-enforcer
description: Strictly enforces the 6-color palette, typography scale, zero-emoji policy, and glassmorphic design system tokens across all Quantum Updater desktop screens and UI components.
---

# Design System Enforcer: Quantum Modern Prism

## 1. Palette Specification (Mandatory 6-Color System)
All UI elements, backgrounds, text, badges, borders, and interactive states MUST use the following color tokens extracted directly from the official palette specification:

| Token | Hex Value | Role & Usage |
|---|---|---|
| `--c-midnight-teal` | `#073A4B` | **Base Canvas & Deep Slate**: Window backdrop, deep card surfaces, header substrate |
| `--c-ocean-blue` | `#108AB1` | **Primary Brand & Navigation**: Active navigation rails, focus outlines, primary selection, interactive links |
| `--c-emerald-mint` | `#06D7A0` | **Success & Telemetry**: "Up to Date" status, "Installed" pill, Authenticode verification pass, healthy metrics |
| `--c-golden-yellow` | `#FFD167` | **Pending & Notice**: "Updates Available (N)" count, queued status, warning highlights, featured badges |
| `--c-coral-peach` | `#F78C6A` | **Tags & Attributes**: Architecture tags (`x64`, `ARM64`), runtime categories, package repository monikers |
| `--c-coral-pink` | `#F04770` | **High-Priority Actions**: "Update All" executive trigger, single-click install, critical patches, destructive actions |

### Surface Elevation & Alpha Layering:
- **Surface Level 0 (Canvas)**: `#073A4B` (solid deep base) or `#00161f`
- **Surface Level 1 (Panels & Bento Cards)**: `rgba(7, 58, 75, 0.70)` with `backdrop-filter: blur(20px)` and 1px border `rgba(255, 255, 255, 0.08)`
- **Surface Level 2 (Active Cards & Rows)**: `rgba(16, 138, 177, 0.15)` with 1px border `rgba(16, 138, 177, 0.40)`
- **Surface Level 3 (Floating Docks & Modals)**: `rgba(4, 25, 33, 0.92)` with `backdrop-filter: blur(32px)`, 1px border `rgba(255, 255, 255, 0.16)`, and ambient shadow `0 20px 48px rgba(0, 0, 0, 0.5)`

---

## 2. Zero-Emoji Enforcement Policy
- **Absolute prohibition**: No Unicode emojis, glyphs, or dingbats are permitted anywhere in the UI.
- **Vector Icons**: All icons must be crisp inline vector SVGs (24x24 viewBox, stroke-width 2px) or authenticated brand SVGs from `cdn.simpleicons.org`.

---

## 3. Typography Standards
- **Display & Headings**: `Outfit` (weights: 600, 700, 800; tracking: -0.02em)
- **UI Body & Labels**: `Plus Jakarta Sans` or `Inter` (weights: 400, 500, 600; line-height: 1.5)
- **Code, Versions & Hashes**: `JetBrains Mono` (weights: 500, 600; tabular numerals enabled)

---

## 4. Component Rules
- **Buttons**:
  - High-Priority CTA: `#F04770` background, `#FFFFFF` text, subtle coral pink ambient glow on hover (`0 0 16px rgba(240, 71, 112, 0.35)`).
  - Primary Action: `rgba(16, 138, 177, 0.25)` background, `1px solid #108AB1`, `#FFFFFF` text.
  - Secondary / Utility: `rgba(255, 255, 255, 0.06)` background, `1px solid rgba(255, 255, 255, 0.12)`.
- **Badges**:
  - Up-to-Date: `rgba(6, 215, 160, 0.12)` background, `1px solid rgba(6, 215, 160, 0.40)`, `#06D7A0` text with 6px status dot.
  - Update Pending: `rgba(255, 209, 103, 0.12)` background, `1px solid rgba(255, 209, 103, 0.45)`, `#FFD167` text.
  - Installed (Explore): High-contrast `#06D7A0` text pill, grays out primary install button.
- **Operation Dock & Queue**:
  - Sticky bottom dock always visible.
  - Sequential FIFO queue prevents MSI mutex collisions (Error 1618).
