//! Real, original app icons. Resolution order: the installed program itself (registry icon,
//! main exe, MSIX logo), then the Chocolatey package icon, then the publisher's homepage icon.
//! Everything is re-encoded to PNG, cached on disk, and served over the `icon` URI scheme.

pub mod cache;
pub mod image;
pub mod sources;
pub mod win;

use crate::ids::{ChocoId, WingetId};
use crate::sources::process::{CommandSpec, Runner};
use cache::{IconCache, Lookup};
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
/// winget's "no package found" (0x8A150014).
const NO_APPLICATIONS_FOUND: i32 = -1978335212;

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

/// Keep only public internet addresses from a DNS answer.
pub fn public_only(
    addrs: impl IntoIterator<Item = std::net::SocketAddr>,
) -> impl Iterator<Item = std::net::SocketAddr> {
    addrs.into_iter().filter(|a| image::is_public_ip(a.ip()))
}

/// Redirect hops must stay on https and never point at a private IP literal.
pub fn redirect_allowed(url: &reqwest::Url) -> bool {
    if url.scheme() != "https" {
        return false;
    }
    match url.host() {
        Some(url::Host::Ipv4(ip)) => image::is_public_ip(ip.into()),
        Some(url::Host::Ipv6(ip)) => image::is_public_ip(ip.into()),
        Some(url::Host::Domain(_)) => true,
        None => false,
    }
}

/// DNS for icon downloads: every lookup (including redirects and re-resolution) drops private
/// addresses, so a hostile site can't point the elevated app at the local network.
struct PublicDns;

impl reqwest::dns::Resolve for PublicDns {
    fn resolve(&self, name: reqwest::dns::Name) -> reqwest::dns::Resolving {
        let host = name.as_str().to_string();
        Box::pin(async move {
            let addrs: Vec<_> =
                public_only(tokio::net::lookup_host((host.as_str(), 0)).await?).collect();
            if addrs.is_empty() {
                return Err("host has no public address".into());
            }
            Ok(Box::new(addrs.into_iter()) as reqwest::dns::Addrs)
        })
    }
}

/// Why a download didn't produce bytes.
#[derive(Debug, PartialEq, Eq)]
enum Fetch {
    /// The server answered: there's nothing here (4xx) or it isn't usable.
    Gone,
    /// Network, DNS, timeout or server error: worth retrying later.
    Retry,
}

/// Overflowing the cap either fails the download (images) or keeps the first part (HTML,
/// where the icon links live in <head>).
#[derive(Clone, Copy)]
enum Overflow {
    Fail,
    Truncate,
}

pub struct IconService {
    cache: IconCache,
    http: reqwest::Client,
    runner: Arc<dyn Runner>,
    winget: Option<PathBuf>,
    program_files: Option<PathBuf>,
    choco_urls: Mutex<HashMap<String, String>>,
    arp: tokio::sync::Mutex<Option<(Instant, Arc<Vec<ArpEntry>>)>>,
    remote: Semaphore,
    shows: Semaphore,
}

impl IconService {
    pub fn new(dir: PathBuf, runner: Arc<dyn Runner>, winget: Option<PathBuf>) -> Self {
        let http = reqwest::Client::builder()
            .https_only(true)
            .timeout(Duration::from_secs(8))
            .dns_resolver(Arc::new(PublicDns))
            .redirect(reqwest::redirect::Policy::custom(|attempt| {
                if attempt.previous().len() >= 3 {
                    attempt.error("too many redirects")
                } else if redirect_allowed(attempt.url()) {
                    attempt.follow()
                } else {
                    attempt.stop()
                }
            }))
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
            arp: tokio::sync::Mutex::new(None),
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
        // The Windows name decides which installed program's icon is used, so it is part of the key.
        let cache_key = match name {
            Some(n) => format!("{key}|{}", n.to_lowercase()),
            None => key.to_string(),
        };
        self.cache
            .get_or_compute(&cache_key, self.compute(key, name))
            .await
    }

    async fn compute(&self, key: &str, name: Option<&str>) -> Lookup {
        let Some((source, id)) = key.split_once(':') else {
            return Lookup::Missing;
        };
        if let Some(png) = self.installed_icon(id, name).await {
            return Lookup::Found(png);
        }
        match source {
            "choco" => self.choco_icon(id).await,
            "winget" => self.homepage_icon(id).await,
            "msstore" => self.store_icon(id).await,
            _ => Lookup::Missing,
        }
    }

