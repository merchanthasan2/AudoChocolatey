# P4: The Real Interface (Warm Store + Springy Motion)

> **For agentic workers:** superpowers:executing-plans (Native). Steps use checkbox (`- [ ]`) syntax.
> **Format:** test cases for pure logic are the contract. Visual work is verified by screenshots of every view in light and dark, taken from the running app via CDP and compared against the approved mockups (option C, springy motion).

**Goal:** Replace the placeholder card with the usable app. Sidebar navigation leads to Marketplace (search plus popular apps), Updates (Update all), Installed (with Uninstall) and Settings. Every install, update and uninstall uses the P3 queue with the Get → ring → tick → Done animation. Light and dark themes are included.

**Architecture:** Svelte 5 runes stores (`*.svelte.ts`) wrap the typed IPC (`commands.ts`, `events.ts`). The queue store subscribes once to `job://*` and `queue://changed`. Components are small and presentational. A tiny state router replaces URL routing, since a desktop window has no address bar. Pure logic lives in `lib/logic/*.ts`, which is unit-tested with vitest.

**Spec:** §4.1–4.4 · **Mockups:** `.superpowers/brainstorm/1004-1791786529/content/{visual-style,motion}.html` (choices C + Springy)

## Global Constraints
- **Tokens:** light canvas `#F6F3EE`, card `#FFFFFF`, ink `#1D1B18`, muted `#7A7368`, accent `#1F5C45`, pill `#EFE9DF`. Dark canvas `#1A1816`, card `#24211E`, ink `#F1ECE4`, muted `#A39B8F`, accent `#5FB38E`, pill `#2E2A26`.
- **Type:** Inter Tight for headings (700, tracking −0.03em), Inter for body. Tabular numerals.
- **Motion (emil-design-eng applied):**
  - Springy (~420 ms, `cubic-bezier(0.34,1.56,0.64,1)`) **only for occasional moments**: search results arriving (staggered 50 ms), the JobButton morph and tick, Update-all completion and the counter roll, drawers and toasts.
  - **Frequent actions stay crisp or instant:** view switches use a 140 ms opacity fade only; hover and press respond in 120 ms or less with `scale(0.97)` on `:active`; **no animation on keyboard actions** (Ctrl+K focus).
  - Only `transform` and `opacity` are animated. Nothing enters from `scale(0)` (the minimum is 0.9 combined with opacity). Hover effects sit behind `(hover: hover)`.
  - Reduced motion (OS or setting) turns every movement into a short fade.
- **Honesty:**
  - A determinate ring only when `job.percent != null`; otherwise an indeterminate arc.
  - winget downloads show the size text when `bytesTotal` is known.
  - Error copy comes from `job.error`.
- **Copy:** plain words ("4 updates ready", "Everything is up to date", "Getting things ready…"). No "engine", and no emoji (CI-enforced).
- **Security:** no `{@html}` (CI-enforced). No remote images until P5's icon protocol; monogram tiles until then.
- **Icons:** colour monogram tiles (hue from a hash of the name, warm palette) until P5. UI glyphs are inline SVG (Lucide shapes, 1.75 stroke).

## Review Focus
1. **Slow or failed sources.** If winget takes 10 s or more, or a source errors, each view shows skeletons, then results, plus a calm inline banner per failing source. The app never shows a blank screen or a raw JSON error. Pinned in T2 (store sets `error`) and T6 (banner rendering in the screenshot run with a forced error).
2. **Rapid typing in search.** A response from an older query must never overwrite a newer one. Pinned in T2 (`latestOnly` unit test).
3. **The same package shown in several places** (Updates row, Installed row, Marketplace card) reflects one shared job state. Pinned in T2 (`jobFor(target)` lookup).
4. **The window resized to its minimum (960×640) and a 150% display scale.** Nothing overlaps or clips. Pinned in T6 (screenshots at 960×640).
5. **Very long app names and ids** (60+ characters). They truncate with an ellipsis and never push buttons off the row. Pinned in T6 (worst-case row in the screenshot run).

