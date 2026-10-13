//! Exit-code classification. winget codes are taken verbatim from Microsoft's returnCodes.md
//! (copy in tests/fixtures/winget/returnCodes.md) and classified by code only, because winget's
//! text is localized. Chocolatey prints English only, so its output text is matched too.
//! MSI codes apply to both (Chocolatey passes installer codes through).

use crate::ids::SourceKind;
use crate::jobs::failure::FailureReason;
use crate::sources::model::OpKind;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Outcome {
    Succeeded { reboot: bool },
    Skipped(String),
    Failed(FailureReason),
}

const REBOOT_MSI: [i32; 2] = [3010, 1641];

fn msi_reason(code: i32) -> Option<FailureReason> {
    Some(match code {
        1618 => FailureReason::InstallerBusy,
        1603 => FailureReason::InstallerError,
        1625 => FailureReason::Blocked,
        _ => return None,
    })
}

fn winget_reason(code: u32) -> FailureReason {
    use FailureReason as R;
    match code {
        0x8A150011 => R::HashMismatch,
        0x8A150008 | 0x8A15002E | 0x8A150086 => R::DownloadFailed,
        0x8A150107 | 0x8A150045 => R::Offline,
        0x8A150014 => R::NotFound,
        0x8A150010 => R::NoApplicableInstaller,
        0x8A150056 | 0x8A15007D => R::ProhibitsElevation,
        0x8A15010A => R::RebootPending,
        0x8A150102 => R::InstallerBusy,
        0x8A150101 | 0x8A150103 | 0x8A150111 => R::AppRunning,
        0x8A150105 => R::DiskFull,
        0x8A150106 => R::LowMemory,
        0x8A150104 | 0x8A150110 => R::MissingDependency,
        0x8A15010F | 0x8A15003A => R::Blocked,
        0x8A150113 => R::NotSupported,
        0x8A15010E => R::NewerInstalled,
        0x8A150114 => R::CannotUpgrade,
        0x8A150108 => R::InstallerError,
        0x8A150030 => R::UninstallerError,
        0x8A150019 => R::NotElevated,
        0x8A15010C => R::Stopped,
        _ => R::Unknown,
    }
}

fn choco_reason(code: i32, tail: &[String]) -> FailureReason {
    use FailureReason as R;
    if code == 350 || code == 1604 {
        return R::RebootPending;
    }
    let has = |needle: &str| tail.iter().any(|l| l.to_lowercase().contains(needle));
    if has("not running from an elevated") {
        R::NotElevated
    } else if has("pending system reboot") {
        R::RebootPending
    } else if has("checksum for") || has("checksum mismatch") {
        R::HashMismatch
    } else if has("not found with the source(s)") {
        R::NotFound
    } else if has("remote name could not be resolved") || has("unable to connect") {
        R::Offline
    } else if has("(404) not found") {
        R::DownloadFailed
    } else {
        R::Unknown
    }
}

