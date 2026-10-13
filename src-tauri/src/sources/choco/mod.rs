//! Chocolatey package source: CLI for local state, community OData feed for search.

pub mod args;
pub mod odata;
pub mod parse;
pub mod titles;

use crate::error::{AppError, AppResult};
use crate::ids::{ChocoId, SearchQuery, SourceKind};
use crate::sources::model::{
    InstalledPackage, SourcePackage, SourceState, SourceStatus, UpgradeCandidate,
};
use crate::sources::process::{CommandSpec, ProcessOutput, Runner};
use std::collections::HashMap;
use std::path::PathBuf;
use std::time::Duration;

pub struct ChocoSource<R: Runner> {
    pub(crate) runner: R,
    exe: Option<PathBuf>,
    http: reqwest::Client,
    major: tokio::sync::OnceCell<u32>,
    titles: titles::TitleCache,
}

impl<R: Runner> ChocoSource<R> {
    pub fn new(runner: R, exe: Option<PathBuf>) -> Self {
        let http = reqwest::Client::builder()
            .https_only(true)
            .timeout(Duration::from_secs(10))
            .user_agent(concat!("QuantumUpdater/", env!("CARGO_PKG_VERSION")))
            .build()
            .expect("static reqwest config");
        Self {
            runner,
            exe,
            http,
            major: tokio::sync::OnceCell::new(),
            titles: titles::TitleCache::default(),
        }
    }

    pub fn exe(&self) -> Option<&PathBuf> {
        self.exe.as_ref()
    }

    async fn run(&self, args: Vec<String>, secs: u64) -> AppResult<ProcessOutput> {
        let exe = self
            .exe
            .clone()
            .ok_or_else(|| AppError::new("source_missing", "Chocolatey is not installed"))?;
        self.runner
            .run(&CommandSpec {
                exe,
                args,
                timeout: Duration::from_secs(secs),
            })
            .await
    }

    pub async fn status(&self) -> SourceStatus {
        let state = match self.run(args::version(), 15).await {
            Ok(out) if out.code == Some(0) => SourceState::Ready {
                version: out.stdout.trim().to_string(),
            },
            Ok(out) => SourceState::Broken {
                reason: format!("choco exited {:?}: {}", out.code, out.stdout.trim()),
            },
            Err(e) if e.code == "source_missing" => SourceState::Missing,
            Err(e) => SourceState::Broken { reason: e.message },
        };
        SourceStatus {
            kind: SourceKind::Choco,
            state,
        }
    }

    /// Cached: the CLI shape only changes if Chocolatey itself is upgraded mid-session.
    async fn major_version(&self) -> u32 {
        *self
            .major
            .get_or_init(|| async {
                match self.run(args::version(), 15).await {
                    Ok(out) => parse::major(&out.stdout).unwrap_or(2),
                    Err(_) => 2,
                }
            })
            .await
    }

    /// Installed version of one package, or None when it isn't installed.
    pub async fn installed_version(&self, id: &ChocoId) -> AppResult<Option<String>> {
        let major = self.major_version().await;
        let out = self.run(args::list_id(major, id), 30).await?;
        Ok(parse::pipe_rows(&out.stdout, 2)
            .into_iter()
            .find(|r| r[0].eq_ignore_ascii_case(id.as_str()))
            .map(|r| r[1].clone()))
    }

    pub async fn list_installed(&self) -> AppResult<Vec<InstalledPackage>> {
        let major = self.major_version().await;
        let out = self.run(args::list(major), 90).await?;
        let rows = parse::pipe_rows(&out.stdout, 2);
        if rows.is_empty() && out.code != Some(0) {
            return Err(AppError::new(
                "process_failed",
                format!("choco list exited {:?}", out.code),
            ));
        }
        Ok(rows
            .into_iter()
            .filter(|r| ChocoId::try_from(r[0].clone()).is_ok())
            .map(|r| InstalledPackage {
                name: r[0].clone(),
                id: r[0].clone(),
                version: r[1].clone(),
                available: None,
                source: Some(SourceKind::Choco),
            })
            .collect())
    }

