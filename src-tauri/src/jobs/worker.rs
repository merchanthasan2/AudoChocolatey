//! The single install queue. One worker runs jobs strictly in order (Windows Installer allows one
//! install at a time), waits politely for other installers, streams output into honest progress,
//! and reports every change through a `JobSink` (Tauri events in the app, a recorder in tests).

use crate::error::{AppError, AppResult};
use crate::ids::SourceKind;
use crate::jobs::exit_codes::{classify, Outcome};
use crate::jobs::model::{Job, JobPhase};
use crate::jobs::progress::{
    percent, ChocoLines, DownloadWatcher, LineParser, Signal, WingetLines,
};
use crate::sources::model::{OpKind, OpRequest};
use crate::sources::stream::{CancellationToken, CommandSpec, StreamRunner};
use crate::sources::{choco, winget};
use async_trait::async_trait;
use std::collections::{HashMap, VecDeque};
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant, SystemTime};
use tokio::sync::Notify;

/// Last-resort limit for a hung process. Installing is never interrupted by the download timeout.
const SAFETY_TIMEOUT: Duration = Duration::from_secs(24 * 60 * 60);
const LOG_LINES: usize = 500;
const PROGRESS_EVERY: Duration = Duration::from_millis(100);

pub trait JobSink: Send + Sync {
    fn changed(&self, job: &Job);
    fn progress(&self, job: &Job);
    fn queue(&self, jobs: &[Job]);
}

/// Is another Windows Installer session running?
pub trait MsiGate: Send + Sync {
    fn busy(&self) -> bool;
}

#[cfg(test)]
pub struct NoGate;
#[cfg(test)]
impl MsiGate for NoGate {
    fn busy(&self) -> bool {
        false
    }
}

/// Probes `Global\_MSIExecute`: if it exists and is owned, an MSI install is in progress.
pub struct SystemMsiGate;
impl MsiGate for SystemMsiGate {
    fn busy(&self) -> bool {
        use windows_sys::Win32::Foundation::{CloseHandle, WAIT_ABANDONED, WAIT_OBJECT_0};
        use windows_sys::Win32::System::Threading::{
            OpenMutexW, ReleaseMutex, WaitForSingleObject,
        };
        const SYNCHRONIZE: u32 = 0x0010_0000;
        const MUTEX_MODIFY_STATE: u32 = 0x0001;
        let name: Vec<u16> = r"Global\_MSIExecute"
            .encode_utf16()
            .chain(Some(0))
            .collect();
        // SAFETY: valid name; handle closed before return; a mutex we acquire is released at once.
        unsafe {
            let h = OpenMutexW(SYNCHRONIZE | MUTEX_MODIFY_STATE, 0, name.as_ptr());
            if h.is_null() {
                return false;
            }
            let r = WaitForSingleObject(h, 0);
            let busy = !(r == WAIT_OBJECT_0 || r == WAIT_ABANDONED);
            if !busy {
                ReleaseMutex(h);
            }
            CloseHandle(h);
            busy
        }
    }
}

/// Installer size for winget jobs (winget prints no progress when piped).
#[async_trait]
pub trait DownloadSizer: Send + Sync {
    async fn total(&self, op: &OpRequest) -> Option<u64>;
}

/// `winget show` for the installer URL, then an HTTPS HEAD for Content-Length.
pub struct WingetSizer {
    pub runner: Arc<dyn crate::sources::process::Runner>,
    pub winget: PathBuf,
    pub http: reqwest::Client,
}

#[async_trait]
impl DownloadSizer for WingetSizer {
    async fn total(&self, op: &OpRequest) -> Option<u64> {
        if op.target.source() != SourceKind::Winget || op.kind == OpKind::Uninstall {
            return None;
        }
        let mut args = vec![
            "show",
            "--id",
            op.target.id(),
            "--exact",
            "--accept-source-agreements",
            "--disable-interactivity",
        ]
        .into_iter()
        .map(String::from)
        .collect::<Vec<_>>();
        if let Some(v) = &op.version {
            args.extend(["--version".to_string(), v.to_string()]);
        }
        let spec = crate::sources::process::CommandSpec {
            exe: self.winget.clone(),
            args,
            timeout: Duration::from_secs(30),
        };
        let out = self.runner.run(&spec).await.ok()?;
        let url = winget::details::parse_show(&out.stdout).installer_url?;
        let resp = self.http.head(url).send().await.ok()?;
        resp.status()
            .is_success()
            .then(|| resp.content_length())
            .flatten()
            .filter(|n| *n > 0)
    }
}

