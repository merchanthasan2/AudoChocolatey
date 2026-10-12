//! Real, original app icons. Resolution order: the installed program itself (registry icon,
//! main exe, MSIX logo), then the Chocolatey package icon, then the publisher's homepage icon.
//! Everything is re-encoded to PNG, cached on disk, and served over the `icon` URI scheme.

pub mod cache;
pub mod image;
pub mod sources;
pub mod win;

use crate::ids::{ChocoId, WingetId};
use crate::sources::process::{CommandSpec, Runner};
use cache::IconCache;
use std::collections::HashMap;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};
use tokio::sync::Semaphore;
use win::ArpEntry;

const ICON_SIZE: u32 = 128;
const ICON_CAP: usize = 1024 * 1024;
const HTML_CAP: usize = 512 * 1024;
const ARP_TTL: Duration = Duration::from_secs(10 * 60);

/// `/winget%3AGit.Git` + `name=Git` -> ("winget:Git.Git", Some("Git")).
pub fn parse_request(path: &str, query: Option<&str>) -> Option<(String, Option<String>)> {
    let key = percent_decode(path.trim_start_matches('/'))?;
    if key.is_empty() || key.len() > 300 || !key.contains(':') {
        return None;
    }
    let name = query.and_then(|q| {
        q.split('&')
            .filter_map(|kv| kv.split_once('='))
            .find(|(k, _)| *k == "name")
            .and_then(|(_, v)| percent_decode(&v.replace('+', " ")))
    });
    Some((key, name.filter(|n| !n.trim().is_empty() && n.len() <= 300)))
}

fn percent_decode(s: &str) -> Option<String> {
    let b = s.as_bytes();
    let mut out = Vec::with_capacity(b.len());
    let mut i = 0;
    while i < b.len() {
        if b[i] == b'%' {
            let hex = std::str::from_utf8(b.get(i + 1..i + 3)?).ok()?;
            out.push(u8::from_str_radix(hex, 16).ok()?);
            i += 3;
        } else {
            out.push(b[i]);
            i += 1;
        }
    }
    String::from_utf8(out).ok()
}

pub struct IconService {
    cache: IconCache,
    http: reqwest::Client,
    runner: Arc<dyn Runner>,
    winget: Option<PathBuf>,
    program_files: Option<PathBuf>,
    choco_urls: Mutex<HashMap<String, String>>,
    arp: Mutex<Option<(Instant, Arc<Vec<ArpEntry>>)>>,
    remote: Semaphore,
    shows: Semaphore,
}

