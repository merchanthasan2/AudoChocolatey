//! Honest progress: phases from package-manager output, percentages only from real numbers
//! (Chocolatey's own percent lines, or bytes on disk versus the server's Content-Length for
//! winget, which prints no progress bar when piped).

use crate::jobs::model::JobPhase;
use regex::Regex;
use std::path::PathBuf;
use std::sync::LazyLock;
use std::time::SystemTime;

#[derive(Debug, Clone, PartialEq)]
pub enum Signal {
    Phase(JobPhase),
    Percent(f32),
    Bytes { done: u64, total: Option<u64> },
}

pub trait LineParser: Send {
    fn feed(&mut self, line: &str) -> Vec<Signal>;
}

static BYTES: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"([\d.]+)\s*(KB|MB|GB)\s*/\s*([\d.]+)\s*(KB|MB|GB)").unwrap());
static CHOCO_PCT: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"^Progress: Downloading .*?(\d{1,3})%\s*$").unwrap());
static CHOCO_INSTALLING: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"^(Installing|Upgrading|Uninstalling) (64-bit |32-bit )?[A-Za-z0-9][\w.\-]*\.\.\.")
        .unwrap()
});
static CHOCO_DONE: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"The (install|upgrade|uninstall) of .+ was successful|^Chocolatey (installed|upgraded|uninstalled) \d+/\d+").unwrap()
});

fn to_bytes(n: &str, unit: &str) -> Option<u64> {
    let v: f64 = n.parse().ok()?;
    let mul = match unit {
        "KB" => 1024.0,
        "MB" => 1024.0 * 1024.0,
        _ => 1024.0 * 1024.0 * 1024.0,
    };
    Some((v * mul).round() as u64)
}

/// Emits a phase only when it changes.
#[derive(Default)]
struct PhaseTrack(Option<JobPhase>);

impl PhaseTrack {
    fn to(&mut self, p: JobPhase, out: &mut Vec<Signal>) {
        if self.0 != Some(p) {
            self.0 = Some(p);
            out.push(Signal::Phase(p));
        }
    }
}

#[derive(Default)]
pub struct WingetLines(PhaseTrack);

impl LineParser for WingetLines {
    fn feed(&mut self, line: &str) -> Vec<Signal> {
        let l = line.trim();
        let mut out = Vec::new();
        if l.starts_with("Downloading http") {
            self.0.to(JobPhase::Downloading, &mut out);
        } else if l.starts_with("Successfully verified installer hash")
            || l.starts_with("Starting package install")
            || l.starts_with("Starting package uninstall")
        {
            self.0.to(JobPhase::Installing, &mut out);
        } else if l.starts_with("Successfully installed")
            || l.starts_with("Successfully uninstalled")
            || l.starts_with("Successfully upgraded")
        {
            self.0.to(JobPhase::Verifying, &mut out);
        } else if let Some(c) = BYTES.captures(l) {
            if let (Some(done), total) = (to_bytes(&c[1], &c[2]), to_bytes(&c[3], &c[4])) {
                out.push(Signal::Bytes { done, total });
            }
        }
        out
    }
}

#[derive(Default)]
pub struct ChocoLines(PhaseTrack);

impl LineParser for ChocoLines {
    fn feed(&mut self, line: &str) -> Vec<Signal> {
        let l = line.trim();
        let mut out = Vec::new();
        if let Some(c) = CHOCO_PCT.captures(l) {
            self.0.to(JobPhase::Downloading, &mut out);
            if let Ok(p) = c[1].parse::<f32>() {
                out.push(Signal::Percent(p.min(100.0)));
            }
        } else if CHOCO_DONE.is_match(l) {
            self.0.to(JobPhase::Verifying, &mut out);
        } else if CHOCO_INSTALLING.is_match(l) {
            self.0.to(JobPhase::Installing, &mut out);
        }
        out
    }
}

pub fn percent(done: u64, total: Option<u64>) -> Option<f32> {
    let total = total.filter(|t| *t > 0)?;
    Some(((done as f64 / total as f64) * 100.0).min(100.0) as f32)
}

/// Size of the largest file written under `dir` after `since` (winget's download cache).
pub struct DownloadWatcher {
    pub dir: PathBuf,
    pub since: SystemTime,
}