    /// `choco outdated` exits 2 when something is outdated (enhanced exit codes).
    pub async fn list_upgrades(&self) -> AppResult<Vec<UpgradeCandidate>> {
        let out = self.run(args::outdated(), 180).await?;
        let rows = parse::pipe_rows(&out.stdout, 3);
        if rows.is_empty() && !matches!(out.code, Some(0) | Some(2)) {
            return Err(AppError::new(
                "process_failed",
                format!("choco outdated exited {:?}", out.code),
            ));
        }
        Ok(rows
            .into_iter()
            // 4th field is "pinned": the user asked Chocolatey never to upgrade it.
            .filter(|r| r.get(3).map(String::as_str) != Some("true"))
            .filter(|r| ChocoId::try_from(r[0].clone()).is_ok())
            .map(|r| UpgradeCandidate {
                name: r[0].clone(),
                id: r[0].clone(),
                installed_version: r[1].clone(),
                available_version: r[2].clone(),
                source: SourceKind::Choco,
            })
            .collect())
    }

    /// Human titles for package ids ("vcredist140" -> "Microsoft Visual C++ Redistributable ...").
    /// Ids without a title (lookup failed, timed out or offline) are left out; the caller shows
    /// the id. See `titles::lookup` for the time limit and miss cache.
    pub async fn titles(&self, ids: &[String]) -> HashMap<String, String> {
        let http = self.http.clone();
        let fetch = move |id: ChocoId| {
            let http = http.clone();
            async move {
                let resp = http.get(odata::by_id_url(&id)).send().await.ok()?;
                if !resp.status().is_success() {
                    return None;
                }
                let xml = resp.text().await.ok()?;
                odata::parse_feed(&xml)
                    .ok()?
                    .into_iter()
                    .next()
                    .map(|p| p.name)
            }
        };
        titles::lookup(ids, &self.titles, fetch, titles::DEADLINE).await
    }

    /// Latest package metadata from the community feed (details panel).
    pub async fn package(&self, id: &ChocoId) -> AppResult<Option<SourcePackage>> {
        let resp = self
            .http
            .get(odata::by_id_url(id))
            .send()
            .await
            .map_err(|e| AppError::new("network", format!("Chocolatey feed: {e}")))?;
        let xml = resp
            .text()
            .await
            .map_err(|e| AppError::new("network", format!("Chocolatey feed: {e}")))?;
        Ok(odata::parse_feed(&xml)?.into_iter().next())
    }