    /// Installed programs, scanned at most once per ARP_TTL even when many icons ask at once.
    async fn arp_entries(&self) -> Arc<Vec<ArpEntry>> {
        let mut slot = self.arp.lock().await;
        if let Some((at, list)) = slot.as_ref() {
            if at.elapsed() < ARP_TTL {
                return list.clone();
            }
        }
        let list = Arc::new(
            tokio::task::spawn_blocking(win::arp_entries)
                .await
                .unwrap_or_default(),
        );
        *slot = Some((Instant::now(), list.clone()));
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

    async fn choco_icon(&self, id: &str) -> Lookup {
        let known = self
            .choco_urls
            .lock()
            .unwrap_or_else(|p| p.into_inner())
            .get(&id.to_ascii_lowercase())
            .cloned();
        let url = match known {
            Some(u) => u,
            None => {
                let Ok(cid) = ChocoId::try_from(id.to_string()) else {
                    return Lookup::Missing;
                };
                let feed = crate::sources::choco::odata::by_id_url(&cid);
                let xml = match self.fetch(&feed, HTML_CAP, Overflow::Fail).await {
                    Ok(x) => x,
                    Err(Fetch::Gone) => return Lookup::Missing,
                    Err(Fetch::Retry) => return Lookup::Unavailable,
                };
                let Ok(pkgs) =
                    crate::sources::choco::odata::parse_feed(&String::from_utf8_lossy(&xml))
                else {
                    return Lookup::Unavailable;
                };
                match pkgs.into_iter().find_map(|p| p.icon_url) {
                    Some(u) => u,
                    None => return Lookup::Missing,
                }
            }
        };
        self.fetch_icon(&url).await
    }

    /// The Store listing's own logo (the same endpoint winget's Store source uses).
    async fn store_icon(&self, id: &str) -> Lookup {
        let Ok(sid) = crate::ids::StoreId::try_from(id.to_string()) else {
            return Lookup::Missing;
        };
        let url = format!(
            "https://storeedgefd.dsx.mp.microsoft.com/v9.0/products/{}?market=US&locale=en-us&deviceFamily=Windows.Desktop",
            sid.as_str()
        );
        let json = match self.fetch(&url, HTML_CAP, Overflow::Fail).await {
            Ok(j) => j,
            Err(Fetch::Gone) => return Lookup::Missing,
            Err(Fetch::Retry) => return Lookup::Unavailable,
        };
        match sources::store_icon_url(&String::from_utf8_lossy(&json)) {
            Some(icon) => self.fetch_icon(&icon).await,
            None => Lookup::Missing,
        }
    }

    /// Publisher homepage (from `winget show`), then its declared icon or /favicon.ico.
    async fn homepage_icon(&self, id: &str) -> Lookup {
        let Ok(wid) = WingetId::try_from(id.to_string()) else {
            return Lookup::Missing;
        };
        let Some(exe) = self.winget.clone() else {
            return Lookup::Unavailable;
        };
        let homepage = {
            let Ok(_permit) = self.shows.acquire().await else {
                return Lookup::Unavailable;
            };
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
                exe,
                args,
                timeout: Duration::from_secs(45),
            };
            let Ok(out) = self.runner.run(&spec).await else {
                return Lookup::Unavailable;
            };
            match out.code {
                Some(0) => {}
                Some(NO_APPLICATIONS_FOUND) => return Lookup::Missing,
                _ => return Lookup::Unavailable,
            }
            match crate::sources::winget::details::parse_show(&out.stdout).homepage {
                Some(h) => h,
                None => return Lookup::Missing,
            }
        };
        let mut retry = false;
        match self.fetch(&homepage, HTML_CAP, Overflow::Truncate).await {
            Ok(html) => {
                if let Some(link) =
                    sources::pick_icon_link(&String::from_utf8_lossy(&html), &homepage)
                {
                    match self.fetch_icon(&link).await {
                        Lookup::Found(png) => return Lookup::Found(png),
                        Lookup::Unavailable => retry = true,
                        Lookup::Missing => {}
                    }
                }
            }
            Err(Fetch::Retry) => retry = true,
            Err(Fetch::Gone) => {}
        }
        let favicon = reqwest::Url::parse(&homepage)
            .ok()
            .and_then(|u| u.join("/favicon.ico").ok());
        let Some(favicon) = favicon else {
            return Lookup::Missing;
        };
        match self.fetch_icon(favicon.as_str()).await {
            Lookup::Missing if retry => Lookup::Unavailable,
            other => other,
        }
    }

