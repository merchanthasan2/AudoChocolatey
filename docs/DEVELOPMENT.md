# Development

## Prerequisites
- Windows 10/11, Node 24+, npm 11+, Rust stable (`rustup default stable`), WebView2 (preinstalled on Windows 11).

## First run
    npm install
    npm run tauri dev

`tauri dev` starts Vite on http://localhost:5173 and opens the app window. Debug builds embed an `asInvoker` manifest, so this works from a normal terminal; the app shows "Running as admin: No (development build)".

## Release build
    npm run tauri build

This builds `app/` first (always, so the UI can never be stale), then the release exe with `requireAdministrator`, then the NSIS installer in `src-tauri/target/release/bundle/nsis/`. The build SHA shown in the app must equal `git rev-parse --short=8 HEAD`. A `-dirty` suffix means uncommitted changes were included; `unknown` means git wasn't available at build time.

## Checks (same as CI)
    npm run test:scripts       # node:test for version + policy scripts
    npm run check:versions     # package.json == app/package.json == Cargo.toml
    npm run check:policy       # no emoji, no {@html}
    npm run check --workspace app
    cd src-tauri && cargo fmt --check && cargo clippy --all-targets -- -D warnings && cargo test

`cargo` commands need `app/dist` to exist (`npm run build --workspace app`), because `tauri::generate_context!` embeds it.

## Inspecting the running app (DevTools without right-click)
Set `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` before launching the exe. Then open `http://127.0.0.1:9222/json` to get the page's DevTools WebSocket (works with Chrome's `chrome://inspect` or any CDP client).

## Bumping the version
    node scripts/bump-version.mjs 3.0.0-alpha.2

## Adding a Rust command
1. Write it with `#[tauri::command]` and register it in `lib.rs` `generate_handler!`.
2. Add its name to `COMMANDS` in `src-tauri/build.rs`.
3. Add `allow-<kebab-name>` to `src-tauri/capabilities/main.json`.
4. Add a typed wrapper in `app/src/lib/ipc/commands.ts` (the only file that calls `invoke`).

If you skip step 2 or 3, the call is rejected at runtime. That is intentional.
