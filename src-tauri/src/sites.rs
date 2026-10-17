//! "Downloads from a different website than last time". For each app we remember which site
//! its installer came from; when a new version's installer comes from a different site, the
//! Updates row says so and automatic updates wait for it. Kept in `hosts.json` (data folder):
//! a forged file can only hide this note or hold an automatic update, never run anything.

use crate::fsutil::{read_json, set_aside, write_atomic, Read};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use std::path::PathBuf;

const SCHEMA_VERSION: u32 = 1;
const MAX_BYTES: u64 = 512 * 1024;
const MAX_ENTRIES: usize = 2000;

/// Shared hosts where the owner is the first path segment.
const BY_OWNER: [&str; 5] = [
    "github.com",
    "gitlab.com",
    "bitbucket.org",
    "storage.googleapis.com",
    "s3.amazonaws.com",
];
/// Shared hosts where each customer has their own subdomain: the whole host is the site.
const BY_HOST: [&str; 8] = [
    ".s3.amazonaws.com",
    ".blob.core.windows.net",
    ".cloudfront.net",
    ".azureedge.net",
    ".github.io",
    ".pages.dev",
    ".netlify.app",
    ".r2.dev",
];
const SECOND_LEVEL: [&str; 10] = [
    "com", "co", "net", "org", "gov", "edu", "ac", "or", "ne", "go",
];

/// Which site an installer URL belongs to: the registrable domain, plus the owner on shared
/// hosting. `None` when it isn't a web address.
pub fn site_of(url: &str) -> Option<String> {
    let u = url::Url::parse(url).ok()?;
    if !matches!(u.scheme(), "https" | "http") {
        return None;
    }
    let host = match u.host()? {
        url::Host::Domain(d) => d.trim_end_matches('.').to_ascii_lowercase(),
        url::Host::Ipv4(ip) => return Some(ip.to_string()),
        url::Host::Ipv6(ip) => return Some(format!("[{ip}]")),
    };
    let first_segment = || {
        u.path_segments()
            .and_then(|mut s| s.next())
            .filter(|s| !s.is_empty())
            .map(|s| s.to_ascii_lowercase())
    };
    if BY_OWNER.contains(&host.as_str()) {
        return Some(match first_segment() {
            Some(owner) => format!("{host}/{owner}"),
            None => host,
        });
    }
    if host == "sourceforge.net" || host.ends_with(".sourceforge.net") {
        let mut segs = u.path_segments()?;
        if segs.next() == Some("projects") {
            if let Some(p) = segs.next().filter(|p| !p.is_empty()) {
                return Some(format!("sourceforge.net/{}", p.to_ascii_lowercase()));
            }
        }
        return Some("sourceforge.net".into());
    }
    if BY_HOST.iter().any(|s| host.ends_with(s)) {
        return Some(host);
    }
    let labels: Vec<&str> = host.split('.').collect();
    let n = labels.len();
    if n <= 2 {
        return Some(host);
    }
    let tld = labels[n - 1];
    let take = if tld.len() == 2 && SECOND_LEVEL.contains(&labels[n - 2]) {
        3
    } else {
        2
    };
    Some(labels[n - take.min(n)..].join("."))
}

/// What the look at one update found.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", tag = "kind")]
pub enum Check {
    Same,
    /// The installer comes from another site than the app's usual one.
    Changed {
        site: String,
    },
    /// No installer address to compare (other language, or none given): can't tell.
    NoUrl,
    Error,
}

impl Check {
    /// Automatic updates go ahead only after a look that found nothing new.
    pub fn lets_auto_run(c: Option<&Check>) -> bool {
        matches!(c, Some(Check::Same | Check::NoUrl))
    }
}

/// Compares an update's site with the app's usual one. Returns the result and, when the app
/// had none yet, the site to remember.
pub fn judge(baseline: Option<&str>, site: Option<&str>) -> (Check, Option<String>) {
    match (baseline, site) {
        (_, None) => (Check::NoUrl, None),
        (None, Some(s)) => (Check::Same, Some(s.to_string())),
        (Some(b), Some(s)) if b == s => (Check::Same, None),
        (Some(_), Some(s)) => (
            Check::Changed {
                site: s.to_string(),
            },
            None,
        ),
    }
}

