//! After a job reports success, check the app's installed version really moved. A mismatch is
//! a note, never a failure: browsers and self-updaters often finish after a restart.

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

pub fn verdict(kind: OpKind, before: Option<&str>, after: Option<&str>) -> Verify {
    match kind {
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
    use crate::sources::model::OpKind::{Install, Uninstall, Upgrade};
    use Verify::*;

    #[test]
    fn verdicts() {
        assert_eq!(verdict(Upgrade, Some("1.0"), Some("1.1")), Confirmed);
        assert_eq!(verdict(Upgrade, Some("1.2"), Some("1.2.0.0")), Unchanged);
        assert_eq!(verdict(Upgrade, Some("1.1"), Some("1.0")), Unknown);
        assert_eq!(verdict(Upgrade, None, Some("1.1")), Unknown);
        assert_eq!(verdict(Upgrade, Some("1.0"), None), Unknown);
        assert_eq!(verdict(Upgrade, Some("Unknown"), Some("Unknown")), Unknown);
        assert_eq!(verdict(Upgrade, Some("< 2.0"), Some("2.1")), Unknown);
        assert_eq!(verdict(Install, None, Some("3.0")), Confirmed);
        assert_eq!(verdict(Install, None, None), Unknown);
        assert_eq!(verdict(Uninstall, Some("3.0"), None), Confirmed);
        assert_eq!(verdict(Uninstall, Some("3.0"), Some("3.0")), Unchanged);
    }
}
