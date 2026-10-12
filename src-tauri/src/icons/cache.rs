//! Icon cache: PNGs on disk keyed by a hash of the icon key, remembered misses (7 days) and
//! one shared computation per key so concurrent rows never fetch the same icon twice.

use sha2::{Digest, Sha256};
use std::collections::HashMap;
use std::future::Future;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime};
use tokio::sync::OnceCell;

const MISS_TTL: Duration = Duration::from_secs(7 * 24 * 60 * 60);

type Cell = Arc<OnceCell<Option<Arc<[u8]>>>>;

pub struct IconCache {
    dir: PathBuf,
    inflight: Mutex<HashMap<String, Cell>>,
}

impl IconCache {
    pub fn new(dir: PathBuf) -> Self {
        Self {
            dir,
            inflight: Mutex::new(HashMap::new()),
        }
    }

    fn stem(&self, key: &str) -> PathBuf {
        let hash = Sha256::digest(key.as_bytes());
        let hex: String = hash.iter().map(|b| format!("{b:02x}")).collect();
        self.dir.join(hex)
    }

    pub fn png_path(&self, key: &str) -> PathBuf {
        self.stem(key).with_extension("png")
    }

    fn miss_path(&self, key: &str) -> PathBuf {
        self.stem(key).with_extension("miss")
    }

    fn fresh_miss(&self, key: &str) -> bool {
        std::fs::metadata(self.miss_path(key))
            .and_then(|m| m.modified())
            .ok()
            .and_then(|t| SystemTime::now().duration_since(t).ok())
            .is_some_and(|age| age < MISS_TTL)
    }

    pub async fn get_or_compute<F>(&self, key: &str, compute: F) -> Option<Arc<[u8]>>
    where
        F: Future<Output = Option<Vec<u8>>>,
    {
        if let Ok(bytes) = std::fs::read(self.png_path(key)) {
            return Some(Arc::from(bytes));
        }
        if self.fresh_miss(key) {
            return None;
        }
        let cell = self
            .inflight
            .lock()
            .unwrap_or_else(|p| p.into_inner())
            .entry(key.to_string())
            .or_default()
            .clone();
        cell.get_or_init(|| async {
            let result = compute.await;
            let _ = std::fs::create_dir_all(&self.dir);
            match &result {
                Some(png) => {
                    let tmp = self.png_path(key).with_extension("tmp");
                    if std::fs::write(&tmp, png).is_ok() {
                        let _ = std::fs::rename(&tmp, self.png_path(key));
                    }
                }
                None => {
                    let _ = std::fs::write(self.miss_path(key), b"");
                }
            }
            result.map(Arc::from)
        })
        .await
        .clone()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicUsize, Ordering};
    use std::sync::Arc;
    use std::time::Duration;

    fn counting(
        result: Option<Vec<u8>>,
        calls: Arc<AtomicUsize>,
    ) -> impl Fn() -> futures_like::Fut {
        move || {
            calls.fetch_add(1, Ordering::SeqCst);
            let r = result.clone();
            Box::pin(async move {
                tokio::time::sleep(Duration::from_millis(30)).await;
                r
            })
        }
    }

    mod futures_like {
        pub type Fut = std::pin::Pin<Box<dyn std::future::Future<Output = Option<Vec<u8>>> + Send>>;
    }

    #[tokio::test]
    async fn computes_once_then_serves_from_disk() {
        let dir = tempfile::tempdir().unwrap();
        let cache = IconCache::new(dir.path().to_path_buf());
        let calls = Arc::new(AtomicUsize::new(0));
        let f = counting(Some(vec![1, 2, 3]), calls.clone());
        assert_eq!(
            cache.get_or_compute("winget:Git.Git", f()).await.as_deref(),
            Some(&[1u8, 2, 3][..])
        );
        let fresh = IconCache::new(dir.path().to_path_buf());
        let f2 = counting(Some(vec![9]), calls.clone());
        assert_eq!(
            fresh
                .get_or_compute("winget:Git.Git", f2())
                .await
                .as_deref(),
            Some(&[1u8, 2, 3][..])
        );
        assert_eq!(
            calls.load(Ordering::SeqCst),
            2,
            "second compute future built but never awaited"
        );
    }

    #[tokio::test]
    async fn misses_are_remembered() {
        let dir = tempfile::tempdir().unwrap();
        let cache = IconCache::new(dir.path().to_path_buf());
        let calls = Arc::new(AtomicUsize::new(0));
        assert_eq!(
            cache
                .get_or_compute("choco:none", counting(None, calls.clone())())
                .await,
            None
        );
        let polled = Arc::new(AtomicUsize::new(0));
        let p2 = polled.clone();
        let again = Box::pin(async move {
            p2.fetch_add(1, Ordering::SeqCst);
            Some(vec![1])
        });
        assert_eq!(cache.get_or_compute("choco:none", again).await, None);
        assert_eq!(
            polled.load(Ordering::SeqCst),
            0,
            "a fresh miss must not recompute"
        );
    }

    #[tokio::test]
    async fn concurrent_requests_share_one_computation() {
        let dir = tempfile::tempdir().unwrap();
        let cache = Arc::new(IconCache::new(dir.path().to_path_buf()));
        let polled = Arc::new(AtomicUsize::new(0));
        let mut handles = Vec::new();
        for _ in 0..5 {
            let (c, p) = (cache.clone(), polled.clone());
            handles.push(tokio::spawn(async move {
                c.get_or_compute(
                    "winget:Same",
                    Box::pin(async move {
                        p.fetch_add(1, Ordering::SeqCst);
                        tokio::time::sleep(Duration::from_millis(50)).await;
                        Some(vec![7])
                    }),
                )
                .await
            }));
        }
        for h in handles {
            assert_eq!(h.await.unwrap().as_deref(), Some(&[7u8][..]));
        }
        assert_eq!(polled.load(Ordering::SeqCst), 1);
    }

    #[test]
    fn file_names_are_hashes_not_keys() {
        let cache = IconCache::new(std::env::temp_dir());
        let p = cache.png_path(r"local:ARP\Machine\X64\Android Studio");
        let name = p.file_name().unwrap().to_string_lossy().to_string();
        assert!(name.ends_with(".png") && name.len() == 64 + 4, "{name}");
    }
}
