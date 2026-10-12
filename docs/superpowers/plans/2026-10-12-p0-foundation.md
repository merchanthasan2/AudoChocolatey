# P0: Cleanup and Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce the repo to one runtime (Tauri + Rust) with a fresh Svelte 5 shell. The release exe really embeds the admin manifest, ships a locked-down CSP and capabilities, and displays its build SHA so stale builds are obvious.

**Architecture:** The root `package.json` is the single version source and an npm workspace containing `app/` (Svelte 5 + Vite + TS). Tauri builds the frontend automatically (`beforeBuildCommand`) from `app/dist`. `build.rs` embeds a release `requireAdministrator` manifest (debug: `asInvoker`), injects `BUILD_SHA`, and declares the app command list so only our commands are reachable. All old Rust commands are removed; P1 rebuilds them properly. Old code stays available in git at `e9a74b2`.

**Tech Stack:** Tauri 2.12, Rust stable (1.99), Svelte 5, Vite (from create-vite 9), TypeScript, Node 24 / npm 11, `node:test`.

**Spec:** [docs/superpowers/specs/2026-10-12-quantum-updater-v3-redesign-design.md](../specs/2026-10-12-quantum-updater-v3-redesign-design.md)

## Global Constraints
- Runtime is Tauri v2 + Rust only. No Python anywhere in the repo or CI.
- Version single source: root `package.json`. P0 version is `3.0.0-alpha.1`. `app/package.json` and `src-tauri/Cargo.toml` must match it exactly.
- Release exe: `requireAdministrator`. Debug exe: `asInvoker`. Both manifests include Common-Controls v6.
- CSP (release): `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: http://icon.localhost; font-src 'self'; connect-src ipc: http://ipc.localhost; object-src 'none'; base-uri 'none'; frame-src 'none'; form-action 'none'`
- `withGlobalTauri: false`. No remote fonts, scripts or images. Fonts are bundled via `@fontsource-variable/*`.
- JS capabilities: only `core:event:default`, `core:window:allow-{show,hide,set-focus,minimize,close}`, `core:app:allow-version`, plus our own `allow-<command>` entries.
- Zero emoji in `app/src` and `src-tauri/src`. No `{@html}` in `.svelte` files.
- Warm Store light tokens: canvas `#F6F3EE`, card `#FFFFFF`, ink `#1D1B18`, muted `#7A7368`, accent `#1F5C45`, pill `#EFE9DF`. Dark: canvas `#1A1816`, card `#24211E`, ink `#F1ECE4`, muted `#A39B8F`, accent `#5FB38E`, pill `#2E2A26`.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Every task that changes the state of the project ticks its box in `docs/PLAN.md` in the same commit.

## Review Focus
1. **Building without git or from a dirty tree** (zip download, CI shallow clone): the build must succeed and show `unknown` or `<sha>-dirty`, never fail. Pinned by T5 Step 9.
2. **`npm run tauri dev` from a normal (non-admin) terminal** must launch, not fail with OS error 740. Pinned by T5 Step 10.
3. **Svelte transitions under the production CSP.** Runtime-injected `<style>` must not be blocked; Tauri's style-src nonce injection would silently disable `'unsafe-inline'`. Pinned by T5 Step 11 (`dangerousDisableAssetCspModification` plus a console check).
4. **Version files with CRLF line endings and prerelease tags** (`v3.0.0-alpha.1`) must validate correctly. Pinned by T2 tests.
5. **Legitimate typographic characters** (`→ … · © –`) must not trip the emoji policy. Pinned by T3 tests.

---

### Task 1: Archive old docs and delete the dead runtimes

**Files:**
- Move: `HANDOVER.md`, `progress.md`, `findings.md`, `task_plan.md` → `docs/archive/`
- Delete (tracked): `main.py`, `web_api.py`, `core/`, `ui/`, `build_exe.py`, `requirements.txt`, `Run_QuantumUpdater.bat`, `Run_QuantumUpdater.vbs`, `Run_WinPackageHub.bat`, `Run_WinPackageHub.vbs`, `manifests/`
- Delete (untracked, stale build output): `build/`, `dist/`, `__pycache__/`, `WinPackageHub.spec`, `err.log`, `out.log`
- Modify: `.gitignore`
- Keep for now: `web/` (Tauri still builds from it until T5)

**Interfaces:** none.

- [ ] **Step 1: Confirm a clean baseline**

Run: `git status --short` (expected: empty) and `cargo test --manifest-path src-tauri/Cargo.toml`
Expected: `test result: ok. 3 passed`

- [ ] **Step 2: Move the old planning docs**

```bash
mkdir -p docs/archive
git mv HANDOVER.md progress.md findings.md task_plan.md docs/archive/
```

- [ ] **Step 3: Delete the tracked dead code**

```bash
git rm -r -q main.py web_api.py core ui build_exe.py requirements.txt \
  Run_QuantumUpdater.bat Run_QuantumUpdater.vbs Run_WinPackageHub.bat Run_WinPackageHub.vbs manifests
```

- [ ] **Step 4: Delete the untracked stale artifacts**

These folders hold the old v2 exes that caused the "I see nothing different" confusion. They can always be rebuilt.

```bash
rm -rf build dist __pycache__ WinPackageHub.spec err.log out.log
```

- [ ] **Step 5: Replace `.gitignore`**

```gitignore
# Rust
/src-tauri/target/
**/*.rs.bk

# Node (package-lock.json IS tracked for reproducible builds)
node_modules/

# Build output
dist/
*.exe
*.msi

# Logs & temp
*.log
*.tmp
.DS_Store
Thumbs.db

# Editors & tools
.vscode/
.idea/
.gemini/
.agents/*
!.agents/skills/
.playwright-mcp/
.superpowers/
```

- [ ] **Step 6: Verify the Rust side is untouched**

Run: `cargo test --manifest-path src-tauri/Cargo.toml`
Expected: `test result: ok. 3 passed`