/// `tail` is the job's last output lines (used for Chocolatey only).
pub fn classify(source: SourceKind, kind: OpKind, code: Option<i32>, tail: &[String]) -> Outcome {
    let Some(code) = code else {
        return Outcome::Failed(FailureReason::Stopped);
    };
    if code == 0 {
        return Outcome::Succeeded { reboot: false };
    }
    if REBOOT_MSI.contains(&code) {
        return Outcome::Succeeded { reboot: true };
    }
    if let Some(r) = msi_reason(code) {
        return Outcome::Failed(r);
    }
    match source {
        SourceKind::Winget | SourceKind::Msstore => match code as u32 {
            0x8A150109 | 0x8A15010B => Outcome::Succeeded { reboot: true },
            0x8A15002B | 0x8A15004F => Outcome::Skipped("Already up to date".into()),
            0x8A150061 | 0x8A15010D if kind == OpKind::Install => {
                Outcome::Skipped("Already installed".into())
            }
            c => Outcome::Failed(winget_reason(c)),
        },
        SourceKind::Choco => Outcome::Failed(choco_reason(code, tail)),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind::{Choco, Winget};
    use crate::sources::model::OpKind::{Install, Uninstall, Upgrade};
    use FailureReason as R;

    fn hx(c: u32) -> Option<i32> {
        Some(c as i32)
    }

    #[test]
    fn winget_success_and_reboot() {
        assert_eq!(
            classify(Winget, Install, Some(0), &[]),
            Outcome::Succeeded { reboot: false }
        );
        for code in [-1978334967, 3010, 1641] {
            assert_eq!(
                classify(Winget, Upgrade, Some(code), &[]),
                Outcome::Succeeded { reboot: true },
                "{code}"
            );
        }
    }

    #[test]
    fn winget_nothing_to_do_is_skipped_not_failed() {
        assert_eq!(
            classify(Winget, Upgrade, Some(-1978335189), &[]),
            Outcome::Skipped("Already up to date".into())
        );
        // Measured live: installing a Store app that's already there.
        assert_eq!(
            classify(Winget, Install, hx(0x8A15002B), &[]),
            Outcome::Skipped("Already up to date".into())
        );
        assert_eq!(
            classify(Winget, Install, Some(-1978335135), &[]),
            Outcome::Skipped("Already installed".into())
        );
        assert_eq!(
            classify(Winget, Install, Some(-1978334963), &[]),
            Outcome::Skipped("Already installed".into())
        );
    }

    #[test]
    fn winget_failures_map_to_reasons_by_code() {
        let table = [
            (0x8A150011, R::HashMismatch),
            (0x8A150008, R::DownloadFailed),
            (0x8A15002E, R::DownloadFailed),
            (0x8A150086, R::DownloadFailed),
            (0x8A150107, R::Offline),
            (0x8A150045, R::Offline),
            (0x8A150014, R::NotFound),
            (0x8A150010, R::NoApplicableInstaller),
            (0x8A150056, R::ProhibitsElevation),
            (0x8A15007D, R::ProhibitsElevation),
            (0x8A15010A, R::RebootPending),
            (0x8A150102, R::InstallerBusy),
            (0x8A150101, R::AppRunning),
            (0x8A150103, R::AppRunning),
            (0x8A150111, R::AppRunning),
            (0x8A150105, R::DiskFull),
            (0x8A150106, R::LowMemory),
            (0x8A150104, R::MissingDependency),
            (0x8A150110, R::MissingDependency),
            (0x8A15010F, R::Blocked),
            (0x8A15003A, R::Blocked),
            (0x8A150113, R::NotSupported),
            (0x8A15010E, R::NewerInstalled),
            (0x8A150114, R::CannotUpgrade),
            (0x8A150108, R::InstallerError),
            (0x8A150030, R::UninstallerError),
            (0x8A150019, R::NotElevated),
            (0x8A15010C, R::Stopped),
        ];
        for (code, want) in table {
            assert_eq!(
                classify(Winget, Install, hx(code), &[]),
                Outcome::Failed(want),
                "0x{code:08X}"
            );
        }
        // Text is ignored for winget (it's localized).
        let tail = ["checksum mismatch".to_string()];
        assert_eq!(
            classify(Winget, Install, Some(-123), &tail),
            Outcome::Failed(R::Unknown)
        );
    }

    #[test]
    fn msi_codes_apply_to_both_sources() {
        for src in [Winget, Choco] {
            assert_eq!(
                classify(src, Install, Some(1618), &[]),
                Outcome::Failed(R::InstallerBusy)
            );
            assert_eq!(
                classify(src, Install, Some(1603), &[]),
                Outcome::Failed(R::InstallerError)
            );
            assert_eq!(
                classify(src, Install, Some(1625), &[]),
                Outcome::Failed(R::Blocked)
            );
        }
    }

    #[test]
    fn choco_codes_and_text() {
        assert_eq!(
            classify(Choco, Install, Some(0), &[]),
            Outcome::Succeeded { reboot: false }
        );
        assert_eq!(
            classify(Choco, Upgrade, Some(3010), &[]),
            Outcome::Succeeded { reboot: true }
        );
        assert_eq!(
            classify(Choco, Uninstall, Some(1641), &[]),
            Outcome::Succeeded { reboot: true }
        );
        assert_eq!(
            classify(Choco, Install, Some(350), &[]),
            Outcome::Failed(R::RebootPending)
        );
        assert_eq!(
            classify(Choco, Install, Some(1604), &[]),
            Outcome::Failed(R::RebootPending)
        );
        let t = |lines: &[&str]| {
            let v: Vec<String> = lines.iter().map(|s| s.to_string()).collect();
            classify(Choco, Install, Some(1), &v)
        };
        assert_eq!(t(&["ERROR: Checksum for 'C:\\pkg\\x.exe' did not meet 'abc' for checksum type 'sha256'."]), Outcome::Failed(R::HashMismatch));
        assert_eq!(
            t(&["foo not installed. The package was not found with the source(s) listed."]),
            Outcome::Failed(R::NotFound)
        );
        assert_eq!(
            t(&["The remote name could not be resolved: 'community.chocolatey.org'"]),
            Outcome::Failed(R::Offline)
        );
        assert_eq!(
            t(&["Unable to connect to source 'https://community.chocolatey.org/api/v2/'"]),
            Outcome::Failed(R::Offline)
        );
        assert_eq!(
            t(&["ERROR: The remote server returned an error: (404) Not Found."]),
            Outcome::Failed(R::DownloadFailed)
        );
        assert_eq!(
            t(&["Chocolatey detected you are not running from an elevated command shell"]),
            Outcome::Failed(R::NotElevated)
        );
        assert_eq!(
            t(&["A pending system reboot request has been detected"]),
            Outcome::Failed(R::RebootPending)
        );
        assert_eq!(t(&["something else"]), Outcome::Failed(R::Unknown));
        assert_eq!(
            classify(Choco, Install, None, &[]),
            Outcome::Failed(R::Stopped)
        );
    }
}
