//! The only module that spawns processes. Executables are absolute paths resolved from
//! well-known locations (never PATH), arguments are an argv vector (never a shell string),
//! and every run has a timeout.

use crate::error::{AppError, AppResult};
use async_trait::async_trait;
use regex::Regex;
use std::path::{Path, PathBuf};
use std::sync::LazyLock;
use std::time::Duration;

const CREATE_NO_WINDOW: u32 = 0x0800_0000;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CommandSpec {
    pub exe: PathBuf,
    pub args: Vec<String>,
    pub timeout: Duration,
}

#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct ProcessOutput {
    pub code: Option<i32>,
    pub stdout: String,
    pub stderr: String,
}

#[async_trait]
pub trait Runner: Send + Sync {
    async fn run(&self, spec: &CommandSpec) -> AppResult<ProcessOutput>;
}

/// Runs real processes: hidden console window, no stdin, killed if dropped or timed out.
#[derive(Debug, Default, Clone, Copy)]
pub struct SystemRunner;

#[async_trait]
impl Runner for SystemRunner {
    async fn run(&self, spec: &CommandSpec) -> AppResult<ProcessOutput> {
        let mut cmd = tokio::process::Command::new(&spec.exe);
        cmd.args(&spec.args)
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::piped())
            .stderr(std::process::Stdio::piped())
            .kill_on_drop(true)
            .creation_flags(CREATE_NO_WINDOW);
        let child = cmd.spawn()?;
        let out = tokio::time::timeout(spec.timeout, child.wait_with_output())
            .await
            .map_err(|_| {
                AppError::new(
                    "timeout",
                    format!(
                        "{} did not finish within {:?}",
                        exe_name(&spec.exe),
                        spec.timeout
                    ),
                )
            })??;
        Ok(ProcessOutput {
            code: out.status.code(),
            stdout: strip_ansi(&String::from_utf8_lossy(&out.stdout)),
            stderr: strip_ansi(&String::from_utf8_lossy(&out.stderr)),
        })
    }
}

fn exe_name(p: &Path) -> String {
    p.file_name()
        .map(|n| n.to_string_lossy().into_owned())
        .unwrap_or_default()
}

static ANSI: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"\x1b\[[0-9;?]*[ -/]*[@-~]").unwrap());

pub fn strip_ansi(s: &str) -> String {
    ANSI.replace_all(s, "").into_owned()
}

/// winget lives behind the per-user App Execution Alias.
pub fn resolve_winget(
    env: &dyn Fn(&str) -> Option<String>,
    exists: &dyn Fn(&Path) -> bool,
) -> Option<PathBuf> {
    let p = PathBuf::from(env("LOCALAPPDATA")?)
        .join("Microsoft")
        .join("WindowsApps")
        .join("winget.exe");
    exists(&p).then_some(p)
}

/// choco honours %ChocolateyInstall%, falling back to the default %ProgramData% location.
pub fn resolve_choco(
    env: &dyn Fn(&str) -> Option<String>,
    exists: &dyn Fn(&Path) -> bool,
) -> Option<PathBuf> {
    let candidates = [
        env("ChocolateyInstall").map(|d| PathBuf::from(d).join("bin").join("choco.exe")),
        env("ProgramData").map(|d| {
            PathBuf::from(d)
                .join("chocolatey")
                .join("bin")
                .join("choco.exe")
        }),
    ];
    candidates.into_iter().flatten().find(|p| exists(p))
}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct ExePaths {
    pub winget: Option<PathBuf>,
    pub choco: Option<PathBuf>,
}

impl ExePaths {
    pub fn detect() -> Self {
        let env = |k: &str| std::env::var(k).ok().filter(|v| !v.is_empty());
        let exists = |p: &Path| p.is_file();
        Self {
            winget: resolve_winget(&env, &exists),
            choco: resolve_choco(&env, &exists),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashMap;

    fn env_of(pairs: &[(&str, &str)]) -> impl Fn(&str) -> Option<String> {
        let map: HashMap<String, String> = pairs
            .iter()
            .map(|(k, v)| (k.to_string(), v.to_string()))
            .collect();
        move |k: &str| {
            assert_ne!(k, "PATH", "PATH must never be consulted");
            map.get(k).cloned()
        }
    }

    #[test]
    fn strips_ansi_sequences() {
        assert_eq!(strip_ansi("\x1b[32mok\x1b[0m"), "ok");
        assert_eq!(strip_ansi("\x1b[?25lplain\x1b[?25h"), "plain");
    }

    #[test]
    fn resolves_winget_from_localappdata() {
        let env = env_of(&[("LOCALAPPDATA", r"C:\U\AppData\Local")]);
        assert_eq!(
            resolve_winget(&env, &|_| true),
            Some(PathBuf::from(
                r"C:\U\AppData\Local\Microsoft\WindowsApps\winget.exe"
            ))
        );
        assert_eq!(resolve_winget(&env, &|_| false), None);
        assert_eq!(resolve_winget(&env_of(&[]), &|_| true), None);
    }

    #[test]
    fn resolves_choco_install_dir_then_programdata() {
        let env = env_of(&[
            ("ChocolateyInstall", r"D:\choco"),
            ("ProgramData", r"C:\ProgramData"),
        ]);
        assert_eq!(
            resolve_choco(&env, &|_| true),
            Some(PathBuf::from(r"D:\choco\bin\choco.exe"))
        );
        let only_default = |p: &Path| p.starts_with(r"C:\ProgramData");
        assert_eq!(
            resolve_choco(&env, &only_default),
            Some(PathBuf::from(r"C:\ProgramData\chocolatey\bin\choco.exe"))
        );
        assert_eq!(resolve_choco(&env, &|_| false), None);
    }

    fn cmd(args: &[&str], timeout_ms: u64) -> CommandSpec {
        CommandSpec {
            exe: PathBuf::from(r"C:\Windows\System32\cmd.exe"),
            args: args.iter().map(|s| s.to_string()).collect(),
            timeout: Duration::from_millis(timeout_ms),
        }
    }

    #[tokio::test]
    async fn runs_a_process_and_captures_stdout() {
        let out = SystemRunner
            .run(&cmd(&["/c", "echo hi"], 10_000))
            .await
            .unwrap();
        assert_eq!(out.code, Some(0));
        assert_eq!(out.stdout.trim(), "hi");
    }

    #[tokio::test]
    async fn missing_exe_is_source_missing() {
        let mut spec = cmd(&[], 1000);
        spec.exe = PathBuf::from(r"C:\definitely\not\here.exe");
        assert_eq!(
            SystemRunner.run(&spec).await.unwrap_err().code,
            "source_missing"
        );
    }

    #[tokio::test]
    async fn slow_process_times_out() {
        let err = SystemRunner
            .run(&cmd(&["/c", "ping -n 5 127.0.0.1"], 200))
            .await
            .unwrap_err();
        assert_eq!(err.code, "timeout");
    }
}
