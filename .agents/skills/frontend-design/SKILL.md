---
name: frontend-design
description: Desktop client UI engineering and high-fidelity glassmorphic frontend styling guidelines for Quantum Updater.
---

# Frontend Design Skill: Desktop Client Glassmorphism

## 1. Ergonomics & Window Architecture
- **Frameless Window Structure**: Custom header region with OS control buttons (minimize, maximize, close) and unified brand titlebar.
- **Fixed-Fluid Responsive Layout**:
  - 240px lateral navigation rail on the left.
  - Fluid main content pane accommodating Bento metric grids, filter bars, and inventory tables.
  - Floating bottom dock for asynchronous task queueing.

## 2. Micro-Interactions & GPU Acceleration
- **Backdrop Filters**: Use hardware-accelerated CSS `backdrop-filter: blur(24px)` with `-webkit-backdrop-filter`.
- **Transitions**: Keep animation timings snappy and predictable (`150ms - 220ms cubic-bezier(0.16, 1, 0.3, 1)`).
- **Specular Highlights**: Apply subtle `inset 0 1px 0 0 rgba(255, 255, 255, 0.12)` to glass card headers to emulate physical rim lighting.

## 3. Accessibility & Telemetry Contrast
- Ensure WCAG AAA compliance for critical text indicators:
  - White text on `#F04770` (Contrast ratio > 4.6:1)
  - `#06D7A0` on `#00161f` / `#073A4B` (Contrast ratio > 8.5:1)
  - `#FFD167` on deep slate backgrounds (Contrast ratio > 9.2:1)
- Never rely solely on color for status; always pair with a label or distinctive shape.