pub struct WorkerDeps {
    pub stream: Arc<dyn StreamRunner>,
    pub winget: Option<PathBuf>,
    pub choco: Option<PathBuf>,
    pub gate: Arc<dyn MsiGate>,
    pub sizer: Option<Arc<dyn DownloadSizer>>,
    /// winget's download cache (`%TEMP%\WinGet`).
    pub winget_temp: PathBuf,
    pub sample_every: Duration,
    pub gate_poll: Duration,
    /// Preparing/downloading taking longer than this is stopped (installing never is).
    pub download_timeout: Duration,
}

#[derive(Default)]
struct Inner {
    jobs: Vec<Job>,
    logs: HashMap<String, VecDeque<String>>,
    tokens: HashMap<String, CancellationToken>,
    last_progress: HashMap<String, Instant>,
    next: u64,
}

#[derive(Clone)]
pub struct Queue {
    inner: Arc<Mutex<Inner>>,
    wake: Arc<Notify>,
    sink: Arc<dyn JobSink>,
}

const ORDER: [JobPhase; 5] = [
    JobPhase::Preparing,
    JobPhase::Waiting,
    JobPhase::Downloading,
    JobPhase::Installing,
    JobPhase::Verifying,
];

fn rank(p: JobPhase) -> usize {
    ORDER.iter().position(|x| *x == p).unwrap_or(usize::MAX)
}

impl Queue {
    pub fn start(deps: WorkerDeps, sink: Arc<dyn JobSink>) -> Queue {
        let q = Queue {
            inner: Arc::default(),
            wake: Arc::new(Notify::new()),
            sink,
        };
        let worker = q.clone();
        let deps = Arc::new(deps);
        tauri::async_runtime::spawn(async move {
            loop {
                let notified = worker.wake.notified();
                let next = worker
                    .lock()
                    .jobs
                    .iter()
                    .find(|j| j.phase == JobPhase::Queued)
                    .map(|j| j.id.clone());
                match next {
                    Some(id) => {
                        let (w, d, jid) = (worker.clone(), deps.clone(), id.clone());
                        let handle =
                            tauri::async_runtime::spawn(async move { w.run_job(&d, &jid).await });
                        if handle.await.is_err() {
                            worker.lock().tokens.remove(&id);
                            worker.update(&id, |j| {
                                j.error =
                                    Some("Something went wrong while running this job.".into());
                                j.set_phase(JobPhase::Failed);
                            });
                        }
                    }
                    None => notified.await,
                }
            }
        });
        q
    }

