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

## Package-manager fixtures
Parser tests run against captured output in `src-tauri/tests/fixtures/` (stored byte-exact via `.gitattributes`, CRLF included). To recapture on your machine:

    winget list --accept-source-agreements --disable-interactivity > src-tauri/tests/fixtures/winget/list.en.txt
    winget upgrade --accept-source-agreements --disable-interactivity > src-tauri/tests/fixtures/winget/upgrade.en.txt
    winget search --query firefox --source winget --count 50 --accept-source-agreements --disable-interactivity > src-tauri/tests/fixtures/winget/search-firefox-sourced.en.txt
    choco list -r > src-tauri/tests/fixtures/choco/list-r.v2.txt
    choco outdated -r > src-tauri/tests/fixtures/choco/outdated-r.v2.txt

Exact row counts in the tests (for example `list.en.txt` = 245 rows) must be updated with the recapture. The German and Japanese fixtures are synthetic: they are padded by East-Asian display width independently of the Rust code.

## TypeScript bindings
Rust IPC types derive `ts_rs::TS`. `cargo test` regenerates `app/src/lib/ipc/bindings/*.ts` (the output folder is set in `src-tauri/.cargo/config.toml`). Commit the regenerated files: CI fails if they differ from what `cargo test` produces.

## Testing installs in development
Debug builds run without admin rights (`asInvoker`):
- **Installs:** winget asks for elevation per installer, so expect a UAC prompt.
- **Uninstalling machine-wide apps** fails with Windows Installer error 1603, because winget does not self-elevate uninstalls.

The release build always runs elevated, so neither applies there. To inspect the elevated release build, use `get_job_log` output or the in-app diagnostics: remote debugging via `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` does not cross the elevation boundary.
