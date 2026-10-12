//! Streaming process runner for install jobs. Every child is placed in a Win32 Job Object with
//! KILL_ON_JOB_CLOSE, so cancel or timeout terminates the whole tree (winget, the installer it
//! launched, msiexec helpers) instead of orphaning it. Output is split on CR and LF because
//! package managers redraw progress with carriage returns.

use crate::error::{AppError, AppResult};
use crate::sources::process::strip_ansi;
pub use crate::sources::process::CommandSpec;
use async_trait::async_trait;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use tokio::io::AsyncReadExt;
use tokio::sync::{mpsc, Notify};

const CREATE_NO_WINDOW: u32 = 0x0800_0000;

#[derive(Default)]
pub struct LineSplitter {
    pending: String,
}

impl LineSplitter {
    pub fn push(&mut self, chunk: &str) -> Vec<String> {
        self.pending.push_str(chunk);
        let mut out = Vec::new();
        while let Some(i) = self.pending.find(['\r', '\n']) {
            let line: String = self.pending.drain(..=i).collect();
            let line = strip_ansi(line.trim_end_matches(['\r', '\n']))
                .trim_end()
                .to_string();
            if !line.trim().is_empty() {
                out.push(line);
            }
        }
        out
    }

    pub fn finish(self) -> Option<String> {
        let line = strip_ansi(&self.pending).trim_end().to_string();
        (!line.trim().is_empty()).then_some(line)
    }
}

/// Cooperative cancellation shared between the queue and a running job.
#[derive(Clone, Default)]
pub struct CancellationToken {
    flag: Arc<AtomicBool>,
    notify: Arc<Notify>,
}

impl CancellationToken {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn cancel(&self) {
        self.flag.store(true, Ordering::SeqCst);
        self.notify.notify_waiters();
    }

    pub fn is_cancelled(&self) -> bool {
        self.flag.load(Ordering::SeqCst)
    }

    pub async fn cancelled(&self) {
        while !self.is_cancelled() {
            let notified = self.notify.notified();
            if self.is_cancelled() {
                break;
            }
            notified.await;
        }
    }
}

#[async_trait]
pub trait StreamRunner: Send + Sync {
    async fn run(
        &self,
        spec: &CommandSpec,
        on_line: &(dyn Fn(String) + Send + Sync),
        cancel: CancellationToken,
    ) -> AppResult<Option<i32>>;
}

/// Owns a Job Object handle; closing it kills any process still inside.
struct KillOnClose(windows_sys::Win32::Foundation::HANDLE);

// SAFETY: a job handle is a kernel handle usable from any thread.
unsafe impl Send for KillOnClose {}
unsafe impl Sync for KillOnClose {}

impl KillOnClose {
    fn for_process(process: std::os::windows::io::RawHandle) -> Option<Self> {
        use windows_sys::Win32::System::JobObjects::*;
        // SAFETY: plain Win32 calls with valid arguments; the handle is owned by the wrapper.
        unsafe {
            let job = CreateJobObjectW(std::ptr::null(), std::ptr::null());
            if job.is_null() {
                return None;
            }
            let guard = KillOnClose(job);
            let mut info: JOBOBJECT_EXTENDED_LIMIT_INFORMATION = std::mem::zeroed();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            let ok = SetInformationJobObject(
                job,
                JobObjectExtendedLimitInformation,
                (&info as *const JOBOBJECT_EXTENDED_LIMIT_INFORMATION).cast(),
                std::mem::size_of::<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>() as u32,
            );
            if ok == 0 || AssignProcessToJobObject(job, process as _) == 0 {
                return None;
            }
            Some(guard)
        }
    }

    fn terminate(&self) {
        // SAFETY: valid job handle.
        unsafe {
            windows_sys::Win32::System::JobObjects::TerminateJobObject(self.0, 1);
        }
    }
}

impl Drop for KillOnClose {
    fn drop(&mut self) {
        // SAFETY: we own the handle.
        unsafe {
            windows_sys::Win32::Foundation::CloseHandle(self.0);
        }
    }
}

#[derive(Debug, Default, Clone, Copy)]
pub struct SystemStreamRunner;

async fn pump<R: tokio::io::AsyncRead + Unpin>(mut r: R, tx: mpsc::UnboundedSender<String>) {
    let mut split = LineSplitter::default();
    let mut carry: Vec<u8> = Vec::new();
    let mut buf = [0u8; 8192];
    while let Ok(n) = r.read(&mut buf).await {
        if n == 0 {
            break;
        }
        carry.extend_from_slice(&buf[..n]);
        // Decode only up to the last line break so multi-byte characters are never split.
        if let Some(cut) = carry.iter().rposition(|b| *b == b'\n' || *b == b'\r') {
            let text = String::from_utf8_lossy(&carry[..=cut]).into_owned();
            carry.drain(..=cut);
            for line in split.push(&text) {
                let _ = tx.send(line);
            }
        }
    }
    split.push(&String::from_utf8_lossy(&carry));
    if let Some(last) = split.finish() {
        let _ = tx.send(last);
    }
}