fn valid_site(s: &str) -> bool {
    !s.is_empty()
        && s.len() <= 300
        && s.bytes()
            .all(|b| b.is_ascii_lowercase() || b.is_ascii_digit() || b"-./:[]".contains(&b))
}

#[derive(Debug, Default, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", default)]
struct File {
    schema_version: u32,
    /// Rule key -> the site its installers usually come from.
    baseline: BTreeMap<String, String>,
    /// Update key (`rule key@version`) -> what the look found.
    checked: BTreeMap<String, Check>,
}

pub struct SiteStore {
    path: Option<PathBuf>,
    f: File,
}

impl SiteStore {
    pub fn open(path: PathBuf) -> Self {
        let f = match read_json(&path, MAX_BYTES) {
            Read::Json(v) => match serde_json::from_value::<File>(v) {
                Ok(f) if f.schema_version <= SCHEMA_VERSION => f,
                Ok(_) => File::default(),
                Err(_) => {
                    set_aside(&path);
                    File::default()
                }
            },
            Read::Corrupt => {
                set_aside(&path);
                File::default()
            }
            _ => File::default(),
        };
        let mut s = Self {
            path: Some(path),
            f,
        };
        s.clean();
        s
    }

    #[cfg(test)]
    pub fn memory() -> Self {
        Self {
            path: None,
            f: File::default(),
        }
    }

    fn clean(&mut self) {
        self.f
            .baseline
            .retain(|k, v| k.len() <= 300 && valid_site(v));
        self.f.checked.retain(|k, c| {
            k.len() <= 300
                && match c {
                    Check::Changed { site } => valid_site(site),
                    _ => true,
                }
        });
        while self.f.baseline.len() > MAX_ENTRIES {
            self.f.baseline.pop_first();
        }
        while self.f.checked.len() > MAX_ENTRIES {
            self.f.checked.pop_first();
        }
    }

    pub fn baseline(&self, rule_key: &str) -> Option<&str> {
        self.f.baseline.get(rule_key).map(String::as_str)
    }

    pub fn checked(&self, update_key: &str) -> Option<&Check> {
        self.f.checked.get(update_key)
    }

    pub fn all_checked(&self) -> &BTreeMap<String, Check> {
        &self.f.checked
    }

    /// Records a look at one update (and the app's first site, if it had none).
    pub fn record(&mut self, rule_key: &str, update_key: &str, found: Check, seed: Option<String>) {
        if let Some(s) = seed.filter(|s| valid_site(s)) {
            self.f.baseline.entry(rule_key.to_string()).or_insert(s);
        }
        self.f.checked.insert(update_key.to_string(), found);
        self.clean();
    }

    /// The person updated or installed the app through its own source: that site is now its
    /// usual one, and earlier looks are forgotten so the note clears.
    pub fn installed_from(&mut self, rule_key: &str, site: &str) {
        if !valid_site(site) {
            return;
        }
        self.f
            .baseline
            .insert(rule_key.to_string(), site.to_string());
        let prefix = format!("{rule_key}@");
        self.f.checked.retain(|k, _| !k.starts_with(&prefix));
        self.clean();
    }

    /// Forgets looks at updates that are no longer offered.
    pub fn prune(&mut self, live: &BTreeSet<String>) {
        self.f.checked.retain(|k, _| live.contains(k));
    }

