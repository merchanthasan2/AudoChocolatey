# Design System: Warm Store (v3)

The look and feel of Quantum Updater, documented from what ships in `app/src`. When the code and this file disagree, fix one of them in the same commit.

**Direction:** "Warm Store": calm, approachable and premium, closer to a curated app store than a system utility. The owner chose it from three mockups on 2026-10-12. Motion is springy for moments and crisp for everyday actions.

## Tokens (`app/src/lib/styles/tokens.css`)

### Colour
| Token | Light | Dark | Use |
|---|---|---|---|
| `--canvas` | `#F6F3EE` | `#1A1816` | Window and sidebar background |
| `--canvas-2` | `#EFEBE4` | `#201E1B` | Main content area |
| `--card` | `#FFFFFF` | `#24211E` | Cards, lists, fields |
| `--ink` / `--ink-2` | `#1D1B18` / `#4A453E` | `#F1ECE4` / `#CFC8BD` | Text, strong and secondary |
| `--muted` / `--faint` | `#7A7368` / `#A8A196` | `#A39B8F` / `#756E64` | Meta text, placeholders |
| `--accent` | `#1F5C45` | `#5FB38E` | Primary actions, progress, selection |
| `--accent-soft` | `#E3EEE8` | `#1F3329` | Active rows, success badges |
| `--pill` | `#EFE9DF` | `#2E2A26` | Secondary buttons, segmented controls |
| `--danger` / `--danger-soft` | `#B3261E` / `#F9E5E3` | `#F2A39C` / `#3A2220` | Failures |
| `--warn-soft` / `--warn-ink` | `#F7EFDC` / `#7A5A12` | `#34291A` / `#E8C98A` | Banners |

**Theme selection:** `<html data-theme="light|dark">` from Settings. When the theme is "System", there is no attribute and `prefers-color-scheme` decides.

### Type
- **Display:** Inter Tight Variable, weight 700, tracking −0.03em. Used for `h1` (28 px) and `h2` (17 px).
- **Body:** Inter Variable, 13.5 px / 1.45. Tabular figures throughout (versions and sizes line up).
- Both are bundled through `@fontsource-variable`; there are no web font requests.

### Shape and depth
- **Radius:** cards 12 px; fields 10 to 12 px; pills fully round; app icons 22% (squircle-like).
- **Shadows:** `--shadow-card` is a hairline plus a soft shadow; `--shadow-raised` is for hover and drawers. No glassmorphism and no neon gradients.

## Motion (`tokens.css`, `app/src/lib/motion/index.ts`)
| Kind | Curve | Duration | Where |
|---|---|---|---|
| Moment | `--spring` `cubic-bezier(0.34,1.56,0.64,1)` | 420 ms | Results arriving (50 ms stagger), check pop, counter roll, empty state, toasts |
| Drawer | `--ease-drawer` `cubic-bezier(0.32,0.72,0,1)` | 300 ms | Activity drawer |
| View switch | linear opacity | 140 ms | Changing screens (frequent, so no movement) |
| Press | `--ease-out` | 120 ms | `scale(0.97)` on `:active` for every pressable |
| Exit | cubic out | 260 ms | Finished update rows collapse and slide out |

**Rules:**
- Animate only `transform` and `opacity`. The JobButton width morph is the one deliberate exception.
- Never animate from `scale(0)` (0.9 at minimum, always with opacity).
- Hover effects are gated behind `(hover: hover)`.
- Keyboard actions such as Ctrl+K are never animated.
- **Reduced motion:** the OS setting or Settings → Motion → Reduced sets `data-motion="reduced"`. Springs become ease-out at 160 ms, movement becomes fades, and spinners slow down.
- **Honesty:** a determinate progress ring or bar appears only when a real percentage exists. Otherwise there is an indeterminate arc or sliding bar.

## Components (`app/src/lib/components`)
- **JobButton.** One element that morphs through states:

  | State | Shows |
  |---|---|
  | idle | Get / Update / Uninstall |
  | queued | Pulsing dot |
  | working | 30 px ring, with a stop square while cancellable |
  | done | Accent circle with a check that draws in, then a quiet "Done" (or "Restart needed") |
  | skipped | "Up to date" |
  | failed | A red "Retry" with a short shake |

  The state mapping is `logic/jobButton.ts` (unit-tested).
- **AppRow.** Icon, name and meta line. While a job runs, the meta line becomes live status ("Downloading · 1.9 MB · 42%") and a 4 px progress bar appears. Running rows get an `--accent-soft` wash.
- **AppIcon.** Shows the app's original icon from `icon://` (see ADR 022). A colour monogram tile (hue hashed from the name) shows until the icon loads, or permanently if no original exists.
- **Checkbox.** 20 px, with a drawn tick, an indeterminate state for "select all", and scale on press.
- **Other components:**
  - ProgressRing, CountRoll (rolling number);
  - Skeleton (shimmer rows with staggered delays);
  - EmptyState (pop in), Banner (calm warning with one action);
  - Toasts (bottom right, at most 4, auto-dismiss, optional Retry);
  - QueuePill and QueueDrawer (activity);
  - Segmented, Toggle (spring knob), SearchField (Ctrl K hint, Esc clears);
  - Sidebar (Updates first, live count badge).
- **Marketplace pieces (P2):**
  - AppCard: 48 px icon, Get button top right, name, publisher, one-line blurb. The whole card opens details; the button only installs. Lifts 1 px on hover (pointer devices only).
  - BundleCard: up to six icons in a row, name, blurb, the app list, and one primary button that says how many apps it will install ("Install 4 missing"), or "All installed".
  - AppDetail: a 460 px panel that slides in from the right (300 ms ease-out; a fade under reduced motion) over a scrim. Esc, the scrim or the close button dismiss it. Shows install button, version, description, sources, website, license and package id.
- **Dark mode icons.** Original icons get a faint light edge in dark mode (`--icon-halo`) so dark logos (Spotify, GitHub, OBS) stay visible. The artwork itself is never recoloured.

## Layout
- The window is 1200×780 by default, with a minimum of 960×640.
- Sidebar: 232 px.
- Content: at most 1040 px wide, with 34/40 px padding, on `--canvas-2` with a 16 px top-left radius against the sidebar.
- Lists are single cards with hairline row dividers. The Marketplace grid uses `auto-fill minmax(220px, 1fr)`.

## Copy
Plain and human:
- Headlines: "4 updates ready", "Everything is up to date", "Find apps".
- Status: "Getting ready", "Waiting for another installer".

Never say "engine" and never use emoji (CI-enforced). Errors say what happened and offer one action ("Couldn't check Chocolatey … Try again").