#[async_trait]
impl StreamRunner for SystemStreamRunner {
    async fn run(
        &self,
        spec: &CommandSpec,
        on_line: &(dyn Fn(String) + Send + Sync),
        cancel: CancellationToken,
    ) -> AppResult<Option<i32>> {
        let mut child = tokio::process::Command::new(&spec.exe)
            .args(&spec.args)
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::piped())
            .stderr(std::process::Stdio::piped())
            .kill_on_drop(true)
            .creation_flags(CREATE_NO_WINDOW)
            .spawn()?;
        let job = child.raw_handle().and_then(KillOnClose::for_process);
        let (tx, mut rx) = mpsc::unbounded_channel();
        if let Some(out) = child.stdout.take() {
            tokio::spawn(pump(out, tx.clone()));
        }
        if let Some(err) = child.stderr.take() {
            tokio::spawn(pump(err, tx.clone()));
        }
        drop(tx);

        let kill = |child: &mut tokio::process::Child| match &job {
            Some(j) => j.terminate(),
            None => {
                let _ = child.start_kill();
            }
        };
        let deadline = tokio::time::sleep(spec.timeout);
        tokio::pin!(deadline);
        let status = loop {
            tokio::select! {
                Some(line) = rx.recv() => on_line(line),
                status = child.wait() => break status?,
                _ = cancel.cancelled() => {
                    kill(&mut child);
                    let _ = child.wait().await;
                    return Err(AppError::new("cancelled", "Stopped"));
                }
                _ = &mut deadline => {
                    kill(&mut child);
                    let _ = child.wait().await;
                    return Err(AppError::new("timeout", format!("did not finish within {:?}", spec.timeout)));
                }
            }
        };
        // Drain remaining output; anything still holding the pipes after 2 s is killed.
        let drain = async {
            while let Some(line) = rx.recv().await {
                on_line(line);
            }
        };
        if tokio::time::timeout(std::time::Duration::from_secs(2), drain)
            .await
            .is_err()
        {
            if let Some(j) = &job {
                j.terminate();
            }
        }
        Ok(status.code())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;
    use std::sync::{Arc, Mutex};
    use std::time::{Duration, Instant};

    #[test]
    fn splitter_handles_cr_lf_and_partials() {
        let mut s = LineSplitter::default();
        assert_eq!(s.push("a\r\nb"), vec!["a"]);
        assert_eq!(s.push("c\rd\n"), vec!["bc", "d"]);
        assert_eq!(s.push("\x1b[32mtail"), Vec::<String>::new());
        assert_eq!(s.finish().as_deref(), Some("tail"));
    }

    fn cmd(args: &[&str], timeout: Duration) -> CommandSpec {
        CommandSpec {
            exe: PathBuf::from(r"C:\Windows\System32\cmd.exe"),
            args: args.iter().map(|s| s.to_string()).collect(),
            timeout,
        }
    }

    fn ping_pids() -> Vec<u32> {
        let out = std::process::Command::new(r"C:\Windows\System32\tasklist.exe")
            .args(["/FI", "IMAGENAME eq PING.EXE", "/FO", "CSV", "/NH"])
            .output()
            .unwrap();
        String::from_utf8_lossy(&out.stdout)
            .lines()
            .filter_map(|l| l.split(',').nth(1)?.trim_matches('"').parse().ok())
            .collect()
    }

    #[tokio::test]
    async fn streams_lines_and_returns_exit_code() {
        let lines = Arc::new(Mutex::new(Vec::new()));
        let sink = lines.clone();
        let code = SystemStreamRunner
            .run(
                &cmd(&["/c", "echo one& echo two"], Duration::from_secs(10)),
                &move |l| sink.lock().unwrap().push(l),
                CancellationToken::new(),
            )
            .await
            .unwrap();
        assert_eq!(code, Some(0));
        assert_eq!(*lines.lock().unwrap(), vec!["one", "two"]);
    }

    #[tokio::test]
    async fn cancel_kills_the_whole_process_tree() {
        let before = ping_pids();
        let token = CancellationToken::new();
        let t2 = token.clone();
        tokio::spawn(async move {
            tokio::time::sleep(Duration::from_millis(400)).await;
            t2.cancel();
        });
        let started = Instant::now();
        let err = SystemStreamRunner
            .run(
                &cmd(&["/c", "ping -n 30 127.0.0.1"], Duration::from_secs(60)),
                &|_| {},
                token,
            )
            .await
            .unwrap_err();
        assert_eq!(err.code, "cancelled");
        assert!(
            started.elapsed() < Duration::from_secs(3),
            "{:?}",
            started.elapsed()
        );
        tokio::time::sleep(Duration::from_millis(300)).await;
        let orphans: Vec<_> = ping_pids()
            .into_iter()
            .filter(|p| !before.contains(p))
            .collect();
        assert!(orphans.is_empty(), "orphaned ping processes: {orphans:?}");
    }

    #[tokio::test]
    async fn timeout_kills_the_tree_too() {
        let before = ping_pids();
        let err = SystemStreamRunner
            .run(
                &cmd(&["/c", "ping -n 30 127.0.0.1"], Duration::from_millis(400)),
                &|_| {},
                CancellationToken::new(),
            )
            .await
            .unwrap_err();
        assert_eq!(err.code, "timeout");
        tokio::time::sleep(Duration::from_millis(300)).await;
        assert!(ping_pids().into_iter().all(|p| before.contains(&p)));
    }
}