impl DownloadWatcher {
    pub fn sample(&self) -> Option<u64> {
        let mut best: Option<u64> = None;
        let mut stack = vec![self.dir.clone()];
        while let Some(d) = stack.pop() {
            let Ok(entries) = std::fs::read_dir(&d) else {
                continue;
            };
            for e in entries.flatten() {
                let Ok(meta) = e.metadata() else { continue };
                if meta.is_dir() {
                    stack.push(e.path());
                } else if meta.len() > 0 && meta.modified().is_ok_and(|m| m > self.since) {
                    best = Some(best.unwrap_or(0).max(meta.len()));
                }
            }
        }
        best
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use std::time::{Duration, SystemTime};

    const DOWNLOAD: &str = include_str!("../../tests/fixtures/winget/download-7zip.en.txt");

    #[test]
    fn winget_lines_drive_phases() {
        let mut p = WingetLines::default();
        let phases: Vec<_> = DOWNLOAD
            .lines()
            .flat_map(|l| p.feed(l))
            .filter_map(|s| match s {
                Signal::Phase(ph) => Some(ph),
                _ => None,
            })
            .collect();
        assert_eq!(phases, [JobPhase::Downloading, JobPhase::Installing]);
        assert_eq!(
            p.feed("Successfully installed"),
            vec![Signal::Phase(JobPhase::Verifying)]
        );
        assert_eq!(
            p.feed("Starting package uninstall..."),
            vec![Signal::Phase(JobPhase::Installing)]
        );
    }

    #[test]
    fn winget_byte_lines_if_ever_printed() {
        let mut p = WingetLines::default();
        assert_eq!(
            p.feed("  ██████▒▒▒▒  12.5 MB / 50.0 MB"),
            vec![Signal::Bytes {
                done: 13_107_200,
                total: Some(52_428_800)
            }]
        );
    }

    #[test]
    fn choco_progress_percent() {
        let mut p = ChocoLines::default();
        assert_eq!(
            p.feed("Progress: Downloading 7zip.install 26.04... 45%"),
            vec![Signal::Phase(JobPhase::Downloading), Signal::Percent(45.0)]
        );
        assert_eq!(
            p.feed("Progress: Downloading x 1.0... 100%"),
            vec![Signal::Percent(100.0)]
        );
        assert_eq!(
            p.feed("Installing 7zip.install..."),
            vec![Signal::Phase(JobPhase::Installing)]
        );
        assert_eq!(
            p.feed(" The install of 7zip.install was successful."),
            vec![Signal::Phase(JobPhase::Verifying)]
        );
        assert!(p.feed("random noise").is_empty());
    }

    #[test]
    fn watcher_reports_newest_largest_file() {
        let dir = tempfile::tempdir().unwrap();
        std::fs::write(dir.path().join("old.bin"), vec![0u8; 10]).unwrap();
        std::thread::sleep(Duration::from_millis(50));
        let since = SystemTime::now();
        let w = DownloadWatcher {
            dir: dir.path().to_path_buf(),
            since,
        };
        assert_eq!(w.sample(), None);
        std::thread::sleep(Duration::from_millis(50));
        let nested = dir.path().join("Pkg.1.0");
        std::fs::create_dir(&nested).unwrap();
        let mut f = std::fs::File::create(nested.join("setup.exe")).unwrap();
        f.write_all(&vec![0u8; 3 * 1024 * 1024]).unwrap();
        f.sync_all().unwrap();
        assert_eq!(w.sample(), Some(3_145_728));
        assert_eq!(
            DownloadWatcher {
                dir: dir.path().join("missing"),
                since
            }
            .sample(),
            None
        );
    }

    /// Delivery Optimization creates an empty placeholder and writes the whole file at the end
    /// (observed live); an empty file must not show as a stuck "0%".
    #[test]
    fn empty_placeholder_files_are_ignored() {
        let dir = tempfile::tempdir().unwrap();
        let since = SystemTime::now() - Duration::from_secs(1);
        std::fs::write(dir.path().join("DOAA2A.tmp"), b"").unwrap();
        let w = DownloadWatcher {
            dir: dir.path().to_path_buf(),
            since,
        };
        assert_eq!(w.sample(), None);
    }

    #[test]
    fn percent_from_bytes() {
        assert_eq!(percent(50, Some(200)), Some(25.0));
        assert_eq!(percent(300, Some(200)), Some(100.0));
        assert_eq!(percent(5, None), None);
        assert_eq!(percent(5, Some(0)), None);
    }
}
