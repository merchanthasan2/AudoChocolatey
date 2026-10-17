//! "What's new" links and release dates for shown updates. Looked up after each check (two at
//! a time, at most 40), kept in memory (6 hours when found, 1 hour when not), and only used
//! when the source's version matches the update's. The link itself never goes to the page:
//! the page gets its host and asks Rust to open it.

use crate::ids::{ChocoId, SourceKind, SourceRef, WingetId};
use crate::sources::model::UpgradeCandidate;
use crate::state::AppState;
use serde::Serialize;
use std::collections::HashMap;
use std::time::{Duration, Instant};
use ts_rs::TS;

const HIT: Duration = Duration::from_secs(6 * 3600);
const MISS: Duration = Duration::from_secs(3600);
const MAX_ITEMS: usize = 40;
const MAX_ENTRIES: usize = 300;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Meta {
    pub version: String,
    pub notes_url: Option<String>,
    pub released: Option<String>,
}

#[derive(Default)]
pub struct InfoCache {
    entries: HashMap<String, (Instant, Option<Meta>)>,
}

pub fn cache_key(source: SourceKind, id: &str) -> String {
    format!("{}:{}", source.as_str(), id.to_ascii_lowercase())
}

impl InfoCache {
    pub fn fresh(&self, key: &str, now: Instant) -> bool {
        self.entries
            .get(key)
            .is_some_and(|(at, m)| now.duration_since(*at) < if m.is_some() { HIT } else { MISS })
    }

    pub fn put(&mut self, key: String, meta: Option<Meta>, now: Instant) {
        if self.entries.len() >= MAX_ENTRIES && !self.entries.contains_key(&key) {
            if let Some(oldest) = self
                .entries
                .iter()
                .min_by_key(|(_, (at, _))| *at)
                .map(|(k, _)| k.clone())
            {
                self.entries.remove(&oldest);
            }
        }
        self.entries.insert(key, (now, meta));
    }

