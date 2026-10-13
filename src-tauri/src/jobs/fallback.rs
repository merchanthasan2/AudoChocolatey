//! Backup sources: when a job fails before any installer ran, try the same app from another
//! source once. Alternates come only from the hand-checked catalog (never from the UI or a
//! name match), so a fallback never installs something the user didn't pick.

use crate::catalog::Catalog;
use crate::ids::{SourceKind, SourceRef};
use crate::jobs::failure::FailureReason;
use crate::sources::model::OpKind;

/// Which optional sources can be used right now.
#[derive(Debug, Clone, Copy)]
pub struct Enabled {
    pub choco: bool,
    pub store: bool,
}

impl Enabled {
    fn allows(self, s: SourceKind) -> bool {
        match s {
            SourceKind::Winget => true,
            SourceKind::Choco => self.choco,
            SourceKind::Msstore => self.store,
        }
    }
}

/// The same catalog app in its other sources, in install-priority order.
pub fn alternates(target: &SourceRef, cat: &Catalog) -> Vec<SourceRef> {
    let Some(app) = cat.by_source(target.source(), target.id()) else {
        return Vec::new();
    };
    [
        (SourceKind::Winget, &app.winget),
        (SourceKind::Choco, &app.choco),
        (SourceKind::Msstore, &app.msstore),
    ]
    .into_iter()
    .filter(|(s, _)| *s != target.source())
    .filter_map(|(s, id)| SourceRef::new(s, id.clone()?).ok())
    .collect()
}

/// A Chocolatey package may take over a winget app's update only if it's the regular
/// (installer) package and the catalog doesn't mark the pair as side-by-side.
pub fn upgrade_target_ok(choco_id: &str, no_upgrade_fallback: bool) -> bool {
    let id = choco_id.to_ascii_lowercase();
    !no_upgrade_fallback && !id.ends_with(".portable") && !id.ends_with(".commandline")
}

