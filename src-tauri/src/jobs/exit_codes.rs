//! Exit-code classification. winget codes are taken verbatim from Microsoft's returnCodes.md
//! (copy in tests/fixtures/winget/returnCodes.md); MSI reboot codes 3010/1641 apply to both.

use crate::ids::SourceKind;
use crate::sources::model::OpKind;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Outcome {
    Succeeded { reboot: bool },
    Skipped(String),
    Failed(String),
}

const REBOOT_MSI: [i32; 2] = [3010, 1641];

fn winget_message(code: i32) -> Option<&'static str> {
    Some(match code as u32 {
        0x8A150101 => "The app is currently running. Close it, then try again.",
        0x8A150102 => "Another installation is in progress. Try again later.",
        0x8A150103 => "One or more files are in use. Close the app, then try again.",
        0x8A150104 => "This package has a dependency missing from your system.",
        0x8A150105 => "There's no more space on your PC. Make space, then try again.",
        0x8A150106 => "There isn't enough memory to install. Close other apps, then try again.",
        0x8A150107 => "This app needs an internet connection. Connect, then try again.",
        0x8A150108 => "The installer reported an error. Contact the app's support.",
        0x8A15010A => "Installation failed. Restart your PC, then try again.",
        0x8A15010C => "The installation was cancelled.",
        0x8A15010E => "A newer version of this app is already installed.",
        0x8A15010F => "Your organisation's policy blocks this installation.",
        0x8A150110 => "Failed to install the package's dependencies.",
        0x8A150111 => "The app is in use by another application.",
        0x8A150113 => "This package isn't supported on your system.",
        0x8A150114 => "This installer can't upgrade the existing version.",
        0x8A150056 => "This installer can't run with administrator rights.",
        0x8A150030 => "The app's uninstaller reported an error.",
        0x8A150019 => "This needs administrator rights.",
        0x8A15003A => "This is blocked by Group Policy on this PC.",
        0x8A150045 => "Couldn't open the winget source. Check your connection.",
        _ => return None,
    })
}

pub fn classify(source: SourceKind, kind: OpKind, code: Option<i32>) -> Outcome {
    let Some(code) = code else {
        return Outcome::Failed("Stopped".into());
    };
    if code == 0 {
        return Outcome::Succeeded { reboot: false };
    }
    if REBOOT_MSI.contains(&code) {
        return Outcome::Succeeded { reboot: true };
    }
    match source {
        SourceKind::Winget => match code as u32 {
            0x8A150109 | 0x8A15010B => Outcome::Succeeded { reboot: true },
            0x8A15002B | 0x8A15004F => Outcome::Skipped("No applicable update".into()),
            0x8A150061 | 0x8A15010D if kind == OpKind::Install => {
                Outcome::Skipped("Already installed".into())
            }
            c => Outcome::Failed(
                winget_message(code)
                    .map(String::from)
                    .unwrap_or_else(|| format!("winget failed with code 0x{c:08X}")),
            ),
        },
        SourceKind::Choco => Outcome::Failed("Chocolatey reported an error".into()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind::{Choco, Winget};
    use crate::sources::model::OpKind::{Install, Uninstall, Upgrade};

    #[test]
    fn winget_success_and_reboot() {
        assert_eq!(
            classify(Winget, Install, Some(0)),
            Outcome::Succeeded { reboot: false }
        );
        for code in [-1978334967, 3010, 1641] {
            assert_eq!(
                classify(Winget, Upgrade, Some(code)),
                Outcome::Succeeded { reboot: true },
                "{code}"
            );
        }
    }

    #[test]
    fn winget_nothing_to_do_is_skipped_not_failed() {
        assert_eq!(
            classify(Winget, Upgrade, Some(-1978335189)),
            Outcome::Skipped("No applicable update".into())
        );
        assert_eq!(
            classify(Winget, Install, Some(-1978335135)),
            Outcome::Skipped("Already installed".into())
        );
        assert_eq!(
            classify(Winget, Install, Some(-1978334963)),
            Outcome::Skipped("Already installed".into())
        );
    }

    #[test]
    fn winget_failures_have_human_messages() {
        let msg = |c| match classify(Winget, Install, Some(c)) {
            Outcome::Failed(m) => m,
            other => panic!("{other:?}"),
        };
        assert!(msg(-1978334975).contains("currently running"));
        assert!(msg(-1978334971).contains("no more space"));
        assert!(msg(-1978334966).contains("Restart your PC"));
        assert!(msg(-123).contains("0xFFFFFF85"), "{}", msg(-123));
        assert!(
            msg(-1978335184).contains("uninstaller"),
            "{}",
            msg(-1978335184)
        );
        assert!(msg(-1978335207).contains("administrator"));
        assert!(msg(-1978335174).contains("Group Policy"));
    }

    #[test]
    fn choco_codes() {
        assert_eq!(
            classify(Choco, Install, Some(0)),
            Outcome::Succeeded { reboot: false }
        );
        assert_eq!(
            classify(Choco, Upgrade, Some(3010)),
            Outcome::Succeeded { reboot: true }
        );
        assert_eq!(
            classify(Choco, Uninstall, Some(1641)),
            Outcome::Succeeded { reboot: true }
        );
        assert_eq!(
            classify(Choco, Install, Some(1)),
            Outcome::Failed("Chocolatey reported an error".into())
        );
        assert_eq!(
            classify(Choco, Install, None),
            Outcome::Failed("Stopped".into())
        );
    }
}
