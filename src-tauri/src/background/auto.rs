//! Automatic updates: which shown updates may install by themselves, and when. Only updates
//! already on the Updates page qualify (never new apps), only with known versions, only for apps
//! whose choice is Automatic, and only while every condition can be read and allows it.

use crate::catalog::Catalog;
use crate::ids::SourceRef;
use crate::rules::{effective, rule_key, Policy, Rule};
use crate::settings::schema::{AutoRules, AutoUpdate, QuietHours};
use crate::sources::model::UpgradeCandidate;
use serde::Serialize;
use std::collections::{BTreeMap, BTreeSet};
use ts_rs::TS;

const DAY_MS: u64 = 24 * 3_600_000;

/// The PC right now. `None` means we couldn't tell, which holds automatic updates.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct Probe {
    pub elevated: bool,
    pub same_user: Option<bool>,
    pub on_battery: Option<bool>,
    pub metered: Option<bool>,
    pub minute_of_day: Option<u16>,
}

/// Why automatic updates are waiting.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum Hold {
    NeedsAdmin,
    OtherAccount,
    OnBattery,
    PowerUnknown,
    Metered,
    NetworkUnknown,
    QuietHours,
    TimeUnknown,
}

impl Hold {
    pub fn message(self) -> &'static str {
        match self {
            Self::NeedsAdmin => "Waiting: Quantum Updater isn't running with admin rights.",
            Self::OtherAccount => "Waiting: someone else is signed in at this PC.",
            Self::OnBattery => "Waiting: this PC is on battery power.",
            Self::PowerUnknown => "Waiting: we couldn't tell whether this PC is on battery.",
            Self::Metered => "Waiting: this internet connection is metered.",
            Self::NetworkUnknown => "Waiting: we couldn't check the internet connection.",
            Self::QuietHours => "Waiting: it's within your quiet hours.",
            Self::TimeUnknown => "Waiting: we couldn't read the time on this PC.",
        }
    }
}

pub fn in_quiet_hours(q: &QuietHours, minute: u16) -> bool {
    if !q.enabled || q.start == q.end {
        return false;
    }
    if q.start < q.end {
        (q.start..q.end).contains(&minute)
    } else {
        minute >= q.start || minute < q.end
    }
}

/// Whether automatic updates may start now. Anything we can't read holds.
pub fn may_auto_install(r: &AutoRules, p: &Probe) -> Result<(), Hold> {
    if !p.elevated {
        return Err(Hold::NeedsAdmin);
    }
    if p.same_user != Some(true) {
        return Err(Hold::OtherAccount);
    }
    if r.skip_on_battery {
        match p.on_battery {
            Some(true) => return Err(Hold::OnBattery),
            None => return Err(Hold::PowerUnknown),
            Some(false) => {}
        }
    }
    if r.skip_on_metered {
        match p.metered {
            Some(true) => return Err(Hold::Metered),
            None => return Err(Hold::NetworkUnknown),
            Some(false) => {}
        }
    }
    if r.quiet_hours.enabled {
        match p.minute_of_day {
            Some(m) if in_quiet_hours(&r.quiet_hours, m) => return Err(Hold::QuietHours),
            Some(_) => {}
            None => return Err(Hold::TimeUnknown),
        }
    }
    Ok(())
}

/// `<rule key>@<version>`: one app's one update, the same across sources.
pub fn update_key(c: &UpgradeCandidate, cat: &Catalog) -> Option<String> {
    let t = SourceRef::new(c.source, c.id.clone()).ok()?;
    Some(format!("{}@{}", rule_key(&t, cat), c.available_version))
}

fn known(v: &str) -> bool {
    let v = v.trim();
    !v.is_empty() && !v.eq_ignore_ascii_case("unknown") && !v.contains(['<', '>'])
}

/// Keeps when each shown update was first seen (for the waiting days). Gone updates are
/// forgotten; a date in the future (the clock went back) restarts from now.
pub fn remember_seen(first_seen: &mut BTreeMap<String, u64>, keys: &BTreeSet<String>, now: u64) {
    first_seen.retain(|k, _| keys.contains(k));
    for k in keys {
        let t = first_seen.entry(k.clone()).or_insert(now);
        if *t > now {
            *t = now;
        }
    }
}

pub fn old_enough(first_seen: u64, now: u64, days: u32) -> bool {
    now.saturating_sub(first_seen) >= u64::from(days) * DAY_MS
}

/// What the automatic run looks at, besides the shown updates.
pub struct PickInput<'a> {
    pub rules: &'a [Rule],
    pub mode: AutoUpdate,
    pub min_age_days: u32,
    pub first_seen: &'a BTreeMap<String, u64>,
    /// Updates already started automatically once; a failure isn't retried by itself.
    pub tried: &'a BTreeSet<String>,
    /// What the download-site look found, by update key.
    pub sites: &'a BTreeMap<String, crate::sites::Check>,
    pub now: u64,
}