impl IconService {
    pub fn new(dir: PathBuf, runner: Arc<dyn Runner>, winget: Option<PathBuf>) -> Self {
        let http = reqwest::Client::builder()
            .https_only(true)
            .timeout(Duration::from_secs(6))
            .redirect(reqwest::redirect::Policy::limited(3))
            .user_agent(concat!(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) QuantumUpdater/",
                env!("CARGO_PKG_VERSION")
            ))
            .build()
            .expect("static reqwest config");
        Self {
            cache: IconCache::new(dir),
            http,
            runner,
            winget,
            program_files: crate::sources::trust::program_files_dir(),
            choco_urls: Mutex::new(HashMap::new()),
            arp: Mutex::new(None),
            remote: Semaphore::new(4),
            shows: Semaphore::new(2),
        }
    }

    /// Chocolatey search results carry their package icon URL; remember it for later.
    pub fn remember_choco(&self, id: &str, url: &str) {
        if url.starts_with("https://") {
            self.choco_urls
                .lock()
                .unwrap_or_else(|p| p.into_inner())
                .insert(id.to_ascii_lowercase(), url.to_string());
        }
    }

    pub async fn resolve(&self, key: &str, name: Option<&str>) -> Option<Arc<[u8]>> {
        self.cache
            .get_or_compute(key, self.compute(key, name))
            .await
    }

    async fn compute(&self, key: &str, name: Option<&str>) -> Option<Vec<u8>> {
        let (source, id) = key.split_once(':')?;
        if let Some(png) = self.installed_icon(id, name).await {
            return Some(png);
        }
        match source {
            "choco" => self.choco_icon(id).await,
            "winget" => self.homepage_icon(id).await,
            _ => None,
        }
    }

    async fn arp_entries(&self) -> Arc<Vec<ArpEntry>> {
        if let Some((at, list)) = self.arp.lock().unwrap_or_else(|p| p.into_inner()).as_ref() {
            if at.elapsed() < ARP_TTL {
                return list.clone();
            }
        }
        let list = Arc::new(
            tokio::task::spawn_blocking(win::arp_entries)
                .await
                .unwrap_or_default(),
        );
        *self.arp.lock().unwrap_or_else(|p| p.into_inner()) = Some((Instant::now(), list.clone()));
        list
    }

    /// The program's own icon, if it's installed on this PC.
    async fn installed_icon(&self, id: &str, name: Option<&str>) -> Option<Vec<u8>> {
        if let Some(full) = id.strip_prefix(r"MSIX\") {
            let (full, pf) = (full.to_string(), self.program_files.clone()?);
            return tokio::task::spawn_blocking(move || win::msix_icon(&full, &pf, ICON_SIZE))
                .await
                .ok()?;
        }
        let name = name?.to_string();
        let entries = self.arp_entries().await;
        let entry = entries
            .iter()
            .find(|e| sources::name_matches(&e.name, &name))?
            .clone();
        tokio::task::spawn_blocking(move || win::icon_for_entry(&entry, ICON_SIZE))
            .await
            .ok()?
    }

    async fn choco_icon(&self, id: &str) -> Option<Vec<u8>> {
        let known = self
            .choco_urls
            .lock()
            .unwrap_or_else(|p| p.into_inner())
            .get(&id.to_ascii_lowercase())
            .cloned();
        let url = match known {
            Some(u) => u,
            None => {
                let cid = ChocoId::try_from(id.to_string()).ok()?;
                let xml = self
                    .fetch(&crate::sources::choco::odata::by_id_url(&cid), HTML_CAP)
                    .await?;
                let pkgs = crate::sources::choco::odata::parse_feed(&String::from_utf8_lossy(&xml))
                    .ok()?;
                pkgs.into_iter().find_map(|p| p.icon_url)?
            }
        };
        self.fetch_icon(&url).await
    }

    /// Publisher homepage (from `winget show`), then its declared icon or /favicon.ico.
    async fn homepage_icon(&self, id: &str) -> Option<Vec<u8>> {
        let wid = WingetId::try_from(id.to_string()).ok()?;
        let homepage = {
            let _permit = self.shows.acquire().await.ok()?;
            let args = [
                "show",
                "--id",
                wid.as_str(),
                "--exact",
                "--source",
                "winget",
                "--accept-source-agreements",
                "--disable-interactivity",
            ]
            .map(String::from)
            .to_vec();
            let spec = CommandSpec {
                exe: self.winget.clone()?,
                args,
                timeout: Duration::from_secs(30),
            };
            let out = self.runner.run(&spec).await.ok()?;
            crate::sources::winget::details::parse_show(&out.stdout).homepage?
        };
        if let Some(html) = self.fetch(&homepage, HTML_CAP).await {
            if let Some(link) = sources::pick_icon_link(&String::from_utf8_lossy(&html), &homepage)
            {
                if let Some(png) = self.fetch_icon(&link).await {
                    return Some(png);
                }
            }
        }
        let origin = reqwest::Url::parse(&homepage)
            .ok()?
            .join("/favicon.ico")
            .ok()?;
        self.fetch_icon(origin.as_str()).await
    }

    async fn fetch_icon(&self, url: &str) -> Option<Vec<u8>> {
        let bytes = self.fetch(url, ICON_CAP).await?;
        tokio::task::spawn_blocking(move || image::to_png(&bytes, ICON_SIZE).ok())
            .await
            .ok()?
    }

    /// HTTPS GET to a host that resolves only to public addresses, with a size cap.
    async fn fetch(&self, url: &str, cap: usize) -> Option<Vec<u8>> {
        let u = reqwest::Url::parse(url).ok()?;
        if u.scheme() != "https" {
            return None;
        }
        let host = u.host_str()?.to_string();
        let addrs: Vec<_> = tokio::net::lookup_host((host.as_str(), 443))
            .await
            .ok()?
            .collect();
        if addrs.is_empty() || !addrs.iter().all(|a| image::is_public_ip(a.ip())) {
            return None;
        }
        let _permit = self.remote.acquire().await.ok()?;
        let mut resp = self.http.get(u).send().await.ok()?;
        if !resp.status().is_success() {
            return None;
        }
        let mut buf = image::CappedBuf::new(cap);
        while let Some(chunk) = resp.chunk().await.ok()? {
            buf.push(&chunk).ok()?;
        }
        Some(buf.data)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_icon_requests() {
        assert_eq!(
            parse_request("/winget%3AGit.Git", Some("name=Git")),
            Some(("winget:Git.Git".into(), Some("Git".into())))
        );
        assert_eq!(
            parse_request(
                "/local%3AARP%5CMachine%5CX64%5CAndroid%20Studio",
                Some("name=Android+Studio")
            ),
            Some((
                r"local:ARP\Machine\X64\Android Studio".into(),
                Some("Android Studio".into())
            ))
        );
        assert_eq!(
            parse_request("/choco%3Avcredist140", None),
            Some(("choco:vcredist140".into(), None))
        );
        assert_eq!(parse_request("/nocolon", None), None);
        assert_eq!(parse_request("/bad%zz", None), None);
        assert_eq!(parse_request("/", None), None);
    }
}
