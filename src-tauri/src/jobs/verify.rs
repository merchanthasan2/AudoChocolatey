//! After a job reports success, check the app's installed version really moved. A mismatch is
//! a note, never a failure (browsers and self-updaters often finish after a restart), except
//! for going back to an earlier version, which counts only when that version is there.

use crate::sources::model::OpKind;
use crate::versions::compare;
use serde::{Deserialize, Serialize};
use std::cmp::Ordering;
use ts_rs::TS;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum Verify {
    /// The version moved as expected.
    Confirmed,
    /// Still reports the old version (or still installed after an uninstall).
    Unchanged,
    /// Couldn't tell; nothing is shown.
    Unknown,
}

fn usable(v: Option<&str>) -> Option<&str> {
    v.map(str::trim).filter(|v| {
        !v.is_empty()
            && !v.eq_ignore_ascii_case("unknown")
            && v.starts_with(|c: char| c.is_ascii_digit())
    })
}

/// `wanted` is the version a rollback asked for.
pub fn verdict(
    kind: OpKind,
    wanted: Option<&str>,
    before: Option<&str>,
    after: Option<&str>,
) -> Verify {
    match kind {
        OpKind::Rollback => match (usable(wanted), usable(after)) {
            (Some(w), Some(a)) if compare(a, w) == Ordering::Equal => Verify::Confirmed,
            (_, Some(a)) if usable(before).is_some_and(|b| compare(a, b) == Ordering::Equal) => {
                Verify::Unchanged
            }
            _ => Verify::Unknown,
        },
        OpKind::Install => match after {
            Some(_) => Verify::Confirmed,
            None => Verify::Unknown,
        },
        OpKind::Uninstall => match after {
            None => Verify::Confirmed,
            Some(_) => Verify::Unchanged,
        },
        OpKind::Upgrade => match (usable(before), usable(after)) {
            (Some(b), Some(a)) => match compare(a, b) {
                Ordering::Greater => Verify::Confirmed,
                Ordering::Equal => Verify::Unchanged,
                Ordering::Less => Verify::Unknown,
            },
            _ => Verify::Unknown,
        },
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sources::model::OpKind::{Install, Rollback, Uninstall, Upgrade};
    use Verify::*;

    #[test]
    fn verdicts() {
        assert_eq!(verdict(Upgrade, None, Some("1.0"), Some("1.1")), Confirmed);
        assert_eq!(
            verdict(Upgrade, None, Some("1.2"), Some("1.2.0.0")),
            Unchanged
        );
        assert_eq!(verdict(Upgrade, None, Some("1.1"), Some("1.0")), Unknown);
        assert_eq!(verdict(Upgrade, None, None, Some("1.1")), Unknown);
        assert_eq!(verdict(Upgrade, None, Some("1.0"), None), Unknown);
        assert_eq!(
            verdict(Upgrade, None, Some("Unknown"), Some("Unknown")),
            Unknown
        );
        assert_eq!(verdict(Upgrade, None, Some("< 2.0"), Some("2.1")), Unknown);
        assert_eq!(verdict(Install, None, None, Some("3.0")), Confirmed);
        assert_eq!(verdict(Install, None, None, None), Unknown);
        assert_eq!(verdict(Uninstall, None, Some("3.0"), None), Confirmed);
        assert_eq!(
            verdict(Uninstall, None, Some("3.0"), Some("3.0")),
            Unchanged
        );
    }

    #[test]
    fn going_back_is_confirmed_only_at_the_asked_version() {
        assert_eq!(
            verdict(Rollback, Some("1.0"), Some("1.1"), Some("1.0.0")),
            Confirmed
        );
        assert_eq!(
            verdict(Rollback, Some("1.0"), Some("1.1"), Some("1.1")),
            Unchanged
        );
        assert_eq!(
            verdict(Rollback, Some("1.0"), Some("1.1"), Some("1.2")),
            Unknown
        );
        assert_eq!(verdict(Rollback, Some("1.0"), Some("1.1"), None), Unknown);
        assert_eq!(verdict(Rollback, None, Some("1.1"), Some("1.0")), Unknown);
    }
}
