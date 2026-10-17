//! Start at sign-in through a Scheduled Task (the Run key is skipped for admin apps; ADR 014).
//! The task always points at the installed copy under Program Files, belongs to one user, and
//! is registered from memory (no temp file someone could swap).

use std::path::{Component, Path, PathBuf};

pub const TASK_FOLDER: &str = r"\QuantumStep";
pub const LAUNCH_ARG: &str = "--background";

pub fn task_name(user_sid: &str) -> String {
    format!("Quantum Updater {user_sid}")
}

fn xml_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
        .replace('\'', "&apos;")
}

/// The task definition. Defaults that `schtasks /SC ONLOGON` would give (stop after 72 h, never
/// start on battery, below-normal priority) are all overridden here.
pub fn task_xml(exe: &Path, user_sid: &str) -> String {
    let exe = xml_escape(&exe.to_string_lossy());
    let sid = xml_escape(user_sid);
    format!(
        r#"<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>Quantum Step</Author>
    <Description>Starts Quantum Updater in the tray when you sign in, so your apps stay up to date.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{sid}</UserId>
      <Delay>PT15S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>{sid}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>{exe}</Command>
      <Arguments>{LAUNCH_ARG}</Arguments>
    </Exec>
  </Actions>
</Task>
"#
    )
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum AutostartBlock {
    DevBuild,
    NeedsAdmin,
    OtherAccount,
    NotInstalled,
}

impl AutostartBlock {
    pub fn message(self) -> &'static str {
        match self {
            Self::DevBuild => "Start at sign-in works only in the installed app, not in a development build.",
            Self::NeedsAdmin => "Quantum Updater needs administrator access to set this up.",
            Self::OtherAccount => "Quantum Updater is running as a different administrator than the person signed in, so it can't start at your sign-in.",
            Self::NotInstalled => "Start at sign-in works only for the copy installed in Program Files.",
        }
    }
}

/// `exe` must be inside `program_files` (compared folder by folder, ignoring case, no `..`).
fn inside(exe: &Path, program_files: &Path) -> bool {
    let parts = |p: &Path| -> Option<Vec<String>> {
        p.components()
            .map(|c| match c {
                Component::ParentDir | Component::CurDir => None,
                c => Some(c.as_os_str().to_string_lossy().to_lowercase()),
            })
            .collect()
    };
    match (parts(exe), parts(program_files)) {
        (Some(e), Some(pf)) => e.len() > pf.len() && e[..pf.len()] == pf[..],
        _ => false,
    }
}

pub fn allowed(
    exe: &Path,
    program_files: &Path,
    elevated: bool,
    same_user: Option<bool>,
    debug: bool,
) -> Result<(), AutostartBlock> {
    if debug {
        return Err(AutostartBlock::DevBuild);
    }
    if !elevated {
        return Err(AutostartBlock::NeedsAdmin);
    }
    if same_user != Some(true) {
        return Err(AutostartBlock::OtherAccount);
    }
    if !inside(exe, program_files) {
        return Err(AutostartBlock::NotInstalled);
    }
    Ok(())
}

/// What an existing task points at.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TaskInfo {
    pub exe: String,
    pub args: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Reconcile {
    Noop,
    Register,
    Delete,
}

/// Run at every start: re-points a task after the app moved (e.g. an update), removes it when
/// the setting is off.
pub fn reconcile(want: bool, existing: Option<&TaskInfo>, exe: &Path) -> Reconcile {
    match (want, existing) {
        (true, None) => Reconcile::Register,
        (true, Some(t)) => {
            let same_exe = t.exe.eq_ignore_ascii_case(&exe.to_string_lossy());
            if same_exe && t.args.trim() == LAUNCH_ARG {
                Reconcile::Noop
            } else {
                Reconcile::Register
            }
        }
        (false, Some(_)) => Reconcile::Delete,
        (false, None) => Reconcile::Noop,
    }
}

/// Where tasks live: Task Scheduler in the app (`taskstore::ComTaskStore`), a fake in tests.
pub trait TaskStore {
    fn get(&self, name: &str) -> Result<Option<TaskInfo>, String>;
    fn register(&self, name: &str, xml: &str) -> Result<(), String>;
    fn delete(&self, name: &str) -> Result<(), String>;
}

/// The facts about this run that the rules depend on.
pub struct Env {
    pub sid: Option<String>,
    pub exe: PathBuf,
    pub program_files: Option<PathBuf>,
    pub elevated: bool,
    pub same_user: Option<bool>,
    pub debug: bool,
}