/// Shown updates that may install by themselves: the app's choice is Automatic, both versions
/// are known, the version has waited long enough, it wasn't tried before, and (for the
/// Microsoft catalog) its download site was looked at and hasn't changed.
pub fn pick(items: &[UpgradeCandidate], i: &PickInput, cat: &Catalog) -> Vec<UpgradeCandidate> {
    items
        .iter()
        .filter(|c| known(&c.installed_version) && known(&c.available_version))
        .filter(|c| {
            let Some(key) = update_key(c, cat) else {
                return false;
            };
            let Some(rule_k) = key.rsplit_once('@').map(|(k, _)| k) else {
                return false;
            };
            let rule = i.rules.iter().find(|r| r.key == rule_k).map(|r| &r.kind);
            let site_ok = c.source != crate::ids::SourceKind::Winget
                || crate::sites::Check::lets_auto_run(i.sites.get(&key));
            effective(i.mode, rule) == Policy::Auto
                && site_ok
                && !i.tried.contains(&key)
                && i.first_seen
                    .get(&key)
                    .is_some_and(|t| old_enough(*t, i.now, i.min_age_days))
        })
        .cloned()
        .collect()
}

/// Reads power, connection cost and the local time. Blocking (WinRT); run off the async loop.
pub fn read_probe() -> Probe {
    Probe {
        elevated: crate::commands::system::is_elevated(),
        same_user: crate::commands::system::shell_user_matches(),
        on_battery: on_battery(),
        metered: metered(),
        minute_of_day: minute_of_day(),
    }
}

fn on_battery() -> Option<bool> {
    use windows_sys::Win32::System::Power::{GetSystemPowerStatus, SYSTEM_POWER_STATUS};
    // SAFETY: plain out-parameter struct.
    let mut s: SYSTEM_POWER_STATUS = unsafe { std::mem::zeroed() };
    // SAFETY: `s` is a valid, writable SYSTEM_POWER_STATUS.
    if unsafe { GetSystemPowerStatus(&mut s) } == 0 {
        return None;
    }
    if s.BatteryFlag == 128 {
        return Some(false); // no battery at all (a desktop)
    }
    match s.ACLineStatus {
        0 => Some(true),
        1 => Some(false),
        _ => None,
    }
}

fn metered() -> Option<bool> {
    use windows::Networking::Connectivity::{NetworkCostType, NetworkInformation};
    use windows::Win32::System::Com::{CoInitializeEx, CoUninitialize, COINIT_MULTITHREADED};
    // SAFETY: balanced with CoUninitialize below on this thread.
    let init = unsafe { CoInitializeEx(None, COINIT_MULTITHREADED) }.is_ok();
    let read = || -> Option<bool> {
        let cost = NetworkInformation::GetInternetConnectionProfile()
            .ok()?
            .GetConnectionCost()
            .ok()?;
        if cost.Roaming().unwrap_or(false) || cost.OverDataLimit().unwrap_or(false) {
            return Some(true);
        }
        match cost.NetworkCostType().ok()? {
            NetworkCostType::Unrestricted => Some(false),
            NetworkCostType::Fixed | NetworkCostType::Variable => Some(true),
            _ => None,
        }
    };
    let out = read();
    if init {
        // SAFETY: matches the successful CoInitializeEx above.
        unsafe { CoUninitialize() };
    }
    out
}

