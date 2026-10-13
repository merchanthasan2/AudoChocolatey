//! Human titles for Chocolatey package ids, looked up on the community feed. The lists that need
//! them (Installed, Updates) wait for this, so it must stay fast when the feed is slow or blocked.

use crate::ids::ChocoId;
use std::collections::HashMap;
use std::future::Future;
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

/// Longest a list waits for titles; ids still pending show as ids this time.
pub const DEADLINE: Duration = Duration::from_secs(3);
/// How long a failed or timed-out lookup is remembered before it's tried again.
pub const MISS_TTL: Duration = Duration::from_secs(10 * 60);

enum Entry {
    Found(String),
    Missed(Instant),
}

#[derive(Default)]
pub struct TitleCache(Mutex<HashMap<String, Entry>>);

impl TitleCache {
    fn lock(&self) -> std::sync::MutexGuard<'_, HashMap<String, Entry>> {
        self.0.lock().unwrap_or_else(|p| p.into_inner())
    }

    #[cfg(test)]
    fn age_misses(&self, by: Duration) {
        for e in self.lock().values_mut() {
            if let Entry::Missed(at) = e {
                *at -= by;
            }
        }
    }
}

/// Titles for `ids` (keyed by the id as given). Unknown ids are fetched 4 at a time; whatever
/// hasn't answered by `deadline` is treated as a miss.
pub async fn lookup<F, Fut>(
    ids: &[String],
    cache: &TitleCache,
    fetch: F,
    deadline: Duration,
) -> HashMap<String, String>
where
    F: Fn(ChocoId) -> Fut,
    Fut: Future<Output = Option<String>> + Send + 'static,
{
    let todo: Vec<ChocoId> = {
        let c = cache.lock();
        let mut seen = std::collections::HashSet::new();
        ids.iter()
            .filter(|id| match c.get(&id.to_ascii_lowercase()) {
                Some(Entry::Found(_)) => false,
                Some(Entry::Missed(at)) => at.elapsed() >= MISS_TTL,
                None => true,
            })
            .filter(|id| seen.insert(id.to_ascii_lowercase()))
            .filter_map(|id| ChocoId::try_from(id.clone()).ok())
            .collect()
    };
    if !todo.is_empty() {
        let gate = Arc::new(tokio::sync::Semaphore::new(4));
        let mut set = tokio::task::JoinSet::new();
        for id in &todo {
            let key = id.as_str().to_ascii_lowercase();
            let (job, gate) = (fetch(id.clone()), gate.clone());
            set.spawn(async move {
                let _permit = gate.acquire_owned().await.ok();
                (key, job.await)
            });
        }
        let mut answered: HashMap<String, Option<String>> = HashMap::new();
        let _ = tokio::time::timeout(deadline, async {
            while let Some(joined) = set.join_next().await {
                if let Ok((k, t)) = joined {
                    answered.insert(k, t);
                }
            }
        })
        .await;
        set.abort_all();
        let now = Instant::now();
        let mut c = cache.lock();
        for id in todo {
            let k = id.as_str().to_ascii_lowercase();
            let entry = match answered.remove(&k).flatten() {
                Some(t) => Entry::Found(t),
                None => Entry::Missed(now),
            };
            c.insert(k, entry);
        }
    }
    let c = cache.lock();
    ids.iter()
        .filter_map(|id| match c.get(&id.to_ascii_lowercase()) {
            Some(Entry::Found(t)) => Some((id.clone(), t.clone())),
            _ => None,
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicUsize, Ordering};
    use std::sync::Arc;

    fn ids(v: &[&str]) -> Vec<String> {
        v.iter().map(|s| s.to_string()).collect()
    }

    #[tokio::test]
    async fn found_titles_are_returned_and_cached() {
        let cache = TitleCache::default();
        let calls = Arc::new(AtomicUsize::new(0));
        let c = calls.clone();
        let fetch = move |id: ChocoId| {
            c.fetch_add(1, Ordering::SeqCst);
            async move { Some(format!("Title of {}", id.as_str())) }
        };
        let got = lookup(&ids(&["Git"]), &cache, fetch.clone(), DEADLINE).await;
        assert_eq!(got.get("Git").map(String::as_str), Some("Title of Git"));
        let again = lookup(&ids(&["git"]), &cache, fetch, DEADLINE).await;
        assert_eq!(again.get("git").map(String::as_str), Some("Title of Git"));
        assert_eq!(calls.load(Ordering::SeqCst), 1);
    }

    #[tokio::test]
    async fn failures_are_not_retried_on_every_refresh() {
        let cache = TitleCache::default();
        let calls = Arc::new(AtomicUsize::new(0));
        let c = calls.clone();
        let fetch = move |_id: ChocoId| {
            c.fetch_add(1, Ordering::SeqCst);
            async { None }
        };
        let list = ids(&["a-pkg", "b-pkg"]);
        assert!(lookup(&list, &cache, fetch.clone(), DEADLINE)
            .await
            .is_empty());
        assert!(lookup(&list, &cache, fetch, DEADLINE).await.is_empty());
        assert_eq!(
            calls.load(Ordering::SeqCst),
            2,
            "second refresh must not refetch"
        );
    }

    #[tokio::test]
    async fn a_blocked_feed_cannot_stall_the_list() {
        let cache = TitleCache::default();
        let hang = |_id: ChocoId| std::future::pending::<Option<String>>();
        let many: Vec<String> = (0..40).map(|i| format!("pkg{i}")).collect();
        let t = std::time::Instant::now();
        let got = lookup(&many, &cache, hang, Duration::from_millis(150)).await;
        assert!(got.is_empty());
        assert!(
            t.elapsed() < Duration::from_secs(1),
            "took {:?}",
            t.elapsed()
        );
        // The timed-out ids count as misses, so the next refresh is instant.
        let t = std::time::Instant::now();
        lookup(&many, &cache, hang, Duration::from_millis(150)).await;
        assert!(
            t.elapsed() < Duration::from_millis(100),
            "took {:?}",
            t.elapsed()
        );
    }

    #[tokio::test]
    async fn misses_expire() {
        let cache = TitleCache::default();
        lookup(
            &ids(&["x-pkg"]),
            &cache,
            |_id: ChocoId| async { None },
            DEADLINE,
        )
        .await;
        cache.age_misses(MISS_TTL + Duration::from_secs(1));
        let got = lookup(
            &ids(&["x-pkg"]),
            &cache,
            |_id: ChocoId| async { Some("X".to_string()) },
            DEADLINE,
        )
        .await;
        assert_eq!(got.get("x-pkg").map(String::as_str), Some("X"));
    }
}