impl Env {
    pub fn current() -> Result<Self, String> {
        Ok(Self {
            sid: crate::commands::system::current_user_sid(),
            exe: std::env::current_exe().map_err(|e| e.to_string())?,
            program_files: crate::sources::trust::program_files_dir(),
            elevated: crate::commands::system::is_elevated(),
            same_user: crate::commands::system::shell_user_matches(),
            debug: cfg!(debug_assertions),
        })
    }
}

/// Turns start-at-sign-in on or off for the current user, checking every rule first.
pub fn apply_with(store: &dyn TaskStore, env: &Env, want: bool) -> Result<(), String> {
    let sid = env
        .sid
        .as_deref()
        .ok_or("Couldn't identify your account.")?;
    let name = task_name(sid);
    if !want {
        return store.delete(&name);
    }
    let pf = env
        .program_files
        .as_deref()
        .ok_or("Couldn't find Program Files.")?;
    allowed(&env.exe, pf, env.elevated, env.same_user, env.debug)
        .map_err(|b| b.message().to_string())?;
    store.register(&name, &task_xml(&env.exe, sid))
}

/// At start: repair a task that points at an old location, or remove one that's no longer
/// wanted. Never creates one before the person answered the first-run question.
pub fn reconcile_with(store: &dyn TaskStore, env: &Env, want: bool, asked: bool) {
    if !asked || env.debug {
        return;
    }
    let Some(sid) = env.sid.as_deref() else {
        return;
    };
    let existing = store.get(&task_name(sid)).ok().flatten();
    if reconcile(want, existing.as_ref(), &env.exe) != Reconcile::Noop {
        let _ = apply_with(store, env, want);
    }
}

pub fn apply(want: bool) -> Result<(), String> {
    apply_with(&super::taskstore::ComTaskStore, &Env::current()?, want)
}