    fn lock(&self) -> std::sync::MutexGuard<'_, Inner> {
        // A panicking job must not take the whole queue down with a poisoned lock.
        self.inner.lock().unwrap_or_else(|p| p.into_inner())
    }

    pub fn snapshot(&self) -> Vec<Job> {
        self.lock().jobs.clone()
    }

    pub fn log(&self, id: &str) -> Vec<String> {
        self.lock()
            .logs
            .get(id)
            .map(|l| l.iter().cloned().collect())
            .unwrap_or_default()
    }

    fn emit_queue(&self) {
        let jobs = self.snapshot();
        self.sink.queue(&jobs);
    }

    /// Adds operations; an identical operation already waiting or running returns its id.
    pub fn enqueue(&self, ops: Vec<(OpRequest, String)>) -> Vec<String> {
        let mut ids = Vec::new();
        let mut added = Vec::new();
        {
            let mut inner = self.lock();
            for (op, name) in ops {
                let existing = inner
                    .jobs
                    .iter()
                    .find(|j| {
                        !j.phase.is_finished() && j.op.target == op.target && j.op.kind == op.kind
                    })
                    .map(|j| j.id.clone());
                if let Some(id) = existing {
                    ids.push(id);
                    continue;
                }
                inner.next += 1;
                let job = Job::new(format!("j{}", inner.next), op, name);
                ids.push(job.id.clone());
                added.push(job.clone());
                inner.jobs.push(job);
            }
        }
        for j in &added {
            self.sink.changed(j);
        }
        if !added.is_empty() {
            self.emit_queue();
            self.wake.notify_one();
        }
        ids
    }

    pub fn cancel(&self, id: &str) -> AppResult<()> {
        let mut inner = self.lock();
        let job = inner
            .jobs
            .iter_mut()
            .find(|j| j.id == id)
            .ok_or_else(|| AppError::new("invalid_input", "no such job"))?;
        if job.phase == JobPhase::Queued {
            job.set_phase(JobPhase::Cancelled);
            let snapshot = job.clone();
            drop(inner);
            self.sink.changed(&snapshot);
            self.emit_queue();
            return Ok(());
        }
        if !job.cancellable {
            return Err(AppError::new(
                "invalid_input",
                "This step can't be stopped safely. It will finish shortly.",
            ));
        }
        if let Some(t) = inner.tokens.get(id) {
            t.cancel();
        }
        Ok(())
    }

    pub fn retry(&self, id: &str) -> AppResult<String> {
        let job = self
            .snapshot()
            .into_iter()
            .find(|j| j.id == id)
            .filter(|j| matches!(j.phase, JobPhase::Failed | JobPhase::Cancelled))
            .ok_or_else(|| {
                AppError::new(
                    "invalid_input",
                    "only failed or cancelled jobs can be retried",
                )
            })?;
        Ok(self.enqueue(vec![(job.op, job.display_name)]).remove(0))
    }

    pub fn clear_finished(&self) {
        {
            let mut inner = self.lock();
            let keep: Vec<Job> = inner
                .jobs
                .iter()
                .filter(|j| !j.phase.is_finished())
                .cloned()
                .collect();
            let live: std::collections::HashSet<String> =
                keep.iter().map(|j| j.id.clone()).collect();
            inner.jobs = keep;
            inner.logs.retain(|k, _| live.contains(k));
        }
        self.emit_queue();
    }

    /// Apply a mutation to a job and emit `changed` (or `progress` when only numbers moved).
    fn update(&self, id: &str, f: impl FnOnce(&mut Job)) {
        let snapshot = {
            let mut inner = self.lock();
            let Some(job) = inner.jobs.iter_mut().find(|j| j.id == id) else {
                return;
            };
            let before = job.phase;
            f(job);
            (job.clone(), before != job.phase)
        };
        if snapshot.1 {
            self.sink.changed(&snapshot.0);
            self.emit_queue();
        } else {
            self.sink.changed(&snapshot.0);
        }
    }

    fn progress(&self, id: &str, f: impl FnOnce(&mut Job)) {
        let snapshot = {
            let mut inner = self.lock();
            let due = inner
                .last_progress
                .get(id)
                .is_none_or(|t| t.elapsed() >= PROGRESS_EVERY);
            let Some(job) = inner.jobs.iter_mut().find(|j| j.id == id) else {
                return;
            };
            let before = (job.percent, job.bytes_done);
            f(job);
            let complete = job.percent == Some(100.0) && before.0 != Some(100.0);
            let changed = (job.percent, job.bytes_done) != before;
            if !(due || complete) || !changed {
                return;
            }
            let snap = job.clone();
            inner.last_progress.insert(id.to_string(), Instant::now());
            snap
        };
        self.sink.progress(&snapshot);
    }

    fn advance(&self, id: &str, phase: JobPhase) {
        self.update(id, |j| {
            if !j.phase.is_finished() && rank(phase) > rank(j.phase) {
                j.set_phase(phase);
            }
        });
    }

    async fn run_job(&self, deps: &WorkerDeps, id: &str) {
        let token = CancellationToken::new();
        let started = {
            let mut inner = self.lock();
            let job = inner
                .jobs
                .iter_mut()
                .find(|j| j.id == id && j.phase == JobPhase::Queued);
            let started = job.map(|j| {
                j.set_phase(JobPhase::Preparing);
                (j.op.clone(), j.clone())
            });
            if started.is_some() {
                inner.tokens.insert(id.to_string(), token.clone());
            }
            started
        };
        let Some((op, snapshot)) = started else {
            return;
        };
        self.sink.changed(&snapshot);
        self.emit_queue();
        let outcome = self.execute(deps, id, &op, &token).await;
        self.update(id, |j| match outcome {
            Ok(Outcome::Succeeded { reboot }) => {
                j.reboot_required = reboot;
                j.set_phase(JobPhase::Succeeded);
            }
            Ok(Outcome::Skipped(why)) => {
                j.detail = Some(why);
                j.set_phase(JobPhase::Skipped);
            }
            Ok(Outcome::Failed(why)) => {
                j.error = Some(why);
                j.set_phase(JobPhase::Failed);
            }
            Err(e) if e.code == "cancelled" => j.set_phase(JobPhase::Cancelled),
            Err(e) => {
                j.error = Some(if e.code == "timeout" {
                    "Timed out".into()
                } else {
                    e.message
                });
                j.set_phase(JobPhase::Failed);
            }
        });
        let mut inner = self.lock();
        inner.tokens.remove(id);
        inner.last_progress.remove(id);
    }

    async fn execute(
        &self,
        deps: &WorkerDeps,
        id: &str,
        op: &OpRequest,
        token: &CancellationToken,
    ) -> AppResult<Outcome> {
        let source = op.target.source();
        let (exe, argv) = match source {
            SourceKind::Winget => (deps.winget.clone(), winget::args::build_op(op)?),
            SourceKind::Choco => (deps.choco.clone(), choco::args::build_op(op)?),
        };
        let Some(exe) = exe else {
            let tool = if source == SourceKind::Winget {
                "winget"
            } else {
                "Chocolatey"
            };
            return Ok(Outcome::Failed(format!("{tool} is not installed")));
        };
        let total = match &deps.sizer {
            Some(s) => tokio::select! {
                t = s.total(op) => t,
                _ = token.cancelled() => return Err(AppError::new("cancelled", "Stopped")),
            },
            None => None,
        };
        while deps.gate.busy() {
            if token.is_cancelled() {
                return Err(AppError::new("cancelled", "Stopped"));
            }
            self.update(id, |j| {
                if j.phase != JobPhase::Waiting {
                    j.set_phase(JobPhase::Waiting);
                    j.detail = Some("Waiting for another installer".into());
                }
            });
            tokio::time::sleep(deps.gate_poll).await;
        }
        self.update(id, |j| j.detail = None);
        if token.is_cancelled() {
            return Err(AppError::new("cancelled", "Stopped"));
        }

        let parser: Mutex<Box<dyn LineParser>> = Mutex::new(match source {
            SourceKind::Winget => Box::new(WingetLines::default()),
            SourceKind::Choco => Box::new(ChocoLines::default()),
        });
        let on_line = |line: String| {
            {
                let mut inner = self.lock();
                let log = inner.logs.entry(id.to_string()).or_default();
                if log.len() == LOG_LINES {
                    log.pop_front();
                }
                log.push_back(line.clone());
            }
            let signals = parser.lock().expect("parser").feed(&line);
            for s in signals {
                match s {
                    Signal::Phase(p) => self.advance(id, p),
                    Signal::Percent(pc) => self.progress(id, |j| j.percent = Some(pc)),
                    Signal::Bytes { done, total: t } => self.progress(id, |j| {
                        j.bytes_done = Some(done);
                        j.bytes_total = t.or(total);
                        j.percent = percent(done, j.bytes_total);
                    }),
                }
            }
        };
        let spec = CommandSpec {
            exe,
            args: argv,
            timeout: SAFETY_TIMEOUT,
        };
        let watcher = DownloadWatcher {
            dir: deps.winget_temp.clone(),
            since: SystemTime::now(),
        };
        let run = deps.stream.run(&spec, &on_line, token.clone());
        tokio::pin!(run);
        let mut tick = tokio::time::interval(deps.sample_every);
        let began = Instant::now();
        let mut timed_out = false;
        let result = loop {
            tokio::select! {
                r = &mut run => break r,
                _ = tick.tick() => {
                    let phase = self.lock().jobs.iter().find(|j| j.id == id).map(|j| j.phase);
                    if !timed_out && phase.is_some_and(Job::cancellable_in) && began.elapsed() > deps.download_timeout {
                        timed_out = true;
                        token.cancel();
                    }
                    let downloading = phase == Some(JobPhase::Downloading);
                    if downloading && source == SourceKind::Winget {
                        if let Some(done) = watcher.sample() {
                            self.progress(id, |j| {
                                j.bytes_done = Some(done);
                                j.bytes_total = total;
                                j.percent = percent(done, total);
                            });
                        }
                    }
                }
            }
        };
        match result {
            Err(e) if e.code == "cancelled" && timed_out => {
                Ok(Outcome::Failed("The download timed out".into()))
            }
            Err(e) => Err(e),
            Ok(code) => Ok(classify(source, op.kind, code)),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::{SourceKind, SourceRef};
    use crate::sources::model::{OpKind, OpRequest};
    use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering as AO};
    use std::sync::Mutex as StdMutex;
    use std::time::Duration;

    const DOWNLOAD: &str = include_str!("../../tests/fixtures/winget/download-7zip.en.txt");

    type Script = (Vec<(u64, String)>, Option<i32>);
    type ScriptIn<'a> = (Vec<(u64, &'a str)>, Option<i32>);

    /// Each run consumes the next script: lines with a delay before each, then an exit code.
    struct FakeStream {
        scripts: StdMutex<std::collections::VecDeque<Script>>,
        running: AtomicBool,
        overlapped: AtomicBool,
        runs: AtomicUsize,
    }

    impl FakeStream {
        fn new(scripts: Vec<ScriptIn<'_>>) -> Arc<Self> {
            Arc::new(Self {
                scripts: StdMutex::new(
                    scripts
                        .into_iter()
                        .map(|(l, c)| (l.into_iter().map(|(d, s)| (d, s.to_string())).collect(), c))
                        .collect(),
                ),
                running: AtomicBool::new(false),
                overlapped: AtomicBool::new(false),
                runs: AtomicUsize::new(0),
            })
        }
    }

    #[async_trait::async_trait]
    impl StreamRunner for FakeStream {
        async fn run(
            &self,
            _spec: &CommandSpec,
            on_line: &(dyn Fn(String) + Send + Sync),
            cancel: CancellationToken,
        ) -> AppResult<Option<i32>> {
            if self.running.swap(true, AO::SeqCst) {
                self.overlapped.store(true, AO::SeqCst);
            }
            self.runs.fetch_add(1, AO::SeqCst);
            let (lines, code) = self
                .scripts
                .lock()
                .unwrap()
                .pop_front()
                .unwrap_or((vec![], Some(0)));
            for (delay, line) in lines {
                tokio::select! {
                    _ = tokio::time::sleep(Duration::from_millis(delay)) => on_line(line),
                    _ = cancel.cancelled() => {
                        self.running.store(false, AO::SeqCst);
                        return Err(AppError::new("cancelled", "Stopped"));
                    }
                }
            }
            self.running.store(false, AO::SeqCst);
            Ok(code)
        }
    }

    #[derive(Default)]
    struct Rec {
        phases: StdMutex<Vec<(String, JobPhase)>>,
        percents: StdMutex<Vec<(String, f32)>>,
        bytes: StdMutex<Vec<u64>>,
    }

    impl JobSink for Rec {
        fn changed(&self, job: &Job) {
            let mut p = self.phases.lock().unwrap();
            if p.iter()
                .rev()
                .find(|(id, _)| id == &job.id)
                .map(|(_, ph)| *ph)
                != Some(job.phase)
            {
                p.push((job.id.clone(), job.phase));
            }
        }
        fn progress(&self, job: &Job) {
            if let Some(pc) = job.percent {
                self.percents.lock().unwrap().push((job.id.clone(), pc));
            }
            if let Some(b) = job.bytes_done {
                self.bytes.lock().unwrap().push(b);
            }
        }
        fn queue(&self, _jobs: &[Job]) {}
    }

    impl Rec {
        fn phases_of(&self, id: &str) -> Vec<JobPhase> {
            self.phases
                .lock()
                .unwrap()
                .iter()
                .filter(|(i, _)| i == id)
                .map(|(_, p)| *p)
                .collect()
        }
    }

    struct BusyGate(AtomicUsize);
    impl MsiGate for BusyGate {
        fn busy(&self) -> bool {
            self.0.fetch_sub(1, AO::SeqCst) > 0
        }
    }

    fn op(source: SourceKind, id: &str, kind: OpKind) -> (OpRequest, String) {
        (
            OpRequest {
                kind,
                target: SourceRef::new(source, id).unwrap(),
                version: None,
            },
            id.to_string(),
        )
    }

    fn start(stream: Arc<FakeStream>, gate: Arc<dyn MsiGate>) -> (Queue, Arc<Rec>) {
        let rec = Arc::new(Rec::default());
        let deps = WorkerDeps {
            stream,
            winget: Some(r"C:\fake\winget.exe".into()),
            choco: Some(r"C:\fake\choco.exe".into()),
            gate,
            sizer: None,
            winget_temp: std::env::temp_dir().join("qu-test-none"),
            sample_every: Duration::from_millis(20),
            gate_poll: Duration::from_millis(20),
            download_timeout: Duration::from_secs(60),
        };
        (Queue::start(deps, rec.clone()), rec)
    }

    async fn until_finished(q: &Queue, id: &str) -> Job {
        for _ in 0..500 {
            if let Some(j) = q.snapshot().into_iter().find(|j| j.id == id) {
                if j.phase.is_finished() {
                    return j;
                }
            }
            tokio::time::sleep(Duration::from_millis(10)).await;
        }
        panic!("job {id} never finished: {:?}", q.snapshot());
    }

    async fn until_phase(q: &Queue, id: &str, phase: JobPhase) {
        for _ in 0..500 {
            if q.snapshot().iter().any(|j| j.id == id && j.phase == phase) {
                return;
            }
            tokio::time::sleep(Duration::from_millis(5)).await;
        }
        panic!("job {id} never reached {phase:?}");
    }

    fn lines(text: &str, delay: u64) -> Vec<(u64, &str)> {
        text.lines().map(|l| (delay, l)).collect()
    }

    #[tokio::test]
    async fn winget_install_goes_through_every_phase() {
        let (q, rec) = start(
            FakeStream::new(vec![(lines(DOWNLOAD, 5), Some(0))]),
            Arc::new(NoGate),
        );
        let id = q
            .enqueue(vec![op(SourceKind::Winget, "7zip.7zip", OpKind::Install)])
            .remove(0);
        let done = until_finished(&q, &id).await;
        assert_eq!(done.phase, JobPhase::Succeeded);
        assert!(done.ended_at.is_some());
        assert_eq!(
            rec.phases_of(&id),
            [
                JobPhase::Queued,
                JobPhase::Preparing,
                JobPhase::Downloading,
                JobPhase::Installing,
                JobPhase::Succeeded
            ]
        );
    }

    #[tokio::test]
    async fn silent_installer_still_finishes_without_fake_percent() {
        let (q, rec) = start(FakeStream::new(vec![(vec![], Some(0))]), Arc::new(NoGate));
        let id = q
            .enqueue(vec![op(SourceKind::Winget, "Silent.App", OpKind::Install)])
            .remove(0);
        assert_eq!(until_finished(&q, &id).await.phase, JobPhase::Succeeded);
        assert!(rec.percents.lock().unwrap().is_empty());
    }

    #[tokio::test]
    async fn jobs_run_fifo_without_overlap() {
        let s = FakeStream::new(vec![
            (vec![(30, "a")], Some(0)),
            (vec![(30, "b")], Some(0)),
            (vec![(30, "c")], Some(0)),
        ]);
        let (q, rec) = start(s.clone(), Arc::new(NoGate));
        let ids = q.enqueue(vec![
            op(SourceKind::Winget, "A.A", OpKind::Install),
            op(SourceKind::Choco, "b", OpKind::Upgrade),
            op(SourceKind::Winget, "C.C", OpKind::Uninstall),
        ]);
        until_finished(&q, &ids[2]).await;
        assert!(!s.overlapped.load(AO::SeqCst));
        let order: Vec<String> = rec
            .phases
            .lock()
            .unwrap()
            .iter()
            .filter(|(_, p)| *p == JobPhase::Preparing)
            .map(|(i, _)| i.clone())
            .collect();
        assert_eq!(order, ids);
    }

    #[tokio::test]
    async fn duplicate_enqueue_returns_existing_job() {
        let (q, _) = start(
            FakeStream::new(vec![(vec![(200, "x")], Some(0))]),
            Arc::new(NoGate),
        );
        let a = q.enqueue(vec![op(SourceKind::Winget, "Dup.App", OpKind::Install)]);
        let b = q.enqueue(vec![op(SourceKind::Winget, "Dup.App", OpKind::Install)]);
        assert_eq!(a, b);
        assert_eq!(q.snapshot().len(), 1);
    }

    #[tokio::test]
    async fn cancel_queued_job_never_runs() {
        let s = FakeStream::new(vec![(vec![(200, "busy")], Some(0))]);
        let (q, _) = start(s.clone(), Arc::new(NoGate));
        let ids = q.enqueue(vec![
            op(SourceKind::Winget, "First.App", OpKind::Install),
            op(SourceKind::Winget, "Second.App", OpKind::Install),
        ]);
        q.cancel(&ids[1]).unwrap();
        until_finished(&q, &ids[0]).await;
        assert_eq!(until_finished(&q, &ids[1]).await.phase, JobPhase::Cancelled);
        assert_eq!(s.runs.load(AO::SeqCst), 1);
    }

    #[tokio::test]
    async fn cancel_while_downloading_stops_the_process() {
        let script = vec![
            (5, "Downloading https://example.com/x.msi"),
            (5000, "never"),
        ];
        let (q, _) = start(FakeStream::new(vec![(script, Some(0))]), Arc::new(NoGate));
        let id = q
            .enqueue(vec![op(SourceKind::Winget, "Big.App", OpKind::Install)])
            .remove(0);
        until_phase(&q, &id, JobPhase::Downloading).await;
        q.cancel(&id).unwrap();
        assert_eq!(until_finished(&q, &id).await.phase, JobPhase::Cancelled);
    }

    #[tokio::test]
    async fn cannot_cancel_while_installing() {
        let script = vec![
            (5, "Successfully verified installer hash"),
            (300, "Successfully installed"),
        ];
        let (q, _) = start(FakeStream::new(vec![(script, Some(0))]), Arc::new(NoGate));
        let id = q
            .enqueue(vec![op(SourceKind::Winget, "Inst.App", OpKind::Install)])
            .remove(0);
        until_phase(&q, &id, JobPhase::Installing).await;
        assert_eq!(q.cancel(&id).unwrap_err().code, "invalid_input");
        assert_eq!(until_finished(&q, &id).await.phase, JobPhase::Succeeded);
    }

    #[tokio::test]
    async fn no_applicable_update_is_skipped() {
        let (q, _) = start(
            FakeStream::new(vec![(vec![], Some(-1978335189))]),
            Arc::new(NoGate),
        );
        let id = q
            .enqueue(vec![op(SourceKind::Winget, "Same.App", OpKind::Upgrade)])
            .remove(0);
        let j = until_finished(&q, &id).await;
        assert_eq!(j.phase, JobPhase::Skipped);
        assert_eq!(j.detail.as_deref(), Some("No applicable update"));
    }

    #[tokio::test]
    async fn retry_failed_job_creates_new_job() {
        let (q, _) = start(
            FakeStream::new(vec![(vec![], Some(1)), (vec![], Some(0))]),
            Arc::new(NoGate),
        );
        let id = q
            .enqueue(vec![op(SourceKind::Choco, "flaky", OpKind::Install)])
            .remove(0);
        let failed = until_finished(&q, &id).await;
        assert_eq!(failed.phase, JobPhase::Failed);
        let again = q.retry(&id).unwrap();
        assert_ne!(again, id);
        assert_eq!(until_finished(&q, &again).await.phase, JobPhase::Succeeded);
        assert_eq!(
            q.snapshot().iter().find(|j| j.id == id).unwrap().phase,
            JobPhase::Failed
        );
    }

    #[tokio::test]
    async fn choco_percent_is_monotonic_to_100() {
        let script = vec![
            (5, "Progress: Downloading x 1.0... 10%"),
            (150, "Progress: Downloading x 1.0... 60%"),
            (150, "Progress: Downloading x 1.0... 100%"),
            (5, "Installing x..."),
        ];
        let (q, rec) = start(FakeStream::new(vec![(script, Some(0))]), Arc::new(NoGate));
        let id = q
            .enqueue(vec![op(SourceKind::Choco, "x", OpKind::Install)])
            .remove(0);
        until_finished(&q, &id).await;
        let pcs: Vec<f32> = rec
            .percents
            .lock()
            .unwrap()
            .iter()
            .map(|(_, p)| *p)
            .collect();
        assert!(pcs.windows(2).all(|w| w[0] <= w[1]), "{pcs:?}");
        assert_eq!(pcs.last(), Some(&100.0));
    }

    #[tokio::test]
    async fn waits_while_another_installer_runs() {
        let (q, rec) = start(
            FakeStream::new(vec![(vec![], Some(0))]),
            Arc::new(BusyGate(AtomicUsize::new(2))),
        );
        let id = q
            .enqueue(vec![op(SourceKind::Winget, "Wait.App", OpKind::Install)])
            .remove(0);
        until_finished(&q, &id).await;
        assert!(rec.phases_of(&id).contains(&JobPhase::Waiting));
    }

    fn deps_with(
        stream: Arc<dyn StreamRunner>,
        timeout: Duration,
        temp: PathBuf,
        sizer: Option<Arc<dyn DownloadSizer>>,
    ) -> WorkerDeps {
        WorkerDeps {
            stream,
            winget: Some(r"C:\fake\winget.exe".into()),
            choco: Some(r"C:\fake\choco.exe".into()),
            gate: Arc::new(NoGate),
            sizer,
            winget_temp: temp,
            sample_every: Duration::from_millis(20),
            gate_poll: Duration::from_millis(20),
            download_timeout: timeout,
        }
    }

    #[tokio::test]
    async fn stuck_download_times_out_but_installing_never_does() {
        let stuck = vec![
            (5, "Downloading https://example.com/x.msi"),
            (5000, "never"),
        ];
        let installing = vec![
            (5, "Successfully verified installer hash"),
            (400, "Successfully installed"),
        ];
        let rec = Arc::new(Rec::default());
        let q = Queue::start(
            deps_with(
                FakeStream::new(vec![(stuck, Some(0)), (installing, Some(0))]),
                Duration::from_millis(200),
                std::env::temp_dir().join("qu-none"),
                None,
            ),
            rec,
        );
        let ids = q.enqueue(vec![
            op(SourceKind::Winget, "Stuck.App", OpKind::Install),
            op(SourceKind::Winget, "Slow.Installer", OpKind::Install),
        ]);
        let a = until_finished(&q, &ids[0]).await;
        assert_eq!(a.phase, JobPhase::Failed);
        assert_eq!(a.error.as_deref(), Some("The download timed out"));
        assert_eq!(until_finished(&q, &ids[1]).await.phase, JobPhase::Succeeded);
    }

    struct SlowSizer;
    #[async_trait::async_trait]
    impl DownloadSizer for SlowSizer {
        async fn total(&self, _op: &OpRequest) -> Option<u64> {
            tokio::time::sleep(Duration::from_secs(10)).await;
            None
        }
    }

    #[tokio::test]
    async fn cancel_while_preparing_is_immediate() {
        let rec = Arc::new(Rec::default());
        let q = Queue::start(
            deps_with(
                FakeStream::new(vec![]),
                Duration::from_secs(60),
                std::env::temp_dir(),
                Some(Arc::new(SlowSizer)),
            ),
            rec,
        );
        let id = q
            .enqueue(vec![op(SourceKind::Winget, "Prep.App", OpKind::Install)])
            .remove(0);
        until_phase(&q, &id, JobPhase::Preparing).await;
        let t = std::time::Instant::now();
        q.cancel(&id).unwrap();
        assert_eq!(until_finished(&q, &id).await.phase, JobPhase::Cancelled);
        assert!(t.elapsed() < Duration::from_secs(1), "{:?}", t.elapsed());
    }

    #[tokio::test]
    async fn bytes_are_reported_even_without_a_known_total() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("setup.exe");
        let script = vec![
            (5, "Downloading https://example.com/x.exe"),
            (400, "Successfully verified installer hash"),
        ];
        let rec = Arc::new(Rec::default());
        let q = Queue::start(
            deps_with(
                FakeStream::new(vec![(script, Some(0))]),
                Duration::from_secs(60),
                dir.path().to_path_buf(),
                None,
            ),
            rec.clone(),
        );
        let id = q
            .enqueue(vec![op(SourceKind::Winget, "Bytes.App", OpKind::Install)])
            .remove(0);
        until_phase(&q, &id, JobPhase::Downloading).await;
        std::fs::write(&path, vec![1u8; 4096]).unwrap();
        until_finished(&q, &id).await;
        assert!(
            rec.bytes.lock().unwrap().contains(&4096),
            "{:?}",
            rec.bytes.lock().unwrap()
        );
    }

    struct PanicStream;
    #[async_trait::async_trait]
    impl StreamRunner for PanicStream {
        async fn run(
            &self,
            spec: &CommandSpec,
            _l: &(dyn Fn(String) + Send + Sync),
            _c: CancellationToken,
        ) -> AppResult<Option<i32>> {
            if spec.args.iter().any(|a| a == "Boom.App") {
                panic!("simulated crash");
            }
            Ok(Some(0))
        }
    }

    #[tokio::test]
    async fn a_crashing_job_fails_without_stopping_the_queue() {
        let rec = Arc::new(Rec::default());
        let q = Queue::start(
            deps_with(
                Arc::new(PanicStream),
                Duration::from_secs(60),
                std::env::temp_dir(),
                None,
            ),
            rec,
        );
        let ids = q.enqueue(vec![
            op(SourceKind::Winget, "Boom.App", OpKind::Install),
            op(SourceKind::Winget, "Next.App", OpKind::Install),
        ]);
        assert_eq!(until_finished(&q, &ids[0]).await.phase, JobPhase::Failed);
        assert_eq!(until_finished(&q, &ids[1]).await.phase, JobPhase::Succeeded);
    }

    #[tokio::test]
    async fn missing_tool_fails_with_a_clear_message() {
        let rec = Arc::new(Rec::default());
        let deps = WorkerDeps {
            stream: FakeStream::new(vec![]),
            winget: None,
            choco: None,
            gate: Arc::new(NoGate),
            sizer: None,
            winget_temp: std::env::temp_dir(),
            sample_every: Duration::from_millis(20),
            gate_poll: Duration::from_millis(20),
            download_timeout: Duration::from_secs(60),
        };
        let q = Queue::start(deps, rec);
        let id = q
            .enqueue(vec![op(SourceKind::Choco, "x", OpKind::Install)])
            .remove(0);
        let j = until_finished(&q, &id).await;
        assert_eq!(j.phase, JobPhase::Failed);
        assert_eq!(j.error.as_deref(), Some("Chocolatey is not installed"));
    }
}
