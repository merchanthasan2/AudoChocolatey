//! "Remove the new desktop icon?" The desktop is looked at when an install or update starts and
//! again when it succeeds; only icons that appeared in between are offered. Paths never come
//! from the page: it names a job, and Rust removes what it found for that job.

use crate::ids::SourceRef;
use crate::jobs::model::{Job, JobPhase};
use crate::sources::model::OpKind;
use std::collections::{BTreeSet, HashMap};
use std::path::{Path, PathBuf};
use std::sync::Mutex;

/// The person's desktop and the shared (Public) desktop.
pub fn desktop_dirs() -> Vec<PathBuf> {
    use windows_sys::Win32::UI::Shell::{FOLDERID_Desktop, FOLDERID_PublicDesktop};
    [&FOLDERID_Desktop, &FOLDERID_PublicDesktop]
        .into_iter()
        .filter_map(crate::sources::trust::known_folder)
        .collect()
}

pub(crate) fn is_shortcut(p: &Path) -> bool {
    p.extension()
        .and_then(|e| e.to_str())
        .is_some_and(|e| e.eq_ignore_ascii_case("lnk") || e.eq_ignore_ascii_case("url"))
}

/// The shortcut files directly on these desktops.
pub fn snapshot(dirs: &[PathBuf]) -> BTreeSet<PathBuf> {
    dirs.iter()
        .filter_map(|d| std::fs::read_dir(d).ok())
        .flat_map(|rd| rd.filter_map(Result::ok))
        .map(|e| e.path())
        .filter(|p| p.is_file() && is_shortcut(p))
        .collect()
}

/// What changed on the desktop while a job ran.
#[derive(Default)]
pub struct DesktopWatch {
    before: HashMap<String, BTreeSet<PathBuf>>,
}

impl DesktopWatch {
    /// Feed every job change. Returns the new shortcuts when an install or update succeeds.
    pub fn observe(
        &mut self,
        job: &Job,
        look: impl Fn() -> BTreeSet<PathBuf>,
    ) -> Option<Vec<PathBuf>> {
        let watched = matches!(job.op.kind, OpKind::Install | OpKind::Upgrade);
        match job.phase {
            JobPhase::Preparing if watched && !self.before.contains_key(&job.id) => {
                // A few jobs at most are running; forget very old ones so nothing grows.
                if self.before.len() >= 32 {
                    self.before.clear();
                }
                self.before.insert(job.id.clone(), look());
                None
            }
            JobPhase::Succeeded => {
                let before = self.before.remove(&job.id)?;
                let new: Vec<PathBuf> = look().difference(&before).cloned().collect();
                (!new.is_empty()).then_some(new)
            }
            p if p.is_finished() => {
                self.before.remove(&job.id);
                None
            }
            _ => None,
        }
    }
}

/// New shortcuts waiting for an answer, by job id.
type Found = HashMap<String, (SourceRef, Vec<PathBuf>)>;
static FOUND: Mutex<Option<Found>> = Mutex::new(None);

pub fn remember(job_id: &str, target: SourceRef, files: Vec<PathBuf>) {
    let mut g = FOUND.lock().unwrap_or_else(|e| e.into_inner());
    let map = g.get_or_insert_with(HashMap::new);
    if map.len() >= 64 {
        map.clear();
    }
    map.insert(job_id.to_string(), (target, files));
}

pub fn take(job_id: &str) -> Option<(SourceRef, Vec<PathBuf>)> {
    FOUND
        .lock()
        .unwrap_or_else(|e| e.into_inner())
        .as_mut()?
        .remove(job_id)
}

/// Deletes these shortcuts if they are still on one of the desktops. Returns how many went.
pub fn remove(files: &[PathBuf], dirs: &[PathBuf]) -> usize {
    files
        .iter()
        .filter(|f| is_shortcut(f) && f.parent().is_some_and(|p| dirs.iter().any(|d| d == p)))
        .filter(|f| std::fs::remove_file(f).is_ok())
        .count()
}

/// The file names to show ("VLC media player"), without the extension.
pub fn names(files: &[PathBuf]) -> Vec<String> {
    files
        .iter()
        .filter_map(|f| f.file_stem().map(|s| s.to_string_lossy().into_owned()))
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind;
    use crate::sources::model::OpRequest;

    fn job(kind: OpKind, phase: JobPhase) -> Job {
        let mut j = Job::new(
            "j1".into(),
            OpRequest {
                kind,
                target: SourceRef::new(SourceKind::Winget, "VideoLAN.VLC").unwrap(),
                version: None,
                mode: Default::default(),
            },
            "VLC".into(),
        );
        j.phase = phase;
        j
    }

    fn set(names: &[&str]) -> BTreeSet<PathBuf> {
        names
            .iter()
            .map(|n| PathBuf::from(format!(r"C:\D\{n}")))
            .collect()
    }

    #[test]
    fn offers_only_icons_that_appeared_during_the_job() {
        let mut w = DesktopWatch::default();
        assert_eq!(
            w.observe(&job(OpKind::Upgrade, JobPhase::Preparing), || set(&[
                "a.lnk"
            ])),
            None
        );
        let new = w.observe(&job(OpKind::Upgrade, JobPhase::Succeeded), || {
            set(&["a.lnk", "VLC media player.lnk"])
        });
        assert_eq!(new, Some(vec![PathBuf::from(r"C:\D\VLC media player.lnk")]));
        // Asked once only.
        assert_eq!(
            w.observe(&job(OpKind::Upgrade, JobPhase::Succeeded), || set(&[
                "b.lnk"
            ])),
            None
        );
    }

    #[test]
    fn nothing_new_failures_and_removals_offer_nothing() {
        let mut w = DesktopWatch::default();
        w.observe(&job(OpKind::Install, JobPhase::Preparing), || {
            set(&["a.lnk"])
        });
        assert_eq!(
            w.observe(&job(OpKind::Install, JobPhase::Succeeded), || set(&[
                "a.lnk"
            ])),
            None
        );

        w.observe(&job(OpKind::Install, JobPhase::Preparing), || set(&[]));
        assert_eq!(
            w.observe(&job(OpKind::Install, JobPhase::Failed), || set(&["x.lnk"])),
            None
        );
        assert_eq!(
            w.observe(&job(OpKind::Install, JobPhase::Succeeded), || set(&[
                "x.lnk"
            ])),
            None
        );

        w.observe(&job(OpKind::Uninstall, JobPhase::Preparing), || set(&[]));
        assert_eq!(
            w.observe(&job(OpKind::Uninstall, JobPhase::Succeeded), || set(&[
                "x.lnk"
            ])),
            None
        );
    }

    #[test]
    fn removes_only_shortcuts_on_a_desktop() {
        let dir = std::env::temp_dir().join(format!("qu-sc-{}", std::process::id()));
        let other = dir.join("sub");
        std::fs::create_dir_all(&other).unwrap();
        let lnk = dir.join("App.lnk");
        let txt = dir.join("notes.txt");
        let deep = other.join("Deep.lnk");
        for f in [&lnk, &txt, &deep] {
            std::fs::write(f, b"x").unwrap();
        }
        let dirs = vec![dir.clone()];
        assert_eq!(snapshot(&dirs), BTreeSet::from([lnk.clone()]));
        assert_eq!(remove(&[lnk.clone(), txt.clone(), deep.clone()], &dirs), 1);
        assert!(!lnk.exists() && txt.exists() && deep.exists());
        assert_eq!(names(std::slice::from_ref(&deep)), vec!["Deep".to_string()]);
        let _ = std::fs::remove_dir_all(&dir);
    }
}