---

### T1: Foundations (tokens, motion, theme, shell, router)
**Files:**
- `app/src/lib/styles/{tokens,base}.css`
- `app/src/lib/motion/index.ts`
- `app/src/lib/stores/{router,settings}.svelte.ts`
- `app/src/lib/components/{Sidebar,Icon}.svelte`
- `app/src/App.svelte`

**Interfaces:**
- Router store: `router.view: 'marketplace' | 'updates' | 'installed' | 'settings'`, plus `go(view)`.
- Settings store: `settings.value`, `settings.load()`, `settings.update(patch)`. Theme is applied as `<html data-theme>` (system means none); motion as `<html data-motion>`.
- Motion module: `motion.reduced` (reactive: OS media query or setting), `spring(node, opts)` (Svelte transition, rise plus fade, falling back to a fade when reduced), `fade140`.
- Sidebar: brand mark, nav items with icon, label and the Updates badge (count), Queue pill slot, and version text in the footer.

**Test cases (vitest):**
- `resolveTheme('system', prefersDark)` gives `undefined`; `('dark', x)` gives `'dark'`.
- `resolveMotion('system', osReduced=true)` gives `reduced`; `('full', true)` gives `full`.

### T2: Stores and pure logic
**Files:**
- `app/src/lib/logic/{jobButton,latestOnly,format,monogram}.ts`
- `app/src/lib/stores/{overview,library,queue,search,toasts}.svelte.ts`
- vitest config plus tests in `app/src/lib/logic/*.test.ts`

**Interfaces:**
- `jobButtonState(job: Job | undefined, mode: 'install' | 'update' | 'uninstall')` returns `{ kind: 'idle' | 'queued' | 'progress' | 'working' | 'done' | 'failed' | 'skipped', percent: number | null, label: string, cancellable: boolean }`:
  - `idle` labels: Get / Update / Uninstall;
  - `queued`: "Queued";
  - `progress`: downloading with a percent;
  - `working`: preparing, waiting, downloading without a percent, installing or verifying (label from the phase: "Getting ready", "Waiting for another installer", "Downloading", "Installing", "Finishing");
  - `done`: succeeded (label "Done", "Restart needed" when `rebootRequired`);
  - `failed`, with retry;
  - `skipped`: "Up to date" or "Already installed" from `detail`.
- `latestOnly<T>()` returns `{ run(p: Promise<T>): Promise<T | typeof STALE> }`.
- `format`: `formatBytes(2009088)` gives `"1.9 MB"`; `pluralUpdates(1)` gives `"1 update ready"`; `relativeTime`.
- `monogram(name)` returns `{ letters, hue }`: the same name always gives the same hue; `"Visual Studio Code"` gives `"VS"`; `"7-Zip"` gives `"7Z"`; an empty name gives `"?"`.
- Library store:
  - state: `installed`, `updates` (each `{ items, sourceErrors, loading, loadedAt }`);
  - `refreshInstalled()`, `refreshUpdates()`;
  - auto-refresh both when a job reaches `succeeded`.
- Queue store:
  - `jobs` (by id), `active` (not finished), `jobFor(source, id, kind?)`;
  - `install(pkg)`, `update(candidate)`, `uninstall(pkg)`, `updateAll()`, `cancel(id)`, `retry(id)`, `clearFinished()`;
  - subscribes to events once.
- Search store: `query`, `results`, `loading`, `error`; debounced 300 ms, uses `latestOnly`, and needs at least 2 characters.

**Test cases (vitest):**
- `jobButtonState`:
  - `undefined` with 'update' gives idle "Update";
  - queued gives queued and cancellable;
  - downloading with 42.5 gives progress 42.5;
  - downloading with null gives working "Downloading";
  - installing gives working, not cancellable;
  - succeeded with reboot gives done "Restart needed";
  - skipped with detail "No applicable update" gives skipped "Up to date";
  - failed gives failed with the label from the error.
