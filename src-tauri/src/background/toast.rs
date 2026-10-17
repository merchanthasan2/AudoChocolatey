//! Windows notifications when new updates appear. Text comes from package names, so the toast
//! XML is built with the DOM API, never by string formatting.

use crate::jobs::model::{Job, JobPhase};
use crate::sources::model::OpKind;
use std::collections::HashSet;

const NAME_MAX: usize = 60;

/// Shown once, the first time closing hides the window to the tray.
pub const TRAY_HINT: (&str, &str) = (
    "Still running in the tray",
    "Quantum Updater keeps checking for updates. Right-click its tray icon to quit.",
);

fn short(name: &str) -> String {
    if name.chars().count() <= NAME_MAX {
        name.to_string()
    } else {
        format!("{}…", name.chars().take(NAME_MAX - 1).collect::<String>())
    }
}

/// Title and body for "new updates appeared".
pub fn update_lines(names: &[String]) -> (String, String) {
    let first = names.first().map(|n| short(n)).unwrap_or_default();
    match names.len() {
        0 | 1 => ("Update ready".into(), format!("{first} can be updated.")),
        2 => (
            "2 updates ready".into(),
            format!("{first} and {} can be updated.", short(&names[1])),
        ),
        n => (
            format!("{n} updates ready"),
            format!("{first} and {} more can be updated.", n - 1),
        ),
    }
}

/// One finished job, as the summary needs it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Done {
    pub name: String,
    pub kind: OpKind,
    pub ok: bool,
}

/// Collects the jobs that ran since the queue was last idle. Only jobs seen while running in
/// this session count, so history loaded at start never produces a notification.
#[derive(Default)]
pub struct BatchWatch {
    active: HashSet<String>,
}

impl BatchWatch {
    /// Returns the batch once nothing is left to run.
    pub fn observe(&mut self, jobs: &[Job]) -> Option<Vec<Done>> {
        for j in jobs.iter().filter(|j| !j.phase.is_finished()) {
            self.active.insert(j.id.clone());
        }
        if self.active.is_empty() || jobs.iter().any(|j| !j.phase.is_finished()) {
            return None;
        }
        let ran = std::mem::take(&mut self.active);
        Some(
            jobs.iter()
                .filter(|j| ran.contains(&j.id))
                .filter(|j| matches!(j.phase, JobPhase::Succeeded | JobPhase::Failed))
                .map(|j| Done {
                    name: j.display_name.clone(),
                    kind: j.op.kind,
                    ok: j.phase == JobPhase::Succeeded,
                })
                .collect(),
        )
    }
}

fn past(kind: OpKind) -> &'static str {
    match kind {
        OpKind::Install => "installed",
        OpKind::Upgrade => "updated",
        OpKind::Uninstall => "removed",
        OpKind::Rollback => "rolled back",
    }
}

/// Notifications for a finished batch: one for what worked, one for what didn't, each only
/// when its switch is on.
pub fn batch_lines(batch: &[Done], finished: bool, failed: bool) -> Vec<(String, String)> {
    let (ok, bad): (Vec<&Done>, Vec<&Done>) = batch.iter().partition(|d| d.ok);
    let mut out = Vec::new();
    if finished && !ok.is_empty() {
        out.push(match ok.as_slice() {
            [one] => (
                format!("{} {}", short(&one.name), past(one.kind)),
                "Finished without problems.".into(),
            ),
            [a, b] => (
                "2 apps done".into(),
                format!("{} and {} finished.", short(&a.name), short(&b.name)),
            ),
            [a, rest @ ..] => (
                format!("{} apps done", ok.len()),
                format!("{} and {} more finished.", short(&a.name), rest.len()),
            ),
            [] => unreachable!(),
        });
    }
    if failed && !bad.is_empty() {
        out.push(match bad.as_slice() {
            [one] => (
                format!("{} wasn't {}", short(&one.name), past(one.kind)),
                "Open Quantum Updater to see why and try again.".into(),
            ),
            [a, rest @ ..] => (
                format!("{} changes didn't finish", bad.len()),
                format!(
                    "{} and {} more. Open Quantum Updater to see why.",
                    short(&a.name),
                    rest.len()
                ),
            ),
            [] => unreachable!(),
        });
    }
    out
}