    async fn fetch_icon(&self, url: &str) -> Lookup {
        match self.fetch(url, ICON_CAP, Overflow::Fail).await {
            Ok(bytes) => {
                match tokio::task::spawn_blocking(move || image::to_png(&bytes, ICON_SIZE)).await {
                    Ok(Ok(png)) => Lookup::Found(png),
                    _ => Lookup::Missing,
                }
            }
            Err(Fetch::Gone) => Lookup::Missing,
            Err(Fetch::Retry) => Lookup::Unavailable,
        }
    }

    /// HTTPS GET to a host that resolves only to public addresses, with a size cap.
    async fn fetch(&self, url: &str, cap: usize, overflow: Overflow) -> Result<Vec<u8>, Fetch> {
        let u = reqwest::Url::parse(url).map_err(|_| Fetch::Gone)?;
        if u.scheme() != "https" {
            return Err(Fetch::Gone);
        }
        let host = u.host_str().ok_or(Fetch::Gone)?.to_string();
        let addrs: Vec<_> = tokio::net::lookup_host((host.as_str(), 443))
            .await
            .map_err(|_| Fetch::Retry)?
            .collect();
        if addrs.is_empty() || !addrs.iter().all(|a| image::is_public_ip(a.ip())) {
            return Err(Fetch::Gone);
        }
        let _permit = self.remote.acquire().await.map_err(|_| Fetch::Retry)?;
        let mut resp = self.http.get(u).send().await.map_err(|_| Fetch::Retry)?;
        let status = resp.status();
        if status.is_client_error() {
            return Err(Fetch::Gone);
        }
        if !status.is_success() {
            return Err(Fetch::Retry);
        }
        let mut buf = image::CappedBuf::new(cap);
        while let Some(chunk) = resp.chunk().await.map_err(|_| Fetch::Retry)? {
            if buf.push(&chunk).is_err() {
                return match overflow {
                    Overflow::Truncate => Ok(buf.data),
                    Overflow::Fail => Err(Fetch::Gone),
                };
            }
        }
        Ok(buf.data)
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

#[cfg(test)]
mod live {
    use super::*;
    /// Manual: `cargo test icons::live -- --ignored --nocapture` resolves real icons with a fresh cache.
    #[tokio::test]
    #[ignore]
    async fn resolve_real_icons() {
        let dir = tempfile::tempdir().unwrap();
        let paths = crate::sources::process::ExePaths::detect();
        let svc = IconService::new(
            dir.path().to_path_buf(),
            Arc::new(crate::sources::process::SystemRunner),
            paths.winget,
        );
        for (key, name) in [
            ("winget:Mozilla.Firefox", "Firefox"),
            ("winget:Brave.Brave", "Brave"),
            ("winget:Spotify.Spotify", "Spotify"),
            ("winget:OpenJS.NodeJS.LTS", "Node.js LTS"),
            (
                "winget:Microsoft.VisualStudioCode",
                "Microsoft Visual Studio Code (User)",
            ),
            ("choco:vcredist140", "vcredist140"),
        ] {
            let t = std::time::Instant::now();
            let r = svc.resolve(key, Some(name)).await;
            println!(
                "{key:45} {:>6} bytes  {:?}",
                r.map(|b| b.len()).unwrap_or(0),
                t.elapsed()
            );
        }
    }
}

#[cfg(test)]
mod network_policy {
    use super::*;

    #[test]
    fn dns_answers_are_filtered_to_public_addresses() {
        let addrs: Vec<std::net::SocketAddr> =
            ["127.0.0.1:443", "10.0.0.5:443", "1.1.1.1:443", "[::1]:443"]
                .iter()
                .map(|s| s.parse().unwrap())
                .collect();
        let kept: Vec<_> = public_only(addrs).collect();
        assert_eq!(
            kept,
            vec!["1.1.1.1:443".parse::<std::net::SocketAddr>().unwrap()]
        );
    }

    #[test]
    fn redirects_to_private_literals_are_refused() {
        for (url, ok) in [
            ("https://example.com/x.png", true),
            ("https://127.0.0.1/x", false),
            ("https://[::1]/x", false),
            ("https://192.168.1.10/x", false),
            ("http://example.com/x", false),
        ] {
            assert_eq!(
                redirect_allowed(&reqwest::Url::parse(url).unwrap()),
                ok,
                "{url}"
            );
        }
    }
}
