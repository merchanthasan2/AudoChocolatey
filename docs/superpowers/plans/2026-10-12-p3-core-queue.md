# P3 core: Install/Update Queue with Real Progress

> **For agentic workers:** superpowers:executing-plans (owner chose Native execution). Steps use checkbox (`- [ ]`) syntax.
>
> **Format note:** same as P1. Test cases are the contract (exact inputs and expected outputs), code is written under TDD, and interfaces are exact.

**Goal:** Install, upgrade and uninstall apps from winget and Chocolatey through one Rust-owned FIFO queue, with honest progress, safe cancel, and events the P4 interface binds to.

**Owner's ordering decision (2026-10-12):** P3 core, then P4 interface, then P2. This plan holds only what a usable app needs. Job persistence/recovery, ignore/pin rules and batch bundles move to "P3 extras" after P4.

**Architecture:**
- **Streaming runner** (`StreamRunner` trait, `SystemStreamRunner` implementation): spawns argv-only processes, assigns them to a Win32 **Job Object** (`KILL_ON_JOB_CLOSE`) so cancel and timeout kill the whole tree, and splits stdout/stderr on CR and LF.
- **Worker** (`jobs::worker`): one tokio task owns a `VecDeque<Job>`. Before each job it waits for the `Global\_MSIExecute` mutex. Each job runs with a per-source **ProgressTracker** and is classified by an exit-code table.
- **Events:** state changes go to a `JobSink` trait. Tests use a recording sink; the app uses `TauriSink` (`job://changed`, `job://progress`, `queue://changed`).

**Spec:** §5.5, §6, §7 · **Facts:** `docs/superpowers/plans/2026-10-12-p1-rust-core.md` facts section, plus the two below.

## Facts discovered 2026-10-12
- **Piped winget prints no progress bar.** `download-7zip.en.txt`: `Downloading <url>`, `Successfully verified installer hash`, `Installer downloaded…`. ConPTY was tried and hung during setup, so it was abandoned.
  - **Winget progress therefore comes from bytes on disk.** The installer URL comes from `winget show --id <id> --exact [--version v]` (`Installer Url:`), `HEAD` gives `Content-Length`, and the tracker polls the size of the newest file under `%TEMP%\WinGet\` every 250 ms while the job is in `downloading`.
  - If either number is unknown, `percent` stays `null` (indeterminate). Nothing is invented.
- **Chocolatey prints `Progress: Downloading <id> <ver>... NN%`** when piped (format per Chocolatey source). This is parsed directly.
- **winget return codes** are verified from Microsoft's `returnCodes.md` (copy in `tests/fixtures/winget/returnCodes.md`).

## Global Constraints
- Spec §8 validation still applies: ops arrive as `OpRequest` (validated `SourceRef`); argv only from `winget::args::build_op` / `choco::args::build_op`.
- One job at a time. The `_MSIExecute` mutex is waited on, not raced.
- **Cancel:** `queued` jobs are removed; `preparing`/`downloading` jobs are killed (Job Object) and become `cancelled`; `installing`/`verifying` jobs are **not cancellable** (`cancellable: false`, and `cancel_job` returns `invalid_input`).
- Progress events are throttled to 10 Hz per job. `percent` is `null` unless it is a real measurement.
- Executables come from `ExePaths` (P1 trust rules, ADR 020).
- Remove the P1 `#[allow(dead_code)]` markers on `OpRequest`, `OpKind`, `SourceRef` and `build_op` (they are now used).

## Review Focus
1. **Cancel during download kills winget and the installer it spawned** (no orphan `msiexec`/setup process). Pinned by the T1 integration test (`cmd` → `ping` tree killed).
2. **A job that never prints anything** (a silent installer) still finishes, shows indeterminate progress, and never stays `downloading` forever. Pinned in T4 (a scripted silent job reaches `succeeded`).
3. **Exit codes meaning "already up to date" or "no applicable update"** (`0x8A15002B`, `0x8A150061`) are not shown as failures. Pinned in T2.
4. **Reboot-required codes** (winget `0x8A150109`, MSI 3010/1641, choco 3010) mean succeeded with `rebootRequired: true`. Pinned in T2.
5. **Enqueuing the same package twice** while it is queued or running is a no-op that returns the existing job ID. Pinned in T4.

---

### T1: Streaming runner with Job Object
**Files:** `src-tauri/src/sources/stream.rs`; windows-sys features `Win32_System_JobObjects`, `Win32_System_Threading`.

**Interfaces:**
- `LineSplitter::push(&mut self, chunk: &str) -> Vec<String>` and `finish(self) -> Option<String>`. Splits on `\r` and `\n`, drops empty lines, strips ANSI.
- `#[async_trait] trait StreamRunner: Send + Sync { async fn run(&self, spec: &CommandSpec, on_line: &(dyn Fn(String) + Send + Sync), cancel: CancellationToken) -> AppResult<Option<i32>> }`. Returns the exit code; a cancel returns `Err(AppError cancelled)`.
- `CancellationToken`: a small `Arc<AtomicBool>` plus `tokio::sync::Notify` wrapper with `cancel()` and `is_cancelled()`.