fn minute_of_day() -> Option<u16> {
    use windows_sys::Win32::Foundation::SYSTEMTIME;
    use windows_sys::Win32::System::SystemInformation::GetLocalTime;
    // SAFETY: plain out-parameter struct.
    let mut t: SYSTEMTIME = unsafe { std::mem::zeroed() };
    // SAFETY: `t` is a valid, writable SYSTEMTIME.
    unsafe { GetLocalTime(&mut t) };
    (t.wHour < 24 && t.wMinute < 60).then_some(t.wHour * 60 + t.wMinute)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind;
    use crate::rules::RuleKind;

    fn ok_probe() -> Probe {
        Probe {
            elevated: true,
            same_user: Some(true),
            on_battery: Some(false),
            metered: Some(false),
            minute_of_day: Some(12 * 60),
        }
    }

    #[test]
    fn quiet_hours_within_a_day_and_across_midnight() {
        let day = QuietHours {
            enabled: true,
            start: 9 * 60,
            end: 18 * 60,
        };
        assert!(in_quiet_hours(&day, 9 * 60));
        assert!(in_quiet_hours(&day, 17 * 60 + 59));
        assert!(!in_quiet_hours(&day, 18 * 60));
        assert!(!in_quiet_hours(&day, 8 * 60));
        let night = QuietHours {
            enabled: true,
            start: 22 * 60,
            end: 7 * 60,
        };
        assert!(in_quiet_hours(&night, 23 * 60));
        assert!(in_quiet_hours(&night, 0));
        assert!(in_quiet_hours(&night, 6 * 60 + 59));
        assert!(!in_quiet_hours(&night, 7 * 60));
        assert!(!in_quiet_hours(&night, 12 * 60));
        let off = QuietHours {
            enabled: false,
            ..night
        };
        assert!(!in_quiet_hours(&off, 23 * 60));
    }

    #[test]
    fn conditions_hold_and_unknown_means_hold() {
        let r = AutoRules::default();
        assert_eq!(may_auto_install(&r, &ok_probe()), Ok(()));
        let cases: &[(Probe, Hold)] = &[
            (
                Probe {
                    elevated: false,
                    ..ok_probe()
                },
                Hold::NeedsAdmin,
            ),
            (
                Probe {
                    same_user: Some(false),
                    ..ok_probe()
                },
                Hold::OtherAccount,
            ),
            (
                Probe {
                    same_user: None,
                    ..ok_probe()
                },
                Hold::OtherAccount,
            ),
            (
                Probe {
                    on_battery: Some(true),
                    ..ok_probe()
                },
                Hold::OnBattery,
            ),
            (
                Probe {
                    on_battery: None,
                    ..ok_probe()
                },
                Hold::PowerUnknown,
            ),
            (
                Probe {
                    metered: Some(true),
                    ..ok_probe()
                },
                Hold::Metered,
            ),
            (
                Probe {
                    metered: None,
                    ..ok_probe()
                },
                Hold::NetworkUnknown,
            ),
        ];
        for (p, want) in cases {
            assert_eq!(may_auto_install(&r, p), Err(*want), "{p:?}");
        }
        // Switched off, battery and metered don't matter.
        let relaxed = AutoRules {
            skip_on_battery: false,
            skip_on_metered: false,
            ..r
        };
        let p = Probe {
            on_battery: None,
            metered: Some(true),
            ..ok_probe()
        };
        assert_eq!(may_auto_install(&relaxed, &p), Ok(()));
        // Quiet hours, and an unreadable clock while they're on.
        let quiet = AutoRules {
            quiet_hours: QuietHours {
                enabled: true,
                start: 9 * 60,
                end: 18 * 60,
            },
            ..r
        };
        assert_eq!(may_auto_install(&quiet, &ok_probe()), Err(Hold::QuietHours));
        let p = Probe {
            minute_of_day: None,
            ..ok_probe()
        };
        assert_eq!(may_auto_install(&quiet, &p), Err(Hold::TimeUnknown));
        assert_eq!(
            may_auto_install(&r, &p),
            Ok(()),
            "no quiet hours, no clock needed"
        );
    }

    #[test]
    fn hold_messages_are_plain() {
        for h in [
            Hold::NeedsAdmin,
            Hold::OtherAccount,
            Hold::OnBattery,
            Hold::PowerUnknown,
            Hold::Metered,
            Hold::NetworkUnknown,
            Hold::QuietHours,
            Hold::TimeUnknown,
        ] {
            let m = h.message().to_ascii_lowercase();
            for word in ["winget", "choco", "msstore", "powershell"] {
                assert!(!m.contains(word), "{m}");
            }
        }
    }

    #[test]
    fn waiting_days_and_first_seen() {
        let now = 100 * DAY_MS;
        assert!(old_enough(now - 2 * DAY_MS, now, 2));
        assert!(!old_enough(now - 2 * DAY_MS + 1, now, 2));
        assert!(old_enough(now, now, 0));

        let mut seen = BTreeMap::from([
            ("gone@1".to_string(), 1),
            ("kept@1".to_string(), 5),
            ("future@1".to_string(), now + DAY_MS),
        ]);
        let keys: BTreeSet<String> = ["kept@1", "future@1", "new@1"]
            .iter()
            .map(|s| s.to_string())
            .collect();
        remember_seen(&mut seen, &keys, now);
        assert_eq!(
            seen,
            BTreeMap::from([
                ("kept@1".to_string(), 5),
                ("future@1".to_string(), now),
                ("new@1".to_string(), now),
            ])
        );
    }

    fn cand(id: &str, from: &str, to: &str) -> UpgradeCandidate {
        UpgradeCandidate {
            name: id.into(),
            id: id.into(),
            installed_version: from.into(),
            available_version: to.into(),
            source: SourceKind::Winget,
        }
    }

    fn rule(id: &str, kind: RuleKind) -> Rule {
        let target = SourceRef::new(SourceKind::Winget, id).unwrap();
        Rule {
            key: rule_key(&target, Catalog::embedded()),
            target,
            name: id.into(),
            kind,
            created_at: 1,
        }
    }

    #[test]
    fn only_automatic_known_old_enough_untried_updates_are_picked() {
        let cat = Catalog::embedded();
        let now = 100 * DAY_MS;
        let items = vec![
            cand("A.Auto", "1.0", "2.0"),
            cand("B.Ask", "1.0", "2.0"),
            cand("C.Default", "1.0", "2.0"),
            cand("D.Unknown", "Unknown", "2.0"),
            cand("E.New", "1.0", "2.0"),
            cand("F.Tried", "1.0", "2.0"),
            cand("G.Range", "< 1.0", "2.0"),
        ];
        let rules = vec![
            rule("A.Auto", RuleKind::Auto),
            rule("B.Ask", RuleKind::Ask),
            rule("D.Unknown", RuleKind::Auto),
            rule("E.New", RuleKind::Auto),
            rule("F.Tried", RuleKind::Auto),
            rule("G.Range", RuleKind::Auto),
        ];
        let mut first_seen: BTreeMap<String, u64> = items
            .iter()
            .map(|c| (update_key(c, cat).unwrap(), now - 3 * DAY_MS))
            .collect();
        first_seen.insert("winget:e.new@2.0".into(), now - DAY_MS);
        let tried = BTreeSet::from(["winget:f.tried@2.0".to_string()]);
        let sites: BTreeMap<String, crate::sites::Check> = items
            .iter()
            .map(|c| (update_key(c, cat).unwrap(), crate::sites::Check::Same))
            .collect();
        let input = PickInput {
            rules: &rules,
            mode: AutoUpdate::Notify,
            min_age_days: 2,
            first_seen: &first_seen,
            tried: &tried,
            sites: &sites,
            now,
        };
        let ids: Vec<String> = pick(&items, &input, cat)
            .into_iter()
            .map(|c| c.id)
            .collect();
        assert_eq!(ids, vec!["A.Auto"]);

        // Automatic in Settings: apps without a choice join; Ask first still stays out.
        let input = PickInput {
            mode: AutoUpdate::Automatic,
            ..input
        };
        let ids: Vec<String> = pick(&items, &input, cat)
            .into_iter()
            .map(|c| c.id)
            .collect();
        assert_eq!(ids, vec!["A.Auto", "C.Default"]);
    }

    #[test]
    fn a_changed_or_unchecked_download_site_holds_automatic_updates() {
        use crate::sites::Check;
        let cat = Catalog::embedded();
        let now = 100 * DAY_MS;
        let items = vec![
            cand("A.Same", "1.0", "2.0"),
            cand("B.NoUrl", "1.0", "2.0"),
            cand("C.Changed", "1.0", "2.0"),
            cand("D.Error", "1.0", "2.0"),
            cand("E.Pending", "1.0", "2.0"),
        ];
        let first_seen: BTreeMap<String, u64> = items
            .iter()
            .map(|c| (update_key(c, cat).unwrap(), 0))
            .collect();
        let sites = BTreeMap::from([
            ("winget:a.same@2.0".to_string(), Check::Same),
            ("winget:b.nourl@2.0".to_string(), Check::NoUrl),
            (
                "winget:c.changed@2.0".to_string(),
                Check::Changed {
                    site: "evil.com".into(),
                },
            ),
            ("winget:d.error@2.0".to_string(), Check::Error),
        ]);
        let input = PickInput {
            rules: &[],
            mode: AutoUpdate::Automatic,
            min_age_days: 0,
            first_seen: &first_seen,
            tried: &BTreeSet::new(),
            sites: &sites,
            now,
        };
        let ids: Vec<String> = pick(&items, &input, cat)
            .into_iter()
            .map(|c| c.id)
            .collect();
        assert_eq!(ids, vec!["A.Same", "B.NoUrl"]);
    }

    #[test]
    fn a_skipped_or_never_app_is_never_picked_even_if_it_slips_through() {
        let cat = Catalog::embedded();
        let now = 100 * DAY_MS;
        let items = vec![cand("A.A", "1.0", "2.0")];
        let first_seen = BTreeMap::from([("winget:a.a@2.0".to_string(), 0)]);
        let rules = vec![rule("A.A", RuleKind::Never)];
        let input = PickInput {
            rules: &rules,
            mode: AutoUpdate::Automatic,
            min_age_days: 0,
            first_seen: &first_seen,
            tried: &BTreeSet::new(),
            sites: &BTreeMap::new(),
            now,
        };
        assert!(pick(&items, &input, cat).is_empty());
    }
}