Run: `git ls-files | grep -E '\.py$|Run_|manifests/'`
Expected: no output

- [ ] **Step 7: Tick `docs/PLAN.md` P0/T1, then commit**

```bash
git add -A
git commit -m "chore: remove Python runtime, launchers and stale manifests; archive v2 docs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Single version source and version scripts

**Files:**
- Create: `package.json`, `scripts/versions.mjs`, `scripts/check-versions.mjs`, `scripts/bump-version.mjs`
- Test: `scripts/versions.test.mjs`
- Modify: `src-tauri/Cargo.toml` (version → `3.0.0-alpha.1`)

**Interfaces:**
- Produces: `scripts/versions.mjs` exports `isValidVersion(v: string): boolean`, `readCargoVersion(text: string): string | null`, `replaceCargoVersion(text: string, version: string): string`, `collectVersions(root: string): Record<string, string|null>`, `findMismatches(versions: Record<string,string|null>, tag?: string): string[]`.
- Produces CLIs: `node scripts/check-versions.mjs [tag]` (exit 1 on mismatch); `node scripts/bump-version.mjs <version>`.
- Produces npm scripts: `test:scripts`, `check:versions`.

- [ ] **Step 1: Create root `package.json`**

```json
{
  "name": "quantum-updater",
  "private": true,
  "version": "3.0.0-alpha.1",
  "type": "module",
  "scripts": {
    "tauri": "tauri",
    "dev": "tauri dev",
    "build": "tauri build",
    "test:scripts": "node --test scripts/",
    "check:versions": "node scripts/check-versions.mjs"
  },
  "devDependencies": {
    "@tauri-apps/cli": "^2.12.2"
  }
}
```

- [ ] **Step 2: Write the failing tests** in `scripts/versions.test.mjs`

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isValidVersion, readCargoVersion, replaceCargoVersion, findMismatches } from './versions.mjs';

const CARGO = '[package]\nname = "quantum_updater"\nversion = "2.0.0"\n\n[dependencies]\nfoo = { version = "1.2.3" }\n';

test('accepts release and prerelease semver, rejects junk', () => {
  assert.ok(isValidVersion('3.0.0'));
  assert.ok(isValidVersion('3.0.0-alpha.1'));
  assert.ok(!isValidVersion('3.0'));
  assert.ok(!isValidVersion('v3.0.0'));
});

test('reads only the [package] version', () => {
  assert.equal(readCargoVersion(CARGO), '2.0.0');
});

test('reads version from CRLF Cargo.toml', () => {
  assert.equal(readCargoVersion(CARGO.replace(/\n/g, '\r\n')), '2.0.0');
});

test('replaces package version but not dependency versions', () => {
  const out = replaceCargoVersion(CARGO, '3.0.0-alpha.1');
  assert.equal(readCargoVersion(out), '3.0.0-alpha.1');
  assert.match(out, /foo = \{ version = "1\.2\.3" \}/);
});

test('preserves CRLF when replacing', () => {
  const out = replaceCargoVersion(CARGO.replace(/\n/g, '\r\n'), '3.0.0');
  assert.ok(out.includes('version = "3.0.0"\r\n'));
});

test('no mismatches when all files agree', () => {
  assert.deepEqual(findMismatches({ 'package.json': '3.0.0', 'src-tauri/Cargo.toml': '3.0.0' }), []);
});

test('reports a file that disagrees', () => {
  const p = findMismatches({ 'package.json': '3.0.0', 'src-tauri/Cargo.toml': '2.0.0' });
  assert.equal(p.length, 1);
  assert.match(p[0], /Cargo\.toml has 2\.0\.0/);
});

test('accepts matching prerelease tag with v prefix and refs/tags', () => {
  assert.deepEqual(findMismatches({ 'package.json': '3.0.0-alpha.1' }, 'refs/tags/v3.0.0-alpha.1'), []);
});

test('reports tag mismatch', () => {
  assert.equal(findMismatches({ 'package.json': '3.0.0' }, 'v3.0.1').length, 1);
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `npm run test:scripts`
Expected: FAIL with `Cannot find module ... versions.mjs`

- [ ] **Step 4: Implement `scripts/versions.mjs`**

```js
import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const SEMVER = /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/;
const PKG_VERSION_LINE = /^version\s*=\s*"([^"]+)"/m;

export function isValidVersion(v) {
  return typeof v === 'string' && SEMVER.test(v);
}