/// Shows a notification as Quantum Updater. Clicking it starts the app through its Start menu
/// shortcut, which single-instance turns into "show the window". Debug builds have no
/// shortcut, so they skip it.
pub fn show(title: &str, body: &str) {
    if cfg!(debug_assertions) {
        eprintln!("notification (debug, not shown): {title} / {body}");
        return;
    }
    let (title, body) = (title.to_string(), body.to_string());
    // WinRT needs an initialised apartment; use a short-lived thread of our own.
    std::thread::spawn(move || {
        // SAFETY: balanced with CoUninitialize below on this thread.
        let _ = unsafe {
            windows::Win32::System::Com::CoInitializeEx(
                None,
                windows::Win32::System::Com::COINIT_MULTITHREADED,
            )
        };
        if let Err(e) = post(&title, &body) {
            eprintln!("notification failed: {e}");
        }
        // SAFETY: matches the CoInitializeEx above.
        unsafe { windows::Win32::System::Com::CoUninitialize() };
    });
}

fn post(title: &str, body: &str) -> windows::core::Result<()> {
    use windows::core::HSTRING;
    use windows::Data::Xml::Dom::XmlDocument;
    use windows::UI::Notifications::{ToastNotification, ToastNotificationManager};
    let xml = XmlDocument::new()?;
    xml.LoadXml(&HSTRING::from(
        "<toast launch=\"open:updates\"><visual><binding template=\"ToastGeneric\"><text></text><text></text></binding></visual></toast>",
    ))?;
    let texts = xml.GetElementsByTagName(&HSTRING::from("text"))?;
    texts
        .Item(0)?
        .AppendChild(&xml.CreateTextNode(&HSTRING::from(title))?)?;
    texts
        .Item(1)?
        .AppendChild(&xml.CreateTextNode(&HSTRING::from(body))?)?;
    let toast = ToastNotification::CreateToastNotification(&xml)?;
    ToastNotificationManager::CreateToastNotifierWithId(&HSTRING::from(crate::APP_ID))?.Show(&toast)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn names(v: &[&str]) -> Vec<String> {
        v.iter().map(|s| s.to_string()).collect()
    }

    #[test]
    fn lines_for_one_two_and_many() {
        assert_eq!(
            update_lines(&names(&["Firefox"])),
            ("Update ready".into(), "Firefox can be updated.".into())
        );
        assert_eq!(
            update_lines(&names(&["Firefox", "Zoom"])).1,
            "Firefox and Zoom can be updated."
        );
        assert_eq!(
            update_lines(&names(&["Firefox", "Zoom", "Git"])),
            (
                "3 updates ready".into(),
                "Firefox and 2 more can be updated.".into()
            )
        );
    }

    #[test]
    fn long_names_are_cut() {
        let long = "A".repeat(200);
        let (_, body) = update_lines(&[long]);
        assert!(body.chars().count() < 80, "{body}");
        assert!(body.contains('…'));
    }

    #[test]
    fn never_names_package_tools() {
        let tool = regex::Regex::new(r"(?i)\b(winget|chocolatey|choco|scoop|msstore)\b").unwrap();
        let (t, b) = update_lines(&names(&["Firefox", "Zoom", "Git"]));
        assert!(!tool.is_match(&t) && !tool.is_match(&b));
        assert!(!tool.is_match(TRAY_HINT.0) && !tool.is_match(TRAY_HINT.1));
        let mixed = [
            done("Firefox", true),
            done("Zoom", false),
            done("Git", false),
        ];
        for (t, b) in batch_lines(&mixed, true, true) {
            assert!(!tool.is_match(&t) && !tool.is_match(&b), "{t} / {b}");
        }
    }

    fn done(name: &str, ok: bool) -> Done {
        Done {
            name: name.into(),
            kind: OpKind::Upgrade,
            ok,
        }
    }

    fn job(id: &str, phase: JobPhase) -> Job {
        use crate::ids::SourceKind;
        use crate::ids::SourceRef;
        use crate::sources::model::OpRequest;
        let op = OpRequest {
            kind: OpKind::Upgrade,
            target: SourceRef::new(SourceKind::Winget, "Mozilla.Firefox").unwrap(),
            version: None,
        };
        Job {
            phase,
            ..Job::new(id.into(), op, format!("App {id}"))
        }
    }

    #[test]
    fn batch_reported_once_when_the_queue_goes_idle() {
        let mut w = BatchWatch::default();
        // History loaded at start (finished before we saw it run) never notifies.
        assert_eq!(w.observe(&[job("old", JobPhase::Failed)]), None);
        assert_eq!(
            w.observe(&[
                job("old", JobPhase::Failed),
                job("a", JobPhase::Queued),
                job("b", JobPhase::Queued)
            ]),
            None
        );
        assert_eq!(
            w.observe(&[
                job("old", JobPhase::Failed),
                job("a", JobPhase::Succeeded),
                job("b", JobPhase::Installing)
            ]),
            None
        );
        let batch = w
            .observe(&[
                job("old", JobPhase::Failed),
                job("a", JobPhase::Succeeded),
                job("b", JobPhase::Failed),
            ])
            .unwrap();
        assert_eq!(
            batch
                .iter()
                .map(|d| (d.name.as_str(), d.ok))
                .collect::<Vec<_>>(),
            [("App a", true), ("App b", false)]
        );
        assert_eq!(
            w.observe(&[job("a", JobPhase::Succeeded), job("b", JobPhase::Failed)]),
            None,
            "only once"
        );
    }

    #[test]
    fn cancelled_and_skipped_jobs_are_left_out() {
        let mut w = BatchWatch::default();
        w.observe(&[job("a", JobPhase::Queued), job("b", JobPhase::Queued)]);
        assert_eq!(
            w.observe(&[job("a", JobPhase::Cancelled), job("b", JobPhase::Skipped)]),
            Some(vec![])
        );
        assert!(batch_lines(&[], true, true).is_empty());
    }

    #[test]
    fn finished_and_failed_lines_follow_their_switches() {
        let one_ok = [done("Firefox", true)];
        assert_eq!(
            batch_lines(&one_ok, true, true),
            [(
                "Firefox updated".to_string(),
                "Finished without problems.".to_string()
            )]
        );
        assert!(batch_lines(&one_ok, false, true).is_empty());
        let mixed = [
            done("Firefox", true),
            done("Zoom", true),
            done("Git", true),
            done("VLC", false),
        ];
        assert_eq!(
            batch_lines(&mixed, true, true),
            [
                (
                    "3 apps done".to_string(),
                    "Firefox and 2 more finished.".to_string()
                ),
                (
                    "VLC wasn't updated".to_string(),
                    "Open Quantum Updater to see why and try again.".to_string()
                ),
            ]
        );
        assert_eq!(batch_lines(&mixed, false, true).len(), 1);
        assert!(batch_lines(&mixed, false, false).is_empty());
        let two_bad = [
            done("Zoom", false),
            Done {
                kind: OpKind::Install,
                ..done("Git", false)
            },
        ];
        assert_eq!(
            batch_lines(&two_bad, true, true)[0].0,
            "2 changes didn't finish"
        );
        let one_install = [Done {
            kind: OpKind::Install,
            ..done("Git", false)
        }];
        assert_eq!(
            batch_lines(&one_install, true, true)[0].0,
            "Git wasn't installed"
        );
    }
}