    /// The info for this exact version, if we have it.
    pub fn for_version(&self, source: SourceKind, id: &str, version: &str) -> Option<&Meta> {
        self.entries
            .get(&cache_key(source, id))
            .and_then(|(_, m)| m.as_ref())
            .filter(|m| m.version == version)
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct UpdateInfo {
    pub source: SourceKind,
    pub id: String,
    pub version: String,
    /// Where "What's new" leads ("www.mozilla.org"), shown next to the link.
    pub notes_host: Option<String>,
    pub released: Option<String>,
}

pub fn infos(cache: &InfoCache, items: &[UpgradeCandidate]) -> Vec<UpdateInfo> {
    items
        .iter()
        .filter_map(|c| {
            let m = cache.for_version(c.source, &c.id, &c.available_version)?;
            Some(UpdateInfo {
                source: c.source,
                id: c.id.clone(),
                version: c.available_version.clone(),
                notes_host: m
                    .notes_url
                    .as_deref()
                    .and_then(crate::safe_url::safe_url)
                    .and_then(|u| crate::safe_url::host_of(&u)),
                released: m.released.clone(),
            })
        })
        .collect()
}

async fn look_up(state: &AppState, c: &UpgradeCandidate) -> Option<Meta> {
    match c.source {
        SourceKind::Winget => {
            let id = WingetId::try_from(c.id.clone()).ok()?;
            let s = tokio::time::timeout(Duration::from_secs(30), state.winget.show(&id))
                .await
                .ok()?
                .ok()?;
            Some(Meta {
                version: s.version?,
                notes_url: s.release_notes_url,
                released: s.release_date,
            })
        }
        SourceKind::Choco => {
            let id = ChocoId::try_from(c.id.clone()).ok()?;
            let all = tokio::time::timeout(Duration::from_secs(20), state.choco.feed_meta(&id))
                .await
                .ok()?
                .ok()?;
            let m = all
                .into_iter()
                .find(|m| m.id.eq_ignore_ascii_case(&c.id) && m.version == c.available_version)?;
            Some(Meta {
                version: m.version,
                notes_url: m.notes_url,
                released: m.released,
            })
        }
        SourceKind::Msstore => None,
    }
}

/// Fills the cache for these updates (two at a time) and returns whether anything changed.
pub async fn refresh(state: &AppState, items: &[UpgradeCandidate]) -> bool {
    let now = Instant::now();
    let todo: Vec<&UpgradeCandidate> = {
        let cache = state.update_info.lock().unwrap_or_else(|p| p.into_inner());
        items
            .iter()
            .filter(|c| c.source != SourceKind::Msstore)
            .filter(|c| !cache.fresh(&cache_key(c.source, &c.id), now))
            .take(MAX_ITEMS)
            .collect()
    };
    if todo.is_empty() {
        return false;
    }
    let lane = |n: usize| {
        let mine: Vec<&&UpgradeCandidate> = todo.iter().skip(n).step_by(2).collect();
        async move {
            for c in mine {
                let meta = look_up(state, c).await;
                state
                    .update_info
                    .lock()
                    .unwrap_or_else(|p| p.into_inner())
                    .put(cache_key(c.source, &c.id), meta, Instant::now());
            }
        }
    };
    tokio::join!(lane(0), lane(1));
    true
}

/// The link to open for this update, re-checked here (never taken from the page).
pub fn notes_url(cache: &InfoCache, target: &SourceRef, version: &str) -> Option<String> {
    let m = cache.for_version(target.source(), target.id(), version)?;
    let u = crate::safe_url::safe_url(m.notes_url.as_deref()?)?;
    Some(u.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn cand(id: &str, v: &str) -> UpgradeCandidate {
        UpgradeCandidate {
            name: id.into(),
            id: id.into(),
            installed_version: "1".into(),
            available_version: v.into(),
            source: SourceKind::Winget,
        }
    }

    #[test]
    fn only_the_matching_version_is_used() {
        let mut c = InfoCache::default();
        let now = Instant::now();
        c.put(
            cache_key(SourceKind::Winget, "Mozilla.Firefox"),
            Some(Meta {
                version: "131.0".into(),
                notes_url: Some("https://www.mozilla.org/notes".into()),
                released: Some("2026-10-01".into()),
            }),
            now,
        );
        let got = infos(
            &c,
            &[
                cand("mozilla.firefox", "131.0"),
                cand("Mozilla.Firefox", "132.0"),
            ],
        );
        assert_eq!(got.len(), 1);
        assert_eq!(got[0].notes_host.as_deref(), Some("www.mozilla.org"));
        let t = SourceRef::new(SourceKind::Winget, "Mozilla.Firefox").unwrap();
        assert!(notes_url(&c, &t, "131.0").is_some());
        assert!(notes_url(&c, &t, "132.0").is_none(), "a different version");
    }

    #[test]
    fn a_forged_link_is_never_opened() {
        let mut c = InfoCache::default();
        c.put(
            cache_key(SourceKind::Winget, "A.B"),
            Some(Meta {
                version: "2".into(),
                notes_url: Some("file:///C:/Windows/System32/cmd.exe".into()),
                released: None,
            }),
            Instant::now(),
        );
        let t = SourceRef::new(SourceKind::Winget, "A.B").unwrap();
        assert!(notes_url(&c, &t, "2").is_none());
        assert_eq!(infos(&c, &[cand("A.B", "2")])[0].notes_host, None);
    }

    #[test]
    fn misses_are_kept_shorter_and_the_cache_is_capped() {
        let mut c = InfoCache::default();
        let t0 = Instant::now();
        c.put("x".into(), None, t0);
        assert!(c.fresh("x", t0 + Duration::from_secs(1800)));
        assert!(!c.fresh("x", t0 + Duration::from_secs(4000)));
        for i in 0..MAX_ENTRIES + 10 {
            c.put(format!("k{i}"), None, t0 + Duration::from_millis(i as u64));
        }
        assert!(c.entries.len() <= MAX_ENTRIES);
    }
}