**Test cases:**
- Splitter: `push("a\r\nb")` gives `["a"]`; then `push("c\rd\n")` gives `["bc", "d"]`. It keeps the text after the last `\r` as separate lines in order and never yields an empty string. `finish()` returns the trailing partial line.
- Integration: running `cmd /c "echo one & echo two"` streams `one` and `two` and returns `Some(0)`.
- Integration: running `cmd /c ping -n 30 127.0.0.1` and cancelling after 300 ms:
  - returns `Err(cancelled)` within 2 s;
  - afterwards, no `PING.EXE` process started after the test began is still alive (checked via `tasklist /FI "IMAGENAME eq PING.EXE"`).
- Integration: a timeout behaves the same and returns `Err(timeout)`.

### T2: Job model and exit classification
**Files:** `src-tauri/src/jobs/{mod,model,exit_codes}.rs`

**Interfaces (TS-exported, camelCase):**
- `JobPhase`: `queued | preparing | waiting | downloading | installing | verifying | succeeded | failed | cancelled | skipped`. `waiting` means "another installer is running".
- `Job { id: string, op: OpRequest, displayName: string, phase: JobPhase, detail: string | null, percent: number | null, bytesDone: number | null, bytesTotal: number | null, cancellable: boolean, rebootRequired: boolean, error: string | null, startedAt: string | null, endedAt: string | null }`.
- `Job::cancellable_in(phase)` is true for queued, preparing, waiting and downloading.
- `Outcome { Succeeded { reboot: bool }, Skipped(String), Failed(String) }` and `exit_codes::classify(source: SourceKind, kind: OpKind, code: Option<i32>) -> Outcome`.

**Test cases:**
- winget 0 gives Succeeded(no reboot).
- winget `-1978334967` (`0x8A150109`), `3010` and `1641` give Succeeded(reboot).
- winget `-1978335189` (`0x8A15002B`) gives Skipped("No applicable update"), and `-1978335135` (`0x8A150061`) on install gives Skipped("Already installed").
- winget `-1978334975` (`0x8A150101`) gives Failed with a message containing "currently running".
- winget `-1978334971` gives Failed containing "no more space".
- An unknown code gives Failed containing the hex `0x…`.
- choco 0 gives Succeeded; 3010 and 1641 give Succeeded(reboot); 1 gives Failed("Chocolatey reported an error"); `None` (killed) gives Failed("Stopped").
- `Job` JSON uses camelCase, and `phase` serializes as `"downloading"`.

### T3: Progress trackers
**Files:** `src-tauri/src/jobs/progress.rs`, plus a `src-tauri/src/sources/winget/details.rs` parser for `winget show`.

**Interfaces:**
- `enum Signal { Phase(JobPhase), Percent(f32), Bytes { done: u64, total: Option<u64> } }`.
- `trait LineParser { fn feed(&mut self, line: &str) -> Vec<Signal>; }`
- `WingetLines`:
  - `Downloading http` gives `Phase(Downloading)`;
  - `Successfully verified installer hash`, `Starting package install` or `Starting package uninstall` give `Phase(Installing)`;
  - `Successfully installed`, `Successfully uninstalled` or `Successfully upgraded` give `Phase(Verifying)`;
  - a `([\d.]+)\s*(KB|MB|GB)\s*/\s*([\d.]+)\s*(KB|MB|GB)` line gives `Bytes`, in case winget ever prints one.
- `ChocoLines`: `Progress: Downloading .* (\d{1,3})%` gives `Phase(Downloading)` plus `Percent`; `Installing|The install of .* was successful|Chocolatey upgraded` give the phases.
- `winget::details::parse_show(stdout) -> ShowInfo { installer_url: Option<String>, homepage: Option<String>, publisher: Option<String>, description: Option<String>, version: Option<String>, license: Option<String> }`. `Key: value` lines; https URLs only.
- `DownloadWatcher { dir: PathBuf, total: Option<u64>, since: SystemTime }`: `sample() -> Option<u64>` returns the size of the largest file modified after `since` under `dir`, recursively.

**Test cases:**
- `download-7zip.en.txt` through `WingetLines` gives the phase sequence `[Downloading, Installing]`.
- A synthetic `Successfully installed` line gives `Verifying`.
- `"  12.5 MB / 50.0 MB"` gives Bytes{13107200, Some(52428800)}.
- Choco `"Progress: Downloading 7zip.install 26.04... 45%"` gives `[Phase(Downloading), Percent(45.0)]`; `"Progress: Downloading x 1.0... 100%"` gives `Percent(100.0)`.
- `parse_show(show-firefox.en.txt)` gives `installer_url` starting with `https://download-installer.cdn.mozilla.net/`, homepage `https://www.mozilla.org/firefox/`, publisher `Mozilla`.
- `DownloadWatcher` in a tempdir:
  - files written before `since` are ignored;
  - a newer 3 MB file sampled gives 3145728;
  - a nested directory is found;
  - an empty dir gives `None`.