- `latestOnly`: resolving A (slow) then B (fast) in the order B, A gives a result for B, and A resolves to STALE.
- `monogram` and `format` cases as listed above.
- `jobFor` returns the newest job for a target, preferring an active job over finished ones.

### T3: Core components
**Files:** `app/src/lib/components/{AppIcon,AppRow,JobButton,ProgressRing,Skeleton,EmptyState,Banner,CountRoll,SearchField,Toasts,QueuePill,QueueDrawer,Segmented,Toggle}.svelte`

**Behaviour:**
- **JobButton** morphs in one element:
  - pill (`width` stays fixed; the content cross-fades with a 2 px blur);
  - a 28 px circle containing ProgressRing, with a stop square while cancellable (click cancels);
  - a check that draws in with a spring pop;
  - then back to a pill reading "Done" (or "Open later" in P6).
  - Failure: a short horizontal shake (reduced: none) and a red "Retry".
- **ProgressRing**: an SVG arc. Determinate uses `stroke-dashoffset` with a CSS transition (linear 120 ms); indeterminate is an 18% arc rotating with a 0.9 s linear animation.
- **CountRoll**: the old digit slides up and out while the new one slides in (spring), and plain text is announced to screen readers (`aria-live="polite"`).
- **QueuePill** (sidebar footer) shows "Installing Firefox · 2 more", with a mini ring, and opens **QueueDrawer**, a right-side sheet listing the jobs with cancel/retry and "Clear finished". The drawer uses the iOS-like curve `cubic-bezier(0.32,0.72,0,1)` at 300 ms.
- **Toasts** (bottom-right, stacked): "Firefox installed", "Couldn't update Discord · Retry"; auto-dismiss after 5 s, paused while hovered.
- **Banner**: per-source error or offline, calm tone, with an icon and one action.

**Tests:** logic-only (T2). Components are verified visually in T6.

### T4: Views
**Files:** `app/src/views/{Marketplace,Updates,Installed,Settings}.svelte` and `app/src/lib/catalog/popular.ts` (winget IDs verified with `winget show`).

- **Marketplace:**
  - a big friendly heading ("Find apps") with the search field (Ctrl+K focuses it from anywhere, with no animation);
  - while empty: "Popular apps", a grid of cards (icon, name, publisher, short description, Get button);
  - while searching: skeleton rows, then results arriving in a 50 ms stagger, each with a source badge ("winget" / "Chocolatey");
  - Chocolatey results show "Requires Chocolatey" when choco is missing (from overview).
- **Updates:**
  - heading `CountRoll` plus "updates ready", "Checked 2 minutes ago", Refresh, and an **Update all** button;
  - rows show `from → to` and the source with a per-row JobButton;
  - finished rows collapse out (spring);
  - when the list empties, an "Everything is up to date" empty state with a check pop.
- **Installed:** a filter field and count; managed rows have Uninstall; local-only rows show the muted text "Managed by Windows" and no button. The list is windowed when there are more than 200 rows (simple fixed-height virtualisation).
- **Settings:** Appearance (Theme segmented, Motion segmented); Sources (winget and Chocolatey status with version, a Chocolatey on/off toggle, "Show apps with unknown versions"); About (version, build, running as admin, settings folder).

### T5: Polish pass (emil-design-eng checklist)
- Run the review checklist over every component, output as a Before/After/Why table in the ledger. Fix every row.
- Keyboard: Tab order and focus rings (2 px accent, offset 2); Enter/Space on cards; Esc closes the drawer.
- Accessibility: `aria-label` on icon buttons; progress ring with `role="progressbar"` and `aria-valuenow` when determinate.

### T6: Visual verification and sign-off
- Debug build plus CDP, with screenshots of each view in light and dark at 1200×780 and at 960×640.
- Forced states via a CDP-injected store override: loading skeletons, a source error banner, the worst-case long name, the JobButton in each state, the drawer open.
- Live: one real search, and an Updates list of real data.
- Compare against the mockups and record the deltas in the ledger. Docs: DESIGN_SYSTEM.md rewritten with real tokens and components, plus PLAN ticks.
- Fresh review, then merge.