pub fn reconcile_now(want: bool, asked: bool) {
    if let Ok(env) = Env::current() {
        reconcile_with(&super::taskstore::ComTaskStore, &env, want, asked);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::cell::RefCell;
    use std::collections::HashMap;
    use std::path::Path;

    const SID: &str = "S-1-5-21-1-2-3-1001";

    #[test]
    fn task_definition() {
        let xml = task_xml(
            Path::new(r"C:\Program Files\Quantum Updater & Co\quantum_updater.exe"),
            SID,
        );
        assert!(
            xml.contains(
                r"<Command>C:\Program Files\Quantum Updater &amp; Co\quantum_updater.exe</Command>"
            ),
            "{xml}"
        );
        assert!(xml.contains("<Arguments>--background</Arguments>"));
        assert!(xml.contains(&format!("<UserId>{SID}</UserId>")));
        assert!(xml.contains("<LogonType>InteractiveToken</LogonType>"));
        assert!(xml.contains("<RunLevel>HighestAvailable</RunLevel>"));
        assert!(xml.contains("<Delay>PT15S</Delay>"));
        assert!(xml.contains("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>"));
        assert!(xml.contains("<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>"));
        assert!(
            xml.contains("<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>"),
            "never killed after 72 h"
        );
        assert!(
            xml.contains("<Priority>4</Priority>"),
            "installers inherit normal priority"
        );
        assert!(xml.contains("<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>"));
        assert_eq!(task_name(SID), r"Quantum Updater S-1-5-21-1-2-3-1001");
    }

    #[test]
    fn only_the_installed_copy_for_the_signed_in_admin() {
        let pf = Path::new(r"C:\Program Files");
        let installed = Path::new(r"C:\PROGRAM FILES\Quantum Updater\quantum_updater.exe");
        assert_eq!(allowed(installed, pf, true, Some(true), false), Ok(()));
        assert_eq!(
            allowed(installed, pf, true, Some(true), true),
            Err(AutostartBlock::DevBuild)
        );
        assert_eq!(
            allowed(installed, pf, false, Some(true), false),
            Err(AutostartBlock::NeedsAdmin)
        );
        assert_eq!(
            allowed(installed, pf, true, Some(false), false),
            Err(AutostartBlock::OtherAccount)
        );
        assert_eq!(
            allowed(installed, pf, true, None, false),
            Err(AutostartBlock::OtherAccount)
        );
        for elsewhere in [
            r"D:\Apps\quantum_updater.exe",
            r"C:\Users\me\AppData\Local\quantum_updater.exe",
            r"C:\Program Files (x86)\Quantum Updater\quantum_updater.exe",
            r"C:\Program Files\..\Users\me\quantum_updater.exe",
        ] {
            assert_eq!(
                allowed(Path::new(elsewhere), pf, true, Some(true), false),
                Err(AutostartBlock::NotInstalled),
                "{elsewhere}"
            );
        }
    }

    #[test]
    fn reconcile_repairs_or_removes() {
        let exe = Path::new(r"C:\Program Files\Quantum Updater\quantum_updater.exe");
        let same = TaskInfo {
            exe: exe.to_string_lossy().to_uppercase(),
            args: "--background".into(),
        };
        let moved = TaskInfo {
            exe: r"C:\Old\quantum_updater.exe".into(),
            args: "--background".into(),
        };
        let odd_args = TaskInfo {
            exe: exe.to_string_lossy().into(),
            args: "--something".into(),
        };
        assert_eq!(reconcile(true, None, exe), Reconcile::Register);
        assert_eq!(reconcile(true, Some(&same), exe), Reconcile::Noop);
        assert_eq!(reconcile(true, Some(&moved), exe), Reconcile::Register);
        assert_eq!(reconcile(true, Some(&odd_args), exe), Reconcile::Register);
        assert_eq!(reconcile(false, Some(&same), exe), Reconcile::Delete);
        assert_eq!(reconcile(false, None, exe), Reconcile::Noop);
    }

    #[test]
    fn block_messages_are_plain() {
        let tool =
            regex::Regex::new(r"(?i)\b(winget|chocolatey|choco|scoop|msstore|schtasks)\b").unwrap();
        for b in [
            AutostartBlock::DevBuild,
            AutostartBlock::NeedsAdmin,
            AutostartBlock::OtherAccount,
            AutostartBlock::NotInstalled,
        ] {
            assert!(!tool.is_match(b.message()));
        }
    }

    #[derive(Default)]
    struct FakeTaskStore {
        tasks: RefCell<HashMap<String, TaskInfo>>,
        log: RefCell<Vec<String>>,
    }

    impl TaskStore for FakeTaskStore {
        fn get(&self, name: &str) -> Result<Option<TaskInfo>, String> {
            Ok(self.tasks.borrow().get(name).cloned())
        }
        fn register(&self, name: &str, xml: &str) -> Result<(), String> {
            self.log.borrow_mut().push(format!("register {name}"));
            let exe = xml
                .split("<Command>")
                .nth(1)
                .and_then(|r| r.split("</Command>").next())
                .unwrap_or("");
            self.tasks.borrow_mut().insert(
                name.into(),
                TaskInfo {
                    exe: exe.into(),
                    args: LAUNCH_ARG.into(),
                },
            );
            Ok(())
        }
        fn delete(&self, name: &str) -> Result<(), String> {
            self.log.borrow_mut().push(format!("delete {name}"));
            self.tasks.borrow_mut().remove(name);
            Ok(())
        }
    }

    fn installed() -> Env {
        Env {
            sid: Some(SID.into()),
            exe: r"C:\Program Files\Quantum Updater\quantum_updater.exe".into(),
            program_files: Some(r"C:\Program Files".into()),
            elevated: true,
            same_user: Some(true),
            debug: false,
        }
    }

    #[test]
    fn apply_registers_then_removes() {
        let store = FakeTaskStore::default();
        apply_with(&store, &installed(), true).unwrap();
        assert!(store.get(&task_name(SID)).unwrap().is_some());
        apply_with(&store, &installed(), false).unwrap();
        assert!(store.get(&task_name(SID)).unwrap().is_none());
    }

    #[test]
    fn a_blocked_setup_never_touches_task_scheduler() {
        let store = FakeTaskStore::default();
        let env = Env {
            exe: r"C:\Users\me\Downloads\quantum_updater.exe".into(),
            ..installed()
        };
        assert_eq!(
            apply_with(&store, &env, true),
            Err(AutostartBlock::NotInstalled.message().to_string())
        );
        let env = Env {
            debug: true,
            ..installed()
        };
        assert_eq!(
            apply_with(&store, &env, true),
            Err(AutostartBlock::DevBuild.message().to_string())
        );
        assert!(store.log.borrow().is_empty());
    }

    #[test]
    fn reconcile_waits_for_the_first_run_answer() {
        let store = FakeTaskStore::default();
        reconcile_with(&store, &installed(), true, false);
        assert!(
            store.log.borrow().is_empty(),
            "nothing before the person said yes"
        );
        reconcile_with(&store, &installed(), true, true);
        reconcile_with(&store, &installed(), true, true);
        assert_eq!(
            store.log.borrow().len(),
            1,
            "an up-to-date task is left alone"
        );
        let moved = Env {
            exe: r"C:\Program Files\Quantum Updater 2\quantum_updater.exe".into(),
            ..installed()
        };
        reconcile_with(&store, &moved, true, true);
        assert_eq!(
            store.get(&task_name(SID)).unwrap().unwrap().exe,
            moved.exe.to_string_lossy()
        );
        reconcile_with(&store, &installed(), false, true);
        assert!(store.get(&task_name(SID)).unwrap().is_none());
    }
}