### T4: Worker
**Files:** `src-tauri/src/jobs/worker.rs`, plus `src-tauri/src/jobs/msi.rs` (`MsiGate` trait; `SystemMsiGate` polls `OpenMutexW(SYNCHRONIZE, FALSE, "Global\\_MSIExecute")` every 500 ms; `NoGate` for tests).

**Interfaces:**
- `trait JobSink: Send + Sync { fn changed(&self, job: &Job); fn progress(&self, job: &Job); fn queue(&self, jobs: &[Job]); }`
- `struct Queue` (cloneable handle) with:
  - `enqueue(ops: Vec<(OpRequest, String)>) -> Vec<String>`;
  - `snapshot() -> Vec<Job>`;
  - `cancel(id) -> AppResult<()>`;
  - `retry(id) -> AppResult<String>`;
  - `clear_finished()`;
  - `log(id) -> Vec<String>` (the last 500 lines per job).
- `Queue::start(deps: WorkerDeps, sink: Arc<dyn JobSink>) -> Queue`. `WorkerDeps` holds the stream runner, `ExePaths`, the msi gate, an HTTP client and a temp root, each injectable.
- **Per job:**
  1. preparing;
  2. winget only: `show` to get the URL, `HEAD` to get the total;
  3. if MSI is busy, `waiting` (detail "Waiting for another installer");
  4. stream argv, feeding the line parser; while `downloading`, a watcher samples every 250 ms and emits throttled progress;
  5. classify the exit code;
  6. succeeded, failed or skipped.
- An exe missing for the source fails the job with "winget is not installed" / "Chocolatey is not installed".

**Test cases (scripted `FakeStream` emits lines with delays, then an exit code; `RecordingSink`):**
- A single winget install with lines from `download-7zip.en.txt`, exit 0: the recorded phases include `queued → preparing → downloading → installing → succeeded`, and the final job has `endedAt` set.
- **Silent job** (no lines, exit 0): reaches `succeeded`, and percent stays `null` throughout.
- **FIFO:** enqueuing A, B and C runs them in order, with no overlap (`FakeStream` asserts nothing else is running when a run starts).
- **Duplicate:** enqueuing the same `SourceRef`/kind twice while A is queued returns A's id, and the snapshot has one job.
- **Cancel:**
  - while queued, the job becomes `cancelled` without running;
  - while downloading, the FakeStream observes the cancel token, and the job ends `cancelled`;
  - while installing, `cancel` returns `Err(invalid_input)` and the job continues to `succeeded`.
- **Skip:** exit `0x8A15002B` gives `skipped` with detail "No applicable update".
- **Retry:** a failed job retried gives a new id queued, and the old job is unchanged.
- **Choco progress:** lines `Progress: Downloading x 1.0... 10%`, then `...60%`, then `...100%` produce progress events whose percents are non-decreasing and end at 100.
- **MSI gate busy for 2 polls:** the job shows `waiting` before `downloading`.

### T5: Commands and events
**Files:**
- `src-tauri/src/commands/jobs.rs`
- `state.rs` (add `queue: Queue`)
- `lib.rs` (start the queue in setup with `TauriSink`)
- `build.rs` and `capabilities/main.json`
- `app/src/lib/ipc/{commands,events}.ts`

**Commands:**

| command | args | returns |
|---|---|---|
| `enqueue` | `ops: {op: OpRequest, displayName: string}[]` | `string[]` |
| `update_all` | – | `string[]` |
| `get_queue` | – | `Job[]` |
| `cancel_job` | `id` | – |
| `retry_job` | `id` | – |
| `clear_finished` | – | – |
| `get_job_log` | `id` | `string[]` |

- `update_all` enqueues every current `list_updates` item, as an upgrade through its source, in list order.

**Events:** `job://changed` (Job), `job://progress` (Job), `queue://changed` (Job[]).

**`events.ts`:** typed `onJobChanged`, `onJobProgress` and `onQueueChanged` wrappers around `listen`.

**Test cases:** the `update_all` mapping function (`UpgradeCandidate` to `OpRequest`) maps a winget candidate to a winget upgrade and a choco candidate to a choco upgrade, and drops candidates whose id fails validation.

**Live verification (owner consent required, because it changes software on the PC):** install and then uninstall one small app through the queue via CDP. Record the observed phase sequence, the progress samples, and the confirmed `%TEMP%\WinGet` download location.

### T6: Docs and sign-off
- PLAN.md: P3 core ticked; P3 extras listed (persistence/recovery, rules, bundles, post-success version verification).
- ADR 021: download progress from bytes on disk plus `Content-Length`; ConPTY rejected.
- Fresh review, then merge.