    pub async fn search(&self, q: &SearchQuery) -> AppResult<Vec<SourcePackage>> {
        let resp = self
            .http
            .get(odata::search_url(q))
            .send()
            .await
            .map_err(|e| AppError::new("network", format!("Chocolatey search: {e}")))?;
        if !resp.status().is_success() {
            return Err(AppError::new(
                "network",
                format!("Chocolatey search: HTTP {}", resp.status()),
            ));
        }
        let body = resp
            .text()
            .await
            .map_err(|e| AppError::new("network", format!("Chocolatey search: {e}")))?;
        odata::parse_feed(&body)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::error::AppError;
    use crate::ids::SourceKind;
    use crate::sources::fake::FakeRunner;
    use crate::sources::model::SourceState;

    const LIST: &str = include_str!("../../../tests/fixtures/choco/list-r.v2.txt");
    const OUTDATED: &str = include_str!("../../../tests/fixtures/choco/outdated-r.v2.txt");

    fn src(r: FakeRunner) -> ChocoSource<FakeRunner> {
        ChocoSource::new(r, Some(r"C:\ProgramData\chocolatey\bin\choco.exe".into()))
    }

    #[tokio::test]
    async fn installed_version_of_one_package() {
        let id = ChocoId::try_from("vcredist140".to_string()).unwrap();
        let s = src(FakeRunner::new()
            .on(
                "--version",
                0,
                "2.7.4
",
            )
            .on(
                "list",
                0,
                include_str!("../../../tests/fixtures/choco/list-id.v2.txt"),
            ));
        assert_eq!(
            s.installed_version(&id).await.unwrap().as_deref(),
            Some("14.44.35211")
        );
        let call = s
            .runner
            .calls()
            .into_iter()
            .find(|c| c[0] == "list")
            .unwrap();
        assert_eq!(call, ["list", "vcredist140", "--exact", "-r"]);
        let s = src(FakeRunner::new()
            .on("--version", 0, "2.7.4")
            .on("list", 0, ""));
        assert_eq!(s.installed_version(&id).await.unwrap(), None);
    }

    #[tokio::test]
    async fn status_ready_and_missing() {
        let s = src(FakeRunner::new().on("--version", 0, "2.7.4\r\n"))
            .status()
            .await;
        assert_eq!(s.kind, SourceKind::Choco);
        assert_eq!(
            s.state,
            SourceState::Ready {
                version: "2.7.4".into()
            }
        );
        let m = ChocoSource::new(FakeRunner::new(), None).status().await;
        assert_eq!(m.state, SourceState::Missing);
        let gone = src(FakeRunner::new().fail("--version", AppError::new("source_missing", "x")));
        assert_eq!(gone.status().await.state, SourceState::Missing);
        let broken = src(FakeRunner::new().on("--version", 1, "boom"))
            .status()
            .await;
        assert!(matches!(broken.state, SourceState::Broken { .. }));
    }

    #[tokio::test]
    async fn v2_list_filters_noise() {
        let s = src(FakeRunner::new()
            .on("--version", 0, "2.7.4")
            .on("list", 0, LIST));
        let items = s.list_installed().await.unwrap();
        assert_eq!(items.len(), 16);
        assert_eq!(items[0].id, "chocolatey");
        assert_eq!(items[0].version, "2.7.4");
        assert!(items.iter().all(|p| p.source == Some(SourceKind::Choco)));
        assert!(items
            .iter()
            .all(|p| !p.id.contains("try") && !p.id.contains("Access")));
        let list_call = s
            .runner
            .calls()
            .into_iter()
            .find(|c| c[0] == "list")
            .unwrap();
        assert_eq!(list_call, ["list", "-r"]);
    }

    #[tokio::test]
    async fn v1_uses_local_only() {
        let s = src(FakeRunner::new()
            .on("--version", 0, "1.4.0")
            .on("list", 0, LIST));
        s.list_installed().await.unwrap();
        let list_call = s
            .runner
            .calls()
            .into_iter()
            .find(|c| c[0] == "list")
            .unwrap();
        assert_eq!(list_call, ["list", "--local-only", "-r"]);
    }

    #[tokio::test]
    async fn pinned_packages_are_not_updates() {
        let pinned = include_str!("../../../tests/fixtures/choco/outdated-pinned.txt");
        let u = src(FakeRunner::new().on("outdated", 2, pinned))
            .list_upgrades()
            .await
            .unwrap();
        let ids: Vec<_> = u.iter().map(|c| c.id.as_str()).collect();
        assert_eq!(ids, ["vcredist140"]);
    }

    #[tokio::test]
    async fn outdated_parses_candidates() {
        let u = src(FakeRunner::new().on("outdated", 0, OUTDATED))
            .list_upgrades()
            .await
            .unwrap();
        assert_eq!(u.len(), 3);
        let vc = u.iter().find(|c| c.id == "vcredist140").unwrap();
        assert_eq!(vc.installed_version, "14.44.35211");
        assert_eq!(vc.available_version, "14.51.36247");
        assert_eq!(vc.source, SourceKind::Choco);
    }
}