    pub fn save(&self) {
        if let Some(p) = &self.path {
            let f = File {
                schema_version: SCHEMA_VERSION,
                ..self.f.clone()
            };
            if let Ok(bytes) = serde_json::to_vec(&f) {
                let _ = write_atomic(p, &bytes);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn site_keys() {
        let cases = [
            (
                "https://download-installer.cdn.mozilla.net/pub/firefox/x.exe",
                "mozilla.net",
            ),
            (
                "https://github.com/vendor/app/releases/download/v1/x.exe",
                "github.com/vendor",
            ),
            (
                "https://github.com/attacker/app/releases/download/v1/x.exe",
                "github.com/attacker",
            ),
            ("https://good.com@evil.com/x.exe", "evil.com"),
            ("https://a.b.example.com.cn/x.exe", "example.com.cn"),
            ("https://dl.foo.co.uk/x.exe", "foo.co.uk"),
            ("https://cdn.example.io/x.exe", "example.io"),
            (
                "https://mybucket.s3.amazonaws.com/x.exe",
                "mybucket.s3.amazonaws.com",
            ),
            (
                "https://s3.amazonaws.com/mybucket/x.exe",
                "s3.amazonaws.com/mybucket",
            ),
            (
                "https://downloads.sourceforge.net/projects/sevenzip/x.exe",
                "sourceforge.net/sevenzip",
            ),
            ("https://203.0.113.9/x.exe", "203.0.113.9"),
            ("https://[::1]/x.exe", "[::1]"),
            ("https://DL.EXAMPLE.com./x.exe", "example.com"),
            (
                "https://xn--bcher-kva.example/x.exe",
                "xn--bcher-kva.example",
            ),
        ];
        for (url, want) in cases {
            assert_eq!(site_of(url).as_deref(), Some(want), "{url}");
        }
        assert_eq!(site_of("file:///C:/x.exe"), None);
        assert_eq!(site_of("not a url"), None);
    }

    #[test]
    fn same_site_or_new_subdomain_is_fine_a_new_site_is_flagged() {
        let a = site_of("https://download.mozilla.org/x").unwrap();
        let b = site_of("https://cdn.download.mozilla.org/y").unwrap();
        assert_eq!(judge(Some(&a), Some(&b)), (Check::Same, None));
        let evil = site_of("https://mozilla-download.example/x").unwrap();
        assert!(matches!(
            judge(Some(&a), Some(&evil)).0,
            Check::Changed { .. }
        ));
        assert_eq!(
            judge(None, Some(&a)),
            (Check::Same, Some(a.clone())),
            "first look seeds"
        );
        assert_eq!(judge(Some(&a), None), (Check::NoUrl, None));
    }

    #[test]
    fn a_changed_site_keeps_the_old_baseline_until_the_person_updates() {
        let mut s = SiteStore::memory();
        s.record("app:x", "app:x@1", Check::Same, Some("x.com".into()));
        s.record(
            "app:x",
            "app:x@2",
            Check::Changed {
                site: "evil.com".into(),
            },
            Some("evil.com".into()),
        );
        assert_eq!(
            s.baseline("app:x"),
            Some("x.com"),
            "never replaced by a seed"
        );
        assert!(!Check::lets_auto_run(s.checked("app:x@2")));
        s.installed_from("app:x", "evil.com");
        assert_eq!(s.baseline("app:x"), Some("evil.com"));
        assert_eq!(s.checked("app:x@2"), None, "the note clears");
    }

    #[test]
    fn automatic_updates_wait_for_a_look() {
        assert!(!Check::lets_auto_run(None));
        assert!(!Check::lets_auto_run(Some(&Check::Error)));
        assert!(Check::lets_auto_run(Some(&Check::Same)));
        assert!(Check::lets_auto_run(Some(&Check::NoUrl)));
    }

    #[test]
    fn forged_files_are_cleaned() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("hosts.json");
        std::fs::write(
            &p,
            r#"{"schemaVersion":1,"baseline":{"app:a":"ok.com","app:b":"BAD HOST <x>"},"checked":{"app:a@1":{"kind":"changed","site":"EVIL"}}}"#,
        )
        .unwrap();
        let s = SiteStore::open(p);
        assert_eq!(s.baseline("app:a"), Some("ok.com"));
        assert_eq!(s.baseline("app:b"), None);
        assert_eq!(s.checked("app:a@1"), None);
    }
}