// The [package] table is the first table in Cargo.toml; it ends at the next line starting with "[".
function splitPackageSection(text) {
  const idx = text.search(/\r?\n\[/);
  return idx === -1 ? [text, ''] : [text.slice(0, idx), text.slice(idx)];
}

export function readCargoVersion(text) {
  const [head] = splitPackageSection(text);
  const m = head.match(PKG_VERSION_LINE);
  return m ? m[1] : null;
}

export function replaceCargoVersion(text, version) {
  const [head, tail] = splitPackageSection(text);
  if (!PKG_VERSION_LINE.test(head)) throw new Error('Cargo.toml [package] has no version line');
  return head.replace(PKG_VERSION_LINE, `version = "${version}"`) + tail;
}

export function collectVersions(root) {
  const json = (p) => JSON.parse(readFileSync(join(root, p), 'utf8')).version;
  const out = {
    'package.json': json('package.json'),
    'src-tauri/Cargo.toml': readCargoVersion(readFileSync(join(root, 'src-tauri/Cargo.toml'), 'utf8')),
  };
  if (existsSync(join(root, 'app/package.json'))) out['app/package.json'] = json('app/package.json');
  return out;
}

export function findMismatches(versions, tag) {
  const expected = versions['package.json'];
  const problems = [];
  if (!isValidVersion(expected)) problems.push(`package.json version "${expected}" is not valid semver`);
  for (const [file, v] of Object.entries(versions)) {
    if (v !== expected) problems.push(`${file} has ${v}, expected ${expected}`);
  }
  if (tag !== undefined) {
    const t = tag.replace(/^refs\/tags\//, '').replace(/^v/, '');
    if (t !== expected) problems.push(`tag ${tag} does not match ${expected}`);
  }
  return problems;
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `npm run test:scripts`
Expected: `# pass 9`, `# fail 0`

- [ ] **Step 6: Create the two CLIs**

`scripts/check-versions.mjs`:
```js
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { collectVersions, findMismatches } from './versions.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const versions = collectVersions(root);
const problems = findMismatches(versions, process.argv[2]);
if (problems.length) {
  console.error('Version check failed:\n  ' + problems.join('\n  '));
  process.exit(1);
}
console.log(`Versions OK: ${versions['package.json']}`);
```

`scripts/bump-version.mjs`:
```js
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { isValidVersion, replaceCargoVersion } from './versions.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const version = process.argv[2];
if (!isValidVersion(version)) {
  console.error('Usage: node scripts/bump-version.mjs <semver>, e.g. 3.0.0-alpha.2');
  process.exit(1);
}
for (const p of ['package.json', 'app/package.json']) {
  const file = join(root, p);
  if (!existsSync(file)) continue;
  const pkg = JSON.parse(readFileSync(file, 'utf8'));
  pkg.version = version;
  writeFileSync(file, JSON.stringify(pkg, null, 2) + '\n');
}
const cargo = join(root, 'src-tauri/Cargo.toml');
writeFileSync(cargo, replaceCargoVersion(readFileSync(cargo, 'utf8'), version));
console.log(`Bumped to ${version}. Run "cargo check" in src-tauri to refresh Cargo.lock.`);
```

- [ ] **Step 7: Bump, install, and verify**

```bash
npm install
node scripts/bump-version.mjs 3.0.0-alpha.1
cargo check --manifest-path src-tauri/Cargo.toml
npm run check:versions
```
Expected: `Versions OK: 3.0.0-alpha.1`, and `package-lock.json` is created.

Run: `node scripts/check-versions.mjs v9.9.9`
Expected: exit 1, `tag v9.9.9 does not match 3.0.0-alpha.1`

- [ ] **Step 8: Tick PLAN.md P0/T2, then commit**

```bash
git add package.json package-lock.json scripts/ src-tauri/Cargo.toml src-tauri/Cargo.lock docs/PLAN.md
git commit -m "build: root package.json as single version source with bump/check scripts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Frontend policy checker (no emoji, no `{@html}`)

**Files:**
- Create: `scripts/policy.mjs`, `scripts/check-frontend-policy.mjs`
- Test: `scripts/policy.test.mjs`
- Modify: `package.json` (add `check:policy` script)

**Interfaces:**
- Produces: `scripts/policy.mjs` exports `findViolations(text: string, file: string): Array<{file: string, line: number, rule: 'emoji'|'html', excerpt: string}>`.
- Produces npm script: `check:policy` (scans `app/src`, `app/index.html`, `src-tauri/src`; exit 1 on any violation).

- [ ] **Step 1: Write the failing tests** in `scripts/policy.test.mjs`

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findViolations } from './policy.mjs';

test('flags emoji with file and line', () => {
  const v = findViolations('ok\nconst s = "done \u{1F680}";\n', 'app/src/a.ts');
  assert.equal(v.length, 1);
  assert.equal(v[0].line, 2);
  assert.equal(v[0].rule, 'emoji');
});

test('flags dingbat check marks', () => {
  assert.equal(findViolations('<span>✓</span>', 'app/src/A.svelte').length, 1);
});

test('allows typographic characters', () => {
  assert.deepEqual(findViolations('1.94 → 1.95 … · © 2026 – ok', 'app/src/A.svelte'), []);
});

test('flags {@html} in svelte files', () => {
  const v = findViolations('<p>{@html desc}</p>', 'app/src/views/AppDetail.svelte');
  assert.equal(v.length, 1);
  assert.equal(v[0].rule, 'html');
});

test('ignores {@html} text outside svelte files', () => {
  assert.deepEqual(findViolations('// never use {@html}', 'app/src/lib/x.ts'), []);
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm run test:scripts`
Expected: FAIL with `Cannot find module ... policy.mjs`

- [ ] **Step 3: Implement `scripts/policy.mjs`**

```js
// Astral-plane symbols (emoji), misc symbols & dingbats, misc technical, stars, !! and !?, and the emoji variation selector.
const EMOJI = /[\u{10000}-\u{10FFFF}\u{2600}-\u{27BF}\u{2300}-\u{23FF}\u{2B50}-\u{2B55}\u{203C}\u{2049}\u{FE0F}]/u;

export function findViolations(text, file) {
  const out = [];
  const isSvelte = file.endsWith('.svelte');
  text.split(/\r?\n/).forEach((line, i) => {
    if (EMOJI.test(line)) out.push({ file, line: i + 1, rule: 'emoji', excerpt: line.trim().slice(0, 80) });
    if (isSvelte && line.includes('{@html')) out.push({ file, line: i + 1, rule: 'html', excerpt: line.trim().slice(0, 80) });
  });
  return out;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npm run test:scripts`
Expected: all tests pass (9 version + 5 policy).

- [ ] **Step 5: Create `scripts/check-frontend-policy.mjs`**

```js
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, relative, extname } from 'node:path';
import { findViolations } from './policy.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const EXTS = new Set(['.svelte', '.ts', '.js', '.css', '.html', '.rs', '.json']);
const TARGETS = ['app/src', 'app/index.html', 'src-tauri/src'];

function* walk(p) {
  if (!existsSync(p)) return;
  if (statSync(p).isFile()) { yield p; return; }
  for (const name of readdirSync(p)) yield* walk(join(p, name));
}

const violations = [];
for (const t of TARGETS) {
  for (const file of walk(join(root, t))) {
    if (!EXTS.has(extname(file))) continue;
    const rel = relative(root, file).replace(/\\/g, '/');
    violations.push(...findViolations(readFileSync(file, 'utf8'), rel));
  }
}
for (const v of violations) console.error(`${v.file}:${v.line} [${v.rule}] ${v.excerpt}`);
if (violations.length) { console.error(`${violations.length} policy violation(s).`); process.exit(1); }
console.log('Frontend policy OK: no emoji, no {@html}.');
```

Add to `package.json` scripts: `"check:policy": "node scripts/check-frontend-policy.mjs"`

- [ ] **Step 6: Run against the current repo**

Run: `npm run check:policy`
Expected: `Frontend policy OK` (`app/` doesn't exist yet; `src-tauri/src` is scanned). If the legacy `lib.rs` is flagged, note it: that file is replaced in T5.

- [ ] **Step 7: Tick PLAN.md P0/T3, then commit**

```bash
git add scripts/ package.json docs/PLAN.md
git commit -m "build: add frontend policy checker (no emoji, no {@html}) with tests

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Scaffold `app/` (Svelte 5 + Vite + TS, Warm Store placeholder)

**Files:**
- Create: `app/` via create-vite, then replace demo files with:
  - `app/index.html`, `app/vite.config.ts`
  - `app/src/main.ts`, `app/src/App.svelte`
  - `app/src/lib/styles/fonts.css`, `app/src/lib/styles/tokens.css`
  - `app/src/lib/ipc/commands.ts`
- Delete: `app/src/lib/Counter.svelte`, `app/src/assets/`, `app/public/vite.svg`, `app/src/app.css`, `app/README.md`
- Modify: root `package.json` (add `"workspaces": ["app"]`), `app/package.json` (name, version)

**Interfaces:**
- Produces: `app/src/lib/ipc/commands.ts` exports `type Overview = { appVersion: string; buildSha: string; isElevated: boolean }` and `getOverview(): Promise<Overview>` (invokes Rust command `get_overview`, implemented in T5).
- Produces CSS custom properties used by every later UI task: `--canvas --card --ink --muted --accent --accent-ink --pill --hairline --radius-card --radius-icon --font-display --font-body --spring-duration --spring-ease`.
- Produces npm workspace scripts: `npm run dev|build|check --workspace app`.

- [ ] **Step 1: Scaffold**

```bash
npm create vite@9.2.1 app -- --template svelte-ts --no-interactive
rm -rf app/src/lib/Counter.svelte app/src/assets app/public/vite.svg app/src/app.css app/README.md
```

- [ ] **Step 2: Wire up the workspace**

In root `package.json`, add `"workspaces": ["app"]`.

In `app/package.json`, set `"name": "quantum-updater-app"`, `"version": "3.0.0-alpha.1"`, `"private": true`. Keep the template's `dev`, `build`, `check`, `preview` scripts.

Then:
```bash
npm install
npm install --workspace app @tauri-apps/api@^2 @fontsource-variable/inter@^5 @fontsource-variable/inter-tight@^5
```

- [ ] **Step 3: Write `app/vite.config.ts`**

```ts
import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

// Tauri serves the built files from app/dist; dev server port must match tauri.conf.json devUrl.
export default defineConfig({
  plugins: [svelte()],
  clearScreen: false,
  server: { port: 5173, strictPort: true },
  build: { target: 'chrome105', outDir: 'dist', emptyOutDir: true },
});
```

- [ ] **Step 4: Write `app/index.html`** (no external links of any kind)

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Quantum Updater</title>
  </head>
  <body>
    <div id="app"></div>
    <script type="module" src="/src/main.ts"></script>
  </body>
</html>
```

- [ ] **Step 5: Write the styles**

`app/src/lib/styles/fonts.css`:
```css
@import '@fontsource-variable/inter';
@import '@fontsource-variable/inter-tight';
```

`app/src/lib/styles/tokens.css`:
```css
:root {
  --canvas: #f6f3ee;
  --card: #ffffff;
  --ink: #1d1b18;
  --muted: #7a7368;
  --accent: #1f5c45;
  --accent-ink: #ffffff;
  --pill: #efe9df;
  --hairline: rgba(60, 40, 10, 0.07);
  --radius-card: 12px;
  --radius-icon: 9px;
  --font-display: 'Inter Tight Variable', 'Segoe UI Variable Display', system-ui, sans-serif;
  --font-body: 'Inter Variable', 'Segoe UI Variable Text', system-ui, sans-serif;
  --spring-duration: 420ms;
  --spring-ease: cubic-bezier(0.34, 1.56, 0.64, 1);
  color-scheme: light;
}

@media (prefers-color-scheme: dark) {
  :root {
    --canvas: #1a1816;
    --card: #24211e;
    --ink: #f1ece4;
    --muted: #a39b8f;
    --accent: #5fb38e;
    --accent-ink: #10201a;
    --pill: #2e2a26;
    --hairline: rgba(255, 240, 220, 0.08);
    color-scheme: dark;
  }
}

@media (prefers-reduced-motion: reduce) {
  :root { --spring-duration: 160ms; --spring-ease: ease-out; }
}

*, *::before, *::after { box-sizing: border-box; }
html, body { margin: 0; height: 100%; }
body {
  background: var(--canvas);
  color: var(--ink);
  font-family: var(--font-body);
  font-size: 14px;
  -webkit-font-smoothing: antialiased;
  font-feature-settings: 'tnum' 1;
}
```

- [ ] **Step 6: Write the IPC wrapper** `app/src/lib/ipc/commands.ts`

```ts
import { invoke } from '@tauri-apps/api/core';

// The only module allowed to call invoke(). P1 replaces these hand-written types with ts-rs bindings.
export type Overview = { appVersion: string; buildSha: string; isElevated: boolean };

export const getOverview = () => invoke<Overview>('get_overview');
```

- [ ] **Step 7: Write `app/src/main.ts` and `app/src/App.svelte`**

`app/src/main.ts`:
```ts
import { mount } from 'svelte';
import './lib/styles/fonts.css';
import './lib/styles/tokens.css';
import App from './App.svelte';

export default mount(App, { target: document.getElementById('app')! });
```

`app/src/App.svelte`:
```svelte
<script lang="ts">
  import { onMount } from 'svelte';
  import { fly } from 'svelte/transition';
  import { backOut } from 'svelte/easing';
  import { getOverview, type Overview } from './lib/ipc/commands';

  let overview = $state<Overview | null>(null);
  let error = $state<string | null>(null);

  onMount(() => {
    getOverview().then((o) => (overview = o)).catch((e) => (error = String(e)));
  });
</script>

<main>
  <section class="card" in:fly={{ y: 14, duration: 420, easing: backOut }}>
    <h1>Quantum Updater</h1>
    <p class="lede">Version 3 is being rebuilt. This is the foundation build.</p>
    {#if overview}
      <dl>
        <dt>Version</dt><dd>{overview.appVersion}</dd>
        <dt>Build</dt><dd class="mono">{overview.buildSha}</dd>
        <dt>Running as admin</dt><dd>{overview.isElevated ? 'Yes' : 'No (development build)'}</dd>
      </dl>
    {:else if error}
      <p class="error">Could not reach the app core: {error}</p>
    {:else}
      <p class="muted">Connecting…</p>
    {/if}
  </section>
</main>

<style>
  main { min-height: 100%; display: grid; place-items: center; padding: 32px; }
  .card {
    background: var(--card);
    border-radius: var(--radius-card);
    box-shadow: 0 0 0 1px var(--hairline), 0 8px 24px -12px rgba(60, 40, 10, 0.18);
    padding: 28px 32px;
    width: min(440px, 100%);
  }
  h1 { font-family: var(--font-display); font-size: 28px; font-weight: 700; letter-spacing: -0.035em; margin: 0; }
  .lede { color: var(--muted); margin: 4px 0 20px; }
  dl { display: grid; grid-template-columns: auto 1fr; gap: 8px 16px; margin: 0; }
  dt { color: var(--muted); }
  dd { margin: 0; font-weight: 600; }
  .mono { font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace; }
  .muted { color: var(--muted); }
  .error { color: #b3261e; }
</style>
```

- [ ] **Step 8: Verify the frontend builds cleanly**

Run: `npm run check --workspace app`
Expected: `svelte-check found 0 errors and 0 warnings`

Run: `npm run build --workspace app`
Expected: `app/dist/index.html` exists. Built CSS contains `@font-face` rules with local `/assets/*.woff2` URLs and no `fonts.googleapis.com`.

Run: `grep -r "googleapis\|gstatic\|cdn\." app/dist || echo clean`
Expected: `clean`

Run: `npm run check:policy`
Expected: `Frontend policy OK`

- [ ] **Step 9: Tick PLAN.md P0/T4, then commit**

```bash
git add app/ package.json package-lock.json docs/PLAN.md
git commit -m "feat(app): scaffold Svelte 5 + Vite shell with Warm Store tokens and bundled fonts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Rust shell: manifests, BUILD_SHA, command manifest, capabilities, CSP, `get_overview`

**Files:**
- Delete: `web/` (the old frontend)
- Replace: `src-tauri/src/lib.rs`, `src-tauri/build.rs`, `src-tauri/tauri.conf.json`, `src-tauri/windows/manifest.xml`
- Create: `src-tauri/windows/manifest.debug.xml`, `src-tauri/capabilities/main.json`, `src-tauri/src/commands/mod.rs`, `src-tauri/src/commands/system.rs`
- Modify: `src-tauri/Cargo.toml` (drop `tauri-plugin-opener`, `regex`, `tokio`; P1 re-adds what it needs)

**Interfaces:**
- Consumes: `app/dist` (T4), root `package.json` version (T2).
- Produces: the Rust command `get_overview() -> Overview { app_version: String, build_sha: String, is_elevated: bool }`, serialized camelCase (`appVersion`, `buildSha`, `isElevated`). Pure helper: `commands::system::build_overview(is_elevated: bool) -> Overview`. Helper `commands::system::is_elevated() -> bool`.
- Produces: the `COMMANDS` list in `build.rs`. Every future command must be added there **and** as `allow-<kebab-name>` in `capabilities/main.json`.

- [ ] **Step 1: Remove the old frontend and legacy commands**

```bash
git rm -r -q web
```

Replace `src-tauri/src/lib.rs` (the old parser and commands live in git at `e9a74b2:src-tauri/src/lib.rs` for P1 reference):
```rust
mod commands;

pub fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![commands::system::get_overview])
        .run(tauri::generate_context!())
        .expect("error while running Quantum Updater");
}
```

`src-tauri/src/commands/mod.rs`:
```rust
pub mod system;
```

- [ ] **Step 2: Write the failing test** in `src-tauri/src/commands/system.rs`

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn overview_reports_package_version_and_sha() {
        let o = build_overview(true);
        assert_eq!(o.app_version, env!("CARGO_PKG_VERSION"));
        assert!(!o.build_sha.is_empty());
        assert!(o.is_elevated);
    }

    #[test]
    fn overview_serializes_camel_case() {
        let json = serde_json::to_value(build_overview(false)).unwrap();
        assert!(json.get("appVersion").is_some());
        assert!(json.get("buildSha").is_some());
        assert_eq!(json["isElevated"], false);
    }
}
```

- [ ] **Step 3: Write both manifests**

`src-tauri/windows/manifest.xml` (release):
```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <assemblyIdentity version="1.0.0.0" processorArchitecture="*" name="QuantumStep.QuantumUpdater" type="win32"/>
  <description>Quantum Updater by Quantum Step</description>
  <dependency>
    <dependentAssembly>
      <assemblyIdentity type="win32" name="Microsoft.Windows.Common-Controls" version="6.0.0.0"
        processorArchitecture="*" publicKeyToken="6595b64144ccf1df" language="*"/>
    </dependentAssembly>
  </dependency>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="requireAdministrator" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

`src-tauri/windows/manifest.debug.xml`: identical, except `level="asInvoker"` and `<description>Quantum Updater (development build)</description>`.

- [ ] **Step 4: Write `src-tauri/build.rs`**

```rust
use std::process::Command;

/// Every #[tauri::command] must be listed here AND granted as `allow-<kebab-name>` in capabilities/main.json.
const COMMANDS: &[&str] = &["get_overview"];

fn git(args: &[&str]) -> Option<String> {
    let out = Command::new("git").args(args).output().ok()?;
    if !out.status.success() {
        return None;
    }
    Some(String::from_utf8_lossy(&out.stdout).trim().to_string())
}

fn main() {
    // Build identity shown in Settings > About, so a stale exe is obvious. Never fails the build.
    let sha = git(&["rev-parse", "--short=8", "HEAD"])
        .filter(|s| !s.is_empty())
        .unwrap_or_else(|| "unknown".into());
    let dirty = git(&["status", "--porcelain"]).is_some_and(|s| !s.is_empty());
    println!("cargo:rustc-env=BUILD_SHA={sha}{}", if dirty { "-dirty" } else { "" });
    println!("cargo:rerun-if-changed=../.git/HEAD");
    println!("cargo:rerun-if-changed=../.git/logs/HEAD");
    println!("cargo:rerun-if-changed=../.git/index");
    println!("cargo:rerun-if-changed=windows");

    // Release requires admin; debug runs as the invoking user so `tauri dev` works from a normal terminal.
    let manifest = if std::env::var("PROFILE").as_deref() == Ok("release") {
        include_str!("windows/manifest.xml")
    } else {
        include_str!("windows/manifest.debug.xml")
    };

    let attrs = tauri_build::Attributes::new()
        .windows_attributes(tauri_build::WindowsAttributes::new().app_manifest(manifest))
        .app_manifest(tauri_build::AppManifest::new().commands(COMMANDS));
    tauri_build::try_build(attrs).expect("tauri-build failed");
}
```

- [ ] **Step 5: Write `src-tauri/capabilities/main.json`**

```json
{
  "$schema": "../gen/schemas/desktop-schema.json",
  "identifier": "main",
  "description": "Main window: core event/window basics plus Quantum Updater's own commands. No shell, fs, opener, dialog or updater access from JS.",
  "windows": ["main"],
  "permissions": [
    "core:event:default",
    "core:window:allow-show",
    "core:window:allow-hide",
    "core:window:allow-set-focus",
    "core:window:allow-minimize",
    "core:window:allow-close",
    "core:app:allow-version",
    "allow-get-overview"
  ]
}
```

- [ ] **Step 6: Replace `src-tauri/tauri.conf.json`**

```json
{
  "$schema": "https://schema.tauri.app/config/2",
  "productName": "Quantum Updater",
  "version": "../package.json",
  "identifier": "in.quantumstep.quantumupdater",
  "build": {
    "beforeDevCommand": "npm run dev --workspace app",
    "devUrl": "http://localhost:5173",
    "beforeBuildCommand": "npm run build --workspace app",
    "frontendDist": "../app/dist"
  },
  "app": {
    "withGlobalTauri": false,
    "windows": [
      {
        "label": "main",
        "title": "Quantum Updater",
        "width": 1200,
        "height": 780,
        "minWidth": 960,
        "minHeight": 640,
        "center": true
      }
    ],
    "security": {
      "csp": "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: http://icon.localhost; font-src 'self'; connect-src ipc: http://ipc.localhost; object-src 'none'; base-uri 'none'; frame-src 'none'; form-action 'none'",
      "devCsp": "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: http://icon.localhost; font-src 'self'; connect-src ipc: http://ipc.localhost ws://localhost:5173; object-src 'none'; base-uri 'none'; frame-src 'none'; form-action 'none'",
      "dangerousDisableAssetCspModification": ["style-src"],
      "freezePrototype": true
    }
  },
  "bundle": {
    "active": true,
    "publisher": "Quantum Step",
    "targets": ["nsis"],
    "icon": ["icons/icon.ico"],
    "windows": {
      "nsis": {
        "installMode": "perMachine",
        "languages": ["English"],
        "displayLanguageSelector": false
      }
    }
  }
}
```

`dangerousDisableAssetCspModification: ["style-src"]` is deliberate. If it were left out, Tauri would add a nonce to `style-src`. Browsers ignore `'unsafe-inline'` when a nonce is present, which would block the `<style>` tags that Svelte transitions inject at runtime. `script-src` keeps Tauri's hash injection.

- [ ] **Step 7: Trim `src-tauri/Cargo.toml` dependencies**

```toml
[build-dependencies]
tauri-build = { version = "2", features = [] }

[dependencies]
tauri = { version = "2", features = ["tray-icon"] }
serde = { version = "1", features = ["derive"] }
serde_json = "1"
windows-sys = { version = "0.59", features = ["Win32_Foundation", "Win32_UI_Shell"] }
```

- [ ] **Step 8: Implement `system.rs`** above its tests

```rust
use serde::Serialize;

#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct Overview {
    pub app_version: String,
    pub build_sha: String,
    pub is_elevated: bool,
}

pub fn build_overview(is_elevated: bool) -> Overview {
    Overview {
        app_version: env!("CARGO_PKG_VERSION").to_string(),
        build_sha: env!("BUILD_SHA").to_string(),
        is_elevated,
    }
}

pub fn is_elevated() -> bool {
    // SAFETY: IsUserAnAdmin takes no arguments and has no preconditions.
    unsafe { windows_sys::Win32::UI::Shell::IsUserAnAdmin() != 0 }
}

#[tauri::command]
pub fn get_overview() -> Overview {
    build_overview(is_elevated())
}
```

Run: `npm run build --workspace app` (so `app/dist` exists), then `cargo test --manifest-path src-tauri/Cargo.toml`
Expected: `test result: ok. 2 passed`

Run: `cargo clippy --manifest-path src-tauri/Cargo.toml --all-targets -- -D warnings` and `cargo fmt --manifest-path src-tauri/Cargo.toml --check`
Expected: no warnings, no diff.

- [ ] **Step 9: Verify builds without git still succeed** (Review Focus 1)

Run (bash): `GIT_DIR=/nonexistent cargo build --manifest-path src-tauri/Cargo.toml`
Expected: build succeeds. Then: `cargo test --manifest-path src-tauri/Cargo.toml` (normal env) rebuilds with the real SHA.

- [ ] **Step 10: Verify dev mode from a normal terminal** (Review Focus 2)

From a **non-elevated** terminal: `npm run tauri dev`
Expected: the window opens with the warm card sliding up. It shows Version `3.0.0-alpha.1`, Build `<8-char sha>` (or `-dirty`), and Running as admin `No (development build)`. No "OS error 740".

- [ ] **Step 11: Verify the release build and CSP** (Review Focus 3)

```bash
npm run tauri build -- --debug --no-bundle
```
Run `src-tauri/target/debug/quantum_updater.exe`, right-click → Inspect → Console.
Expected: no `Content Security Policy` errors; the card animates in.

```bash
npm run tauri build
```
Expected: `src-tauri/target/release/bundle/nsis/Quantum Updater_3.0.0-alpha.1_x64-setup.exe` is produced.

Run (PowerShell): `Select-String -Path src-tauri/target/release/quantum_updater.exe -Pattern requireAdministrator -SimpleMatch -Quiet`
Expected: `True`

Run (PowerShell): `Select-String -Path src-tauri/target/debug/quantum_updater.exe -Pattern asInvoker -SimpleMatch -Quiet`
Expected: `True`

Double-click `src-tauri/target/release/quantum_updater.exe`.
Expected: a UAC prompt; after approving, "Running as admin: Yes" and Build equal to `git rev-parse --short=8 HEAD`.

- [ ] **Step 12: Tick PLAN.md P0/T5, then commit**

```bash
git add -A src-tauri web docs/PLAN.md
git commit -m "feat(core): Tauri shell with embedded admin manifest, build SHA, locked CSP and capabilities

Removes the legacy web/ frontend and unvalidated commands; P1 rebuilds sources properly.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: CI and release workflow cleanup

**Files:**
- Replace: `.github/workflows/ci.yml`, `.github/workflows/release.yml`

**Interfaces:**
- Consumes: npm scripts `test:scripts`, `check:versions`, `check:policy` (T2/T3) and the workspace `check`/`build` scripts (T4).
- Produces: CI gates that all later phases rely on. P7 rewrites `release.yml` fully (signing, updater, winget).

- [ ] **Step 1: Write `.github/workflows/ci.yml`**

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  frontend:
    name: Frontend, scripts & policy
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 24
          cache: npm
      - run: npm ci
      - run: npm run test:scripts
      - run: npm run check:versions
      - run: npm run check:policy
      - run: npm run check --workspace app
      - run: npm run build --workspace app

  rust:
    name: Rust fmt, clippy & tests
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 24
          cache: npm
      - run: npm ci
      - name: Build frontend (tauri::generate_context! needs app/dist)
        run: npm run build --workspace app
      - uses: dtolnay/rust-toolchain@stable
        with:
          components: rustfmt, clippy
      - uses: Swatinem/rust-cache@v2
        with:
          workspaces: src-tauri -> target
      - working-directory: src-tauri
        run: cargo fmt --check
      - working-directory: src-tauri
        run: cargo clippy --all-targets -- -D warnings
      - working-directory: src-tauri
        run: cargo test
```

- [ ] **Step 2: Write `.github/workflows/release.yml`** (minimal; P7 replaces it)

```yaml
name: Release

on:
  push:
    tags: ['v*']

permissions:
  contents: write

jobs:
  build:
    name: Build NSIS installer
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 24
          cache: npm
      - run: npm ci
      - name: Tag must match package version
        run: node scripts/check-versions.mjs ${{ github.ref_name }}
      - uses: dtolnay/rust-toolchain@stable
      - uses: Swatinem/rust-cache@v2
        with:
          workspaces: src-tauri -> target
      - name: Build
        run: npm run tauri build
      - name: Publish release
        uses: softprops/action-gh-release@v2
        with:
          files: src-tauri/target/release/bundle/nsis/*.exe
          prerelease: ${{ contains(github.ref_name, '-') }}
          generate_release_notes: true
```

- [ ] **Step 3: Validate the workflow YAML locally**

Run: `node -e "for (const f of ['ci','release']) { const t = require('fs').readFileSync('.github/workflows/'+f+'.yml','utf8'); if (/python|pip |\bweb\//i.test(t)) throw new Error(f+' still references Python or web/'); } console.log('workflows clean')"`
Expected: `workflows clean`

- [ ] **Step 4: Tick PLAN.md P0/T6, commit, push, and confirm CI is green**

```bash
git add .github/workflows docs/PLAN.md
git commit -m "ci: drop Python jobs; add frontend checks, clippy -D warnings and version gate

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push origin main
gh run watch --exit-status
```
Expected: both `frontend` and `rust` jobs pass. If one fails, fix it in a follow-up commit before starting T7.

---

### Task 7: Documentation update (project memory) and P0 sign-off

**Files:**
- Modify: `docs/DECISIONS.md`, `docs/ARCHITECTURE.md`, `README.md`, `docs/PLAN.md`
- Create: `docs/DEVELOPMENT.md`

**Interfaces:** none (docs only).

- [ ] **Step 1: Update `docs/DECISIONS.md`**

Under the title, insert a status note:
```markdown
> v3 redesign (2026-10-12): ADR 001–004 are superseded or amended below. Source of truth: docs/superpowers/specs/2026-10-12-quantum-updater-v3-redesign-design.md
```

Set statuses on the existing ADRs:
- ADR 001 → `Superseded by ADR 006`
- ADR 002 → `Superseded by ADR 015`
- ADR 003 → `Superseded by ADR 007 (queue moves to Rust in P3)`
- ADR 004 → `Amended by ADR 008`
- ADR 005 stays `Accepted` (now CI-enforced by `scripts/check-frontend-policy.mjs`)

Append these ADRs. Each uses the existing format (Status / Context / Decision / Consequences), 3–6 lines each:
- **ADR 006 Tauri + Rust only.** The Python runtime is removed. Context: the dual runtime drifted, and a stale exe hid the UI changes. Consequence: one codebase; `beforeBuildCommand` makes stale UI builds impossible.
- **ADR 007 Combined winget + Chocolatey catalog.** Merge and dedupe via curated mappings, then a conservative heuristic; owner-wins updates. Consequence: the user never picks an engine; a wrong merge is treated as worse than a duplicate.
- **ADR 008 Always admin, properly embedded.** `build.rs` embeds the release `requireAdministrator` / debug `asInvoker` manifests; the `.bat`/`.vbs` launchers are removed. Consequence: the security hardening in ADR 009 is mandatory.
- **ADR 009 Hardening for an elevated WebView.** Strict CSP, no remote content, explicit command manifest + capabilities, validated ID newtypes, no shell, URL allowlist, honest logs only.
- **ADR 010 Svelte 5 + Vite frontend** in `app/`.
- **ADR 011 Warm Store visual language** (option C) with light and dark themes.
- **ADR 012 Springy motion** (~420 ms soft overshoot); instant hover/press; reduced-motion fades.
- **ADR 013 Distribution:** NSIS, signed self-update, Authenticode signing inside `tauri build`, winget via komac.
- **ADR 014 Background:** tray, scheduler, toasts; autostart via an elevated Scheduled Task.
- **ADR 015 Layered icons:** Windows extraction, then bundled curated icons, then a cached site icon, then a monogram.
- **ADR 016 Proprietary licence.**
- **ADR 017 Root `package.json` is the single version source;** CI blocks mismatches.
- **ADR 018 `docs/PLAN.md` is the live tracker;** every task updates it in the same commit.

- [ ] **Step 2: Mark `docs/ARCHITECTURE.md` as transitional**

Insert at the top:
```markdown
> **Transitional (v3 rebuild in progress).** The sections below describe v2 and are being replaced phase by phase.
> Current target architecture: docs/superpowers/specs/2026-10-12-quantum-updater-v3-redesign-design.md §5.
> As of P0: Tauri shell + Svelte `app/`, single command `get_overview`; Python runtime and `web/` removed. Full rewrite lands in P8.
```

- [ ] **Step 3: Create `docs/DEVELOPMENT.md`**

```markdown
# Development

## Prerequisites
- Windows 10/11, Node 24+, npm 11+, Rust stable (`rustup default stable`), WebView2 (preinstalled on Windows 11).

## First run
    npm install
    npm run tauri dev

`tauri dev` starts Vite on http://localhost:5173 and opens the app window. Debug builds embed an `asInvoker` manifest, so this works from a normal terminal; the app shows "Running as admin: No (development build)".

## Release build
    npm run tauri build

Builds `app/` first (always, so the UI can never be stale), then the release exe with `requireAdministrator`, then the NSIS installer in `src-tauri/target/release/bundle/nsis/`. The build SHA shown in the app must equal `git rev-parse --short=8 HEAD`; `-dirty` means uncommitted changes were included.

## Checks (same as CI)
    npm run test:scripts       # node:test for version + policy scripts
    npm run check:versions     # package.json == app/package.json == Cargo.toml
    npm run check:policy       # no emoji, no {@html}
    npm run check --workspace app
    cd src-tauri && cargo fmt --check && cargo clippy --all-targets -- -D warnings && cargo test

## Bumping the version
    node scripts/bump-version.mjs 3.0.0-alpha.2

## Adding a Rust command
1. Write it with `#[tauri::command]` and register it in `lib.rs` `generate_handler!`.
2. Add its name to `COMMANDS` in `src-tauri/build.rs`.
3. Add `allow-<kebab-name>` to `src-tauri/capabilities/main.json`.
4. Add a typed wrapper in `app/src/lib/ipc/commands.ts` (the only file that calls `invoke`).
If you skip step 2 or 3, the call is rejected at runtime. That is intentional.
```

- [ ] **Step 4: Rewrite `README.md`**

````markdown
# Quantum Updater

A Windows app by [Quantum Step](https://quantumstep.in) to discover, install and keep software up to date, using winget and Chocolatey together.

**Status:** v3 is being rebuilt. Plan and progress: [docs/PLAN.md](docs/PLAN.md). Design: [docs/superpowers/specs/](docs/superpowers/specs/).

## Develop
```
npm install
npm run tauri dev
```
See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Licence
Proprietary. Copyright Quantum Step. All rights reserved.
````

- [ ] **Step 5: Final P0 verification**

```bash
npm run test:scripts && npm run check:versions && npm run check:policy && npm run check --workspace app
cargo test --manifest-path src-tauri/Cargo.toml
git ls-files | grep -E '\.py$|^web/|Run_|manifests/' || echo "no legacy files"
```
Expected: all green, and `no legacy files`.

- [ ] **Step 6: Update `docs/PLAN.md`**

- Tick T7. Set the P0 row to `Done (<sha>)`. Set Status to `P0 done · P1 next`. Update "Last updated".
- Add a Course corrections row for anything that differed from this plan (for example, a create-vite flag that changed). Write "none" if nothing differed.

- [ ] **Step 7: Commit and push**

```bash
git add docs README.md
git commit -m "docs: v3 ADRs, development guide, README; mark P0 complete

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push origin main
```
