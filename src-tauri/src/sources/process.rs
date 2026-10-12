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

/// winget is reached through the per-user App Execution Alias, but that folder is user-writable
/// and this app runs elevated. Only an app link pointing at the Store-installed App Installer is
/// accepted, and its target (admin-protected) is executed directly, so nothing can be swapped
/// between the check and the launch.
pub fn resolve_winget(
    env: &dyn Fn(&str) -> Option<String>,
    link_target: &dyn Fn(&Path) -> Option<String>,
    exists: &dyn Fn(&Path) -> bool,
) -> Option<PathBuf> {
    let alias = PathBuf::from(env("LOCALAPPDATA")?)
        .join("Microsoft")
        .join("WindowsApps")
        .join("winget.exe");
    let target =
        link_target(&alias).filter(|t| crate::sources::trust::is_trusted_winget_target(t))?;
    let target = PathBuf::from(target);
    exists(&target).then_some(target)
}

/// Chocolatey from the machine-wide ChocolateyInstall (literal paths only), else the default
/// under the ProgramData known folder. The user's own environment is never consulted.
pub fn resolve_choco(
    machine_env: &dyn Fn(&str) -> Option<String>,
    program_data: Option<PathBuf>,
    exists: &dyn Fn(&Path) -> bool,
) -> Option<PathBuf> {
    let candidates = [
        crate::sources::trust::literal_path(machine_env("ChocolateyInstall"))
            .map(|d| d.join("bin").join("choco.exe")),
        program_data.map(|d| d.join("chocolatey").join("bin").join("choco.exe")),
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
        use crate::sources::trust::{machine_env, program_data_dir, read_app_link};
        let env = |k: &str| std::env::var(k).ok().filter(|v| !v.is_empty());
        let exists = |p: &Path| p.is_file();
        Self {
            winget: resolve_winget(&env, &read_app_link, &exists),
            choco: resolve_choco(&machine_env, program_data_dir(), &exists),
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
    fn resolves_winget_to_the_trusted_link_target() {
        let env = env_of(&[("LOCALAPPDATA", r"C:\U\AppData\Local")]);
        const TARGET: &str = r"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.29.380.0_x64__8wekyb3d8bbwe\winget.exe";
        let link = |p: &Path| {
            assert_eq!(
                p,
                Path::new(r"C:\U\AppData\Local\Microsoft\WindowsApps\winget.exe")
            );
            Some(TARGET.to_string())
        };
        assert_eq!(
            resolve_winget(&env, &link, &|_| true),
            Some(PathBuf::from(TARGET))
        );
        assert_eq!(resolve_winget(&env, &link, &|_| false), None);
        assert_eq!(resolve_winget(&env_of(&[]), &link, &|_| true), None);
    }

    /// A plain file (not an app link) or a link to an untrusted place is refused: the alias
    /// folder is user-writable and this app runs elevated.
    #[test]
    fn planted_winget_is_refused() {
        let env = env_of(&[("LOCALAPPDATA", r"C:\U\AppData\Local")]);
        assert_eq!(resolve_winget(&env, &|_| None, &|_| true), None);
        let evil = |_: &Path| Some(r"C:\Users\x\evil\winget.exe".to_string());
        assert_eq!(resolve_winget(&env, &evil, &|_| true), None);
    }

    #[test]
    fn resolves_choco_from_machine_env_then_program_data() {
        let machine = env_of(&[("ChocolateyInstall", r"D:\choco")]);
        let pd = Some(PathBuf::from(r"C:\ProgramData"));
        assert_eq!(
            resolve_choco(&machine, pd.clone(), &|_| true),
            Some(PathBuf::from(r"D:\choco\bin\choco.exe"))
        );
        let only_default = |p: &Path| p.starts_with(r"C:\ProgramData");
        assert_eq!(
            resolve_choco(&machine, pd.clone(), &only_default),
            Some(PathBuf::from(r"C:\ProgramData\chocolatey\bin\choco.exe"))
        );
        let expanding = env_of(&[("ChocolateyInstall", r"%USERPROFILE%\choco")]);
        assert_eq!(
            resolve_choco(&expanding, pd.clone(), &|p| !p.starts_with("%")),
            Some(PathBuf::from(r"C:\ProgramData\chocolatey\bin\choco.exe"))
        );
        assert_eq!(resolve_choco(&machine, pd, &|_| false), None);
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