/// Where to retry, or None. Updates also need the alternate's latest version to be newer than
/// what's installed; the caller checks that (it needs the network).
pub fn candidate(
    kind: OpKind,
    target: &SourceRef,
    reason: FailureReason,
    reached_installing: bool,
    cat: &Catalog,
    on: Enabled,
) -> Option<SourceRef> {
    use FailureReason as R;
    if reached_installing {
        return None;
    }
    match kind {
        OpKind::Uninstall => None,
        OpKind::Install => {
            let fixable = matches!(
                reason,
                R::NotFound
                    | R::DownloadFailed
                    | R::HashMismatch
                    | R::NoApplicableInstaller
                    | R::ProhibitsElevation
            );
            if !fixable {
                return None;
            }
            alternates(target, cat)
                .into_iter()
                .find(|a| on.allows(a.source()))
        }
        OpKind::Upgrade => {
            let fixable = matches!(
                reason,
                R::NotFound | R::DownloadFailed | R::HashMismatch | R::NoApplicableInstaller
            );
            if !fixable || target.source() != SourceKind::Winget || !on.choco {
                return None;
            }
            let app = cat.by_source(target.source(), target.id())?;
            let alt = alternates(target, cat)
                .into_iter()
                .find(|a| a.source() == SourceKind::Choco)?;
            upgrade_target_ok(alt.id(), app.no_upgrade_fallback).then_some(alt)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::jobs::failure::FailureReason as R;
    use crate::sources::model::OpKind::{Install, Uninstall, Upgrade};

    fn r(source: SourceKind, id: &str) -> SourceRef {
        SourceRef::new(source, id).unwrap()
    }
    const ALL_ON: Enabled = Enabled {
        choco: true,
        store: true,
    };

    #[test]
    fn alternates_come_from_the_catalog_only() {
        let cat = Catalog::embedded();
        let spotify = alternates(&r(SourceKind::Winget, "Spotify.Spotify"), cat);
        assert_eq!(
            spotify,
            [
                r(SourceKind::Choco, "spotify"),
                r(SourceKind::Msstore, "9NCBCSZSJRSB")
            ]
        );
        assert!(alternates(&r(SourceKind::Winget, "Contoso.Widget"), cat).is_empty());
    }

    #[test]
    fn installs_fall_back_for_download_and_installer_problems() {
        let cat = Catalog::embedded();
        let w = r(SourceKind::Winget, "Spotify.Spotify");
        for reason in [
            R::NotFound,
            R::DownloadFailed,
            R::HashMismatch,
            R::NoApplicableInstaller,
            R::ProhibitsElevation,
        ] {
            assert_eq!(
                candidate(Install, &w, reason, false, cat, ALL_ON),
                Some(r(SourceKind::Choco, "spotify")),
                "{reason:?}"
            );
        }
        // Chocolatey off: the Store is next.
        let no_choco = Enabled {
            choco: false,
            store: true,
        };
        assert_eq!(
            candidate(Install, &w, R::HashMismatch, false, cat, no_choco),
            Some(r(SourceKind::Msstore, "9NCBCSZSJRSB"))
        );
        assert_eq!(
            candidate(
                Install,
                &w,
                R::HashMismatch,
                false,
                cat,
                Enabled {
                    choco: false,
                    store: false
                }
            ),
            None
        );
    }

    #[test]
    fn never_after_an_installer_ran_or_for_problems_another_source_cannot_fix() {
        let cat = Catalog::embedded();
        let w = r(SourceKind::Winget, "Spotify.Spotify");
        assert_eq!(
            candidate(Install, &w, R::DownloadFailed, true, cat, ALL_ON),
            None
        );
        for reason in [
            R::Stopped,
            R::InstallerBusy,
            R::RebootPending,
            R::NotElevated,
            R::Offline,
            R::AppRunning,
            R::DiskFull,
            R::LowMemory,
            R::Blocked,
            R::Interrupted,
            R::TimedOut,
            R::InstallerError,
            R::NewerInstalled,
            R::ToolMissing,
            R::Crashed,
            R::Unknown,
        ] {
            assert_eq!(
                candidate(Install, &w, reason, false, cat, ALL_ON),
                None,
                "{reason:?}"
            );
        }
        assert_eq!(
            candidate(Uninstall, &w, R::NotFound, false, cat, ALL_ON),
            None
        );
    }

    #[test]
    fn updates_only_move_from_winget_to_chocolatey() {
        let cat = Catalog::embedded();
        let w = r(SourceKind::Winget, "Spotify.Spotify");
        assert_eq!(
            candidate(Upgrade, &w, R::HashMismatch, false, cat, ALL_ON),
            Some(r(SourceKind::Choco, "spotify"))
        );
        // ProhibitsElevation would need the Store, which installs side by side: not for updates.
        assert_eq!(
            candidate(Upgrade, &w, R::ProhibitsElevation, false, cat, ALL_ON),
            None
        );
        assert_eq!(
            candidate(
                Upgrade,
                &w,
                R::HashMismatch,
                false,
                cat,
                Enabled {
                    choco: false,
                    store: true
                }
            ),
            None
        );
        let c = r(SourceKind::Choco, "spotify");
        assert_eq!(
            candidate(Upgrade, &c, R::HashMismatch, false, cat, ALL_ON),
            None,
            "never Chocolatey to winget"
        );
        let s = r(SourceKind::Msstore, "9NCBCSZSJRSB");
        assert_eq!(
            candidate(Upgrade, &s, R::HashMismatch, false, cat, ALL_ON),
            None
        );
    }

    #[test]
    fn portable_packages_never_take_over_an_update() {
        assert!(!upgrade_target_ok("git.portable", false));
        assert!(!upgrade_target_ok("7zip.commandline", false));
        assert!(!upgrade_target_ok("git", true), "catalog says no");
        assert!(upgrade_target_ok("git", false));
    }
}
