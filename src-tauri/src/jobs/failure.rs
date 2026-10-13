//! Why a job failed, in words a person can act on. Messages never name the package tool:
//! people see the app and what to do, not "winget" or "Chocolatey" (owner decision 2026-10-13).
//! The raw exit code always goes to the job log for support.

use serde::{Deserialize, Serialize};
use ts_rs::TS;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum FailureReason {
    NotElevated,
    InstallerBusy,
    RebootPending,
    HashMismatch,
    DownloadFailed,
    Offline,
    NotFound,
    NoApplicableInstaller,
    ProhibitsElevation,
    AppRunning,
    DiskFull,
    LowMemory,
    MissingDependency,
    Blocked,
    NotSupported,
    NewerInstalled,
    CannotUpgrade,
    Interrupted,
    TimedOut,
    Stopped,
    ToolMissing,
    InstallerError,
    UninstallerError,
    Crashed,
    Unknown,
}

impl FailureReason {
    #[cfg(test)]
    pub const ALL: [FailureReason; 25] = [
        Self::NotElevated,
        Self::InstallerBusy,
        Self::RebootPending,
        Self::HashMismatch,
        Self::DownloadFailed,
        Self::Offline,
        Self::NotFound,
        Self::NoApplicableInstaller,
        Self::ProhibitsElevation,
        Self::AppRunning,
        Self::DiskFull,
        Self::LowMemory,
        Self::MissingDependency,
        Self::Blocked,
        Self::NotSupported,
        Self::NewerInstalled,
        Self::CannotUpgrade,
        Self::Interrupted,
        Self::TimedOut,
        Self::Stopped,
        Self::ToolMissing,
        Self::InstallerError,
        Self::UninstallerError,
        Self::Crashed,
        Self::Unknown,
    ];

    pub fn message(self, name: &str) -> String {
        match self {
            Self::NotElevated => "Quantum Updater needs administrator rights for this. Restart it as administrator, then try again.".into(),
            Self::InstallerBusy => "Another installation is in progress. Try again in a few minutes.".into(),
            Self::RebootPending => format!("Windows needs a restart before {name} can be installed. Restart your PC, then try again."),
            Self::HashMismatch => format!("The download for {name} didn't match its expected checksum, so it wasn't installed."),
            Self::DownloadFailed => format!("Couldn't download {name}. Try again later."),
            Self::Offline => "There's no internet connection. Connect, then try again.".into(),
            Self::NotFound => format!("{name} isn't available right now."),
            Self::NoApplicableInstaller => format!("There's no installer for {name} that suits this PC."),
            Self::ProhibitsElevation => format!("{name}'s installer won't run with administrator rights."),
            Self::AppRunning => format!("{name} is in use. Close it, then try again."),
            Self::DiskFull => "There isn't enough space on your PC. Free some space, then try again.".into(),
            Self::LowMemory => "There isn't enough memory right now. Close other apps, then try again.".into(),
            Self::MissingDependency => format!("{name} needs another component that couldn't be installed."),
            Self::Blocked => "A policy on this PC blocks this installation.".into(),
            Self::NotSupported => format!("{name} isn't supported on this PC."),
            Self::NewerInstalled => format!("A newer version of {name} is already installed."),
            Self::CannotUpgrade => format!("{name} can't be updated in place. Uninstall it, then install the new version."),
            Self::Interrupted => format!("Quantum Updater closed while {name} was installing. Check that it works, or try again."),
            Self::TimedOut => format!("{name} took too long and was stopped. Try again later."),
            Self::Stopped => "Stopped.".into(),
            Self::ToolMissing => "The installer this app needs isn't set up on this PC. Open Settings to set it up.".into(),
            Self::InstallerError => format!("{name}'s installer reported an error."),
            Self::UninstallerError => format!("{name}'s uninstaller reported an error."),
            Self::Crashed => "Something went wrong while running this job.".into(),
            Self::Unknown => format!("Something went wrong with {name}. The details are in the activity log."),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn messages_never_name_a_package_tool() {
        for r in FailureReason::ALL {
            let m = r.message("Firefox").to_lowercase();
            for word in ["winget", "choco", "scoop", "msstore", "0x", "exit code"] {
                assert!(!m.contains(word), "{r:?}: {m}");
            }
            assert!(m.ends_with('.'), "{r:?}: {m}");
        }
    }

    #[test]
    fn messages_name_the_app_where_it_helps() {
        assert!(FailureReason::AppRunning
            .message("Firefox")
            .contains("Firefox"));
        assert!(FailureReason::NotElevated
            .message("Firefox")
            .contains("administrator"));
        assert!(FailureReason::RebootPending
            .message("Firefox")
            .contains("Restart"));
    }

    #[test]
    fn serializes_camel_case() {
        assert_eq!(
            serde_json::to_string(&FailureReason::HashMismatch).unwrap(),
            "\"hashMismatch\""
        );
    }
}
