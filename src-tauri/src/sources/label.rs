//! Where an installed app comes from, as people see it (ADR 028): Microsoft catalog,
//! Community catalog, Microsoft Store, Installed directly, or Steam. Friendly names live in
//! the UI (`logic/sources.ts`); this only classifies.

use crate::ids::SourceKind;
use serde::Serialize;
use ts_rs::TS;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum SourceLabel {
    MicrosoftCatalog,
    CommunityCatalog,
    MicrosoftStore,
    InstalledDirectly,
    Steam,
}

/// A Programs-list id as winget prints it: `ARP\<Machine|User>\<X64|X86>\<key>`.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ArpId<'a> {
    pub machine: bool,
    pub x64: bool,
    pub key: &'a str,
}

pub fn parse_arp_id(id: &str) -> Option<ArpId<'_>> {
    let mut parts = id.splitn(4, '\\');
    if !parts.next()?.eq_ignore_ascii_case("ARP") {
        return None;
    }
    let scope = parts.next()?;
    let arch = parts.next()?;
    let key = parts.next()?;
    let machine = if scope.eq_ignore_ascii_case("Machine") {
        true
    } else if scope.eq_ignore_ascii_case("User") {
        false
    } else {
        return None;
    };
    let x64 = if arch.eq_ignore_ascii_case("X64") {
        true
    } else if arch.eq_ignore_ascii_case("X86") {
        false
    } else {
        return None;
    };
    (!key.is_empty()).then_some(ArpId { machine, x64, key })
}

fn is_steam_key(key: &str) -> bool {
    key.strip_prefix("Steam App ")
        .is_some_and(|n| (1..=10).contains(&n.len()) && n.bytes().all(|b| b.is_ascii_digit()))
}

pub fn classify(source: Option<SourceKind>, id: &str) -> SourceLabel {
    match source {
        Some(SourceKind::Winget) => SourceLabel::MicrosoftCatalog,
        Some(SourceKind::Choco) => SourceLabel::CommunityCatalog,
        Some(SourceKind::Msstore) => SourceLabel::MicrosoftStore,
        None if id.len() >= 5 && id[..5].eq_ignore_ascii_case("MSIX\\") => {
            SourceLabel::MicrosoftStore
        }
        None if parse_arp_id(id).is_some_and(|a| is_steam_key(a.key)) => SourceLabel::Steam,
        None => SourceLabel::InstalledDirectly,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use SourceLabel::*;

    #[test]
    fn labels() {
        assert_eq!(classify(Some(SourceKind::Winget), "A.B"), MicrosoftCatalog);
        assert_eq!(classify(Some(SourceKind::Choco), "a"), CommunityCatalog);
        assert_eq!(classify(Some(SourceKind::Msstore), "9N"), MicrosoftStore);
        assert_eq!(classify(None, "msix\\x_1"), MicrosoftStore);
        assert_eq!(classify(None, "ARP\\Machine\\X64\\Steam App 730"), Steam);
        assert_eq!(classify(None, "ARP\\User\\X64\\Steam App 1"), Steam);
        for id in [
            "ARP\\Machine\\X64\\Steam",
            "ARP\\Machine\\X64\\NotSteam App 730",
            "Steam App 730",
            "ARP\\Machine\\X64\\Steam App 12345678901",
            "ARP\\Machine\\X64\\{ABC}",
        ] {
            assert_eq!(classify(None, id), InstalledDirectly, "{id}");
        }
    }

    #[test]
    fn arp_ids() {
        assert_eq!(
            parse_arp_id("ARP\\Machine\\X86\\FileZilla Client"),
            Some(ArpId {
                machine: true,
                x64: false,
                key: "FileZilla Client"
            })
        );
        assert_eq!(parse_arp_id("ARP\\Other\\X64\\x"), None);
        assert_eq!(parse_arp_id("ARP\\Machine\\ARM\\x"), None);
        assert_eq!(parse_arp_id("ARP\\Machine\\X64\\"), None);
        assert_eq!(parse_arp_id("Mozilla.Firefox"), None);
    }

    #[test]
    fn fixture_counts() {
        let text = include_str!("../../tests/fixtures/winget/list.en.txt");
        let rows = crate::sources::winget::parse::parse_tables(text, true).concat();
        let labels: Vec<SourceLabel> = rows
            .iter()
            .map(|r| {
                classify(
                    crate::sources::winget::row_source(r.source.as_deref(), &r.id),
                    &r.id,
                )
            })
            .collect();
        let count = |l| labels.iter().filter(|x| **x == l).count();
        assert_eq!(count(MicrosoftCatalog), 96);
        assert_eq!(count(Steam), 1);
        assert!(count(MicrosoftStore) > 0);
    }
}
