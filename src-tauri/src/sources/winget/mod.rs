//! winget package source: status, installed list, upgrades and search over the CLI.

pub mod args;
pub mod details;
pub mod parse;

use crate::error::{AppError, AppResult};
use crate::ids::{SearchQuery, SourceKind, StoreId, WingetId};
use crate::sources::model::{
    InstalledPackage, SourcePackage, SourceState, SourceStatus, UpgradeCandidate,
};
use crate::sources::process::{CommandSpec, ProcessOutput, Runner};
use parse::{parse_tables, Row};
use std::path::PathBuf;
use std::sync::atomic::{AtomicU32, Ordering};
use std::time::Duration;

/// `APPINSTALLER_CLI_ERROR_NO_APPLICATIONS_FOUND` (0x8A150014): "nothing matched", not a failure.
const NO_APPLICATIONS_FOUND: i32 = -1978335212;
const SELF_ID: &str = "QuantumStep.QuantumUpdater";
const TRUNCATION: char = '\u{2026}';

pub struct WingetSource<R: Runner> {
    pub(crate) runner: R,
    exe: Option<PathBuf>,
}

impl<R: Runner> WingetSource<R> {
    pub fn new(runner: R, exe: Option<PathBuf>) -> Self {
        Self { runner, exe }
    }

    pub fn exe(&self) -> Option<&PathBuf> {
        self.exe.as_ref()
    }

    async fn run(&self, args: Vec<String>, secs: u64) -> AppResult<ProcessOutput> {
        let exe = self
            .exe
            .clone()
            .ok_or_else(|| AppError::new("source_missing", "winget is not installed"))?;
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
                version: out.stdout.trim().trim_start_matches('v').to_string(),
            },
            Ok(out) => SourceState::Broken { reason: tail(&out) },
            Err(e) if e.code == "source_missing" => SourceState::Missing,
            Err(e) => SourceState::Broken { reason: e.message },
        };
        SourceStatus {
            kind: SourceKind::Winget,
            state,
        }
    }

    /// Rows from a query, treating "no table" as empty when winget says nothing matched.
    async fn query(&self, args: Vec<String>, secs: u64, has_source: bool) -> AppResult<Vec<Row>> {
        let out = self.run(args, secs).await?;
        let rows: Vec<Row> = parse_tables(&out.stdout, has_source)
            .into_iter()
            .flatten()
            .collect();
        if rows.is_empty() && !matches!(out.code, Some(0) | Some(NO_APPLICATIONS_FOUND)) {
            return Err(AppError::new(
                "process_failed",
                format!("winget failed: {}", tail(&out)),
            ));
        }
        Ok(rows)
    }

    pub async fn list_installed(&self) -> AppResult<Vec<InstalledPackage>> {
        let rows = self
            .resolve_truncated(self.query(args::list(), 90, true).await?)
            .await;
        Ok(rows
            .into_iter()
            .map(|r| {
                let source = row_source(r.source.as_deref(), &r.id);
                InstalledPackage {
                    label: super::label::classify(source, &r.id),
                    component: false,
                    name: r.name,
                    id: r.id,
                    version: r.version,
                    available: r.extra,
                    source,
                }
            })
            .collect())
    }

    /// Installed version of one app, or None when it isn't installed.
    pub async fn installed_version(&self, id: &WingetId) -> AppResult<Option<String>> {
        let rows = self.query(args::list_id(id), 30, true).await?;
        Ok(rows
            .into_iter()
            .find(|r| r.id.eq_ignore_ascii_case(id.as_str()))
            .map(|r| r.version))
    }

    pub async fn list_upgrades(&self, include_unknown: bool) -> AppResult<Vec<UpgradeCandidate>> {
        let rows = self
            .resolve_truncated(
                self.query(args::upgrade(include_unknown), 120, true)
                    .await?,
            )
            .await;
        Ok(rows
            .into_iter()
            .filter(|r| r.id != SELF_ID)
            .filter_map(|r| {
                let source = row_source(r.source.as_deref(), &r.id)?;
                Some(UpgradeCandidate {
                    available_version: r.extra?,
                    name: r.name,
                    id: r.id,
                    installed_version: r.version,
                    source,
                })
            })
            .collect())
    }

    /// Package details (`winget show --exact`), used by the app details panel.
    pub async fn show(&self, id: &WingetId) -> AppResult<details::ShowInfo> {
        self.show_in("winget", id.as_str()).await
    }

    /// The installer address winget would use for one version (latest when `None`). `None`
    /// when it doesn't say (other display languages, or no address in the manifest).
    pub async fn installer_url(
        &self,
        id: &WingetId,
        version: Option<&crate::ids::PkgVersion>,
    ) -> AppResult<Option<String>> {
        let mut args: Vec<String> = [
            "show",
            "--id",
            id.as_str(),
            "--exact",
            "--source",
            "winget",
            "--accept-source-agreements",
            "--disable-interactivity",
        ]
        .map(String::from)
        .to_vec();
        if let Some(v) = version {
            args.extend(["--version".into(), v.as_str().to_string()]);
        }
        let out = self.run(args, 30).await?;
        if out.code != Some(0) {
            return Err(AppError::new(
                "process_failed",
                format!("winget show failed: {}", tail(&out)),
            ));
        }
        Ok(details::parse_show(&out.stdout).installer_url)
    }

    /// Versions the winget catalog still lists for a package.
    pub async fn versions(&self, id: &WingetId) -> AppResult<Vec<String>> {
        let args = [
            "show",
            "--id",
            id.as_str(),
            "--exact",
            "--source",
            "winget",
            "--versions",
            "--accept-source-agreements",
            "--disable-interactivity",
        ]
        .map(String::from)
        .to_vec();
        let out = self.run(args, 45).await?;
        if out.code != Some(0) {
            return Err(AppError::new(
                "process_failed",
                format!("winget show --versions failed: {}", tail(&out)),
            ));
        }
        Ok(crate::rollback::parse_versions(&out.stdout))
    }

    /// Store app details (publisher, licence, description).
    pub async fn show_store(&self, id: &StoreId) -> AppResult<details::ShowInfo> {
        self.show_in("msstore", id.as_str()).await
    }

    async fn show_in(&self, source: &str, id: &str) -> AppResult<details::ShowInfo> {
        let args = [
            "show",
            "--id",
            id,
            "--exact",
            "--source",
            source,
            "--accept-source-agreements",
            "--disable-interactivity",
        ]
        .map(String::from)
        .to_vec();
        let out = self.run(args, 45).await?;
        if out.code != Some(0) {
            return Err(AppError::new(
                "process_failed",
                format!("winget show failed: {}", tail(&out)),
            ));
        }
        Ok(details::parse_show(&out.stdout))
    }

    /// Every winget package with a tag (local index, fast). Unfiltered; see `catalog::browse`.
    pub async fn search_tag(&self, tag: &str) -> AppResult<Vec<SourcePackage>> {
        if tag.starts_with('-') || tag.is_empty() {
            return Err(AppError::new("invalid_input", "bad tag"));
        }
        let rows = self.query(args::search_tag(tag), 30, false).await?;
        Ok(rows
            .into_iter()
            .filter(|r| valid(&r.id))
            .map(|r| SourcePackage {
                version: (!r.version.is_empty() && r.version != "Unknown").then_some(r.version),
                name: r.name,
                id: r.id,
                source: SourceKind::Winget,
                matched_on: None,
                description: None,
                homepage: None,
                icon_url: None,
            })
            .collect())
    }

    /// Microsoft Store search. No Source or version columns: versions are "Unknown".
    pub async fn search_store(&self, q: &SearchQuery) -> AppResult<Vec<SourcePackage>> {
        let rows = self
            .query(args::search_store(q.as_str()), 60, false)
            .await?;
        Ok(rows
            .into_iter()
            .filter(|r| StoreId::try_from(r.id.clone()).is_ok())
            .map(|r| SourcePackage {
                version: None,
                name: r.name,
                id: r.id,
                source: SourceKind::Msstore,
                matched_on: None,
                description: None,
                homepage: None,
                icon_url: None,
            })
            .collect())
    }

    pub async fn search(&self, q: &SearchQuery) -> AppResult<Vec<SourcePackage>> {
        // `--source winget` is fixed, so there is no Source column; every row is winget.
        let rows = self.query(args::search(q.as_str()), 60, false).await?;
        Ok(rows
            .into_iter()
            .filter(|r| valid(&r.id))
            .map(|r| SourcePackage {
                version: (!r.version.is_empty() && r.version != "Unknown").then_some(r.version),
                name: r.name,
                id: r.id,
                source: SourceKind::Winget,
                matched_on: r.extra,
                description: None,
                homepage: None,
                icon_url: None,
            })
            .collect())
    }

    /// winget truncates ids with an ellipsis on narrow consoles. Resolve each by unique prefix
    /// against `winget export`; drop rows that stay ambiguous rather than guess.
    async fn resolve_truncated(&self, rows: Vec<Row>) -> Vec<Row> {
        if !rows.iter().any(|r| r.id.ends_with(TRUNCATION)) {
            return rows;
        }
        let known = self.export_ids().await.unwrap_or_default();
        rows.into_iter()
            .filter_map(|mut r| {
                let Some(prefix) = r.id.strip_suffix(TRUNCATION) else {
                    return Some(r);
                };
                let mut matches = known.iter().filter(|k| k.starts_with(prefix));
                match (matches.next(), matches.next()) {
                    (Some(full), None) => {
                        r.id = full.clone();
                        Some(r)
                    }
                    _ => None,
                }
            })
            .collect()
    }

    async fn export_ids(&self) -> AppResult<Vec<String>> {
        static N: AtomicU32 = AtomicU32::new(0);
        let path = std::env::temp_dir().join(format!(
            "quantum-updater-export-{}-{}.json",
            std::process::id(),
            N.fetch_add(1, Ordering::Relaxed)
        ));
        let path_str = path.to_string_lossy().into_owned();
        let result = self.run(args::export(&path_str), 90).await;
        let text = std::fs::read_to_string(&path);
        let _ = std::fs::remove_file(&path);
        result?;
        let json: serde_json::Value = serde_json::from_str(&text?)
            .map_err(|e| AppError::new("parse", format!("winget export: {e}")))?;
        Ok(json["Sources"]
            .as_array()
            .into_iter()
            .flatten()
            .flat_map(|s| s["Packages"].as_array().into_iter().flatten())
            .filter_map(|p| p["PackageIdentifier"].as_str().map(String::from))
            .collect())
    }
}

fn valid(id: &str) -> bool {
    WingetId::try_from(id.to_string()).is_ok()
}

/// Which source manages a `list`/`upgrade` row: winget and Store rows with valid ids only;
/// anything else (MSIX\..., ARP\...) is handled by Windows.
pub(crate) fn row_source(source: Option<&str>, id: &str) -> Option<SourceKind> {
    match source {
        Some("winget") if valid(id) => Some(SourceKind::Winget),
        Some("msstore") if StoreId::try_from(id.to_string()).is_ok() => Some(SourceKind::Msstore),
        _ => None,
    }
}

fn tail(out: &ProcessOutput) -> String {
    let text = format!("{} {}", out.stdout.trim(), out.stderr.trim());
    let text = text.trim();
    let start = text
        .char_indices()
        .rev()
        .nth(299)
        .map(|(i, _)| i)
        .unwrap_or(0);
    format!("exit {:?}: {}", out.code, &text[start..])
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::error::AppError;
    use crate::ids::{SearchQuery, SourceKind, WingetId};
    use crate::sources::fake::FakeRunner;
    use crate::sources::model::SourceState;

    const LIST: &str = include_str!("../../../tests/fixtures/winget/list.en.txt");
    const UPGRADE: &str = include_str!("../../../tests/fixtures/winget/upgrade.en.txt");
    const UPGRADE_SELF: &str = include_str!("../../../tests/fixtures/winget/upgrade-self.en.txt");
    const TRUNC: &str = include_str!("../../../tests/fixtures/winget/upgrade-truncated.en.txt");
    const TRUNC_EXPORT: &str = include_str!("../../../tests/fixtures/winget/export-truncated.json");
    const NO_RESULTS: &str = include_str!("../../../tests/fixtures/winget/no-results.en.txt");
    const SEARCH_SOURCED: &str =
        include_str!("../../../tests/fixtures/winget/search-firefox-sourced.en.txt");

    #[tokio::test]
    async fn installed_version_of_one_app() {
        let found = include_str!("../../../tests/fixtures/winget/list-id.en.txt");
        let missing = include_str!("../../../tests/fixtures/winget/list-id-missing.en.txt");
        let id = crate::ids::WingetId::try_from("Docker.DockerDesktop".to_string()).unwrap();
        let s = src(FakeRunner::new().on("list", 0, found));
        assert_eq!(
            s.installed_version(&id).await.unwrap().as_deref(),
            Some("4.94.0")
        );
        let call = &s.runner.calls()[0];
        assert!(
            call.windows(2)
                .any(|w| w == ["--id", "Docker.DockerDesktop"]),
            "{call:?}"
        );
        assert!(call.contains(&"--exact".to_string()));
        let s = src(FakeRunner::new().on("list", NO_APPLICATIONS_FOUND, missing));
        assert_eq!(s.installed_version(&id).await.unwrap(), None);
    }

    #[tokio::test]
    async fn store_search_and_installed_store_apps() {
        let s = src(FakeRunner::new().on(
            "search",
            0,
            include_str!("../../../tests/fixtures/winget/search-msstore.en.txt"),
        ));
        let q = SearchQuery::try_from("whatsapp".to_string()).unwrap();
        let r = s.search_store(&q).await.unwrap();
        assert_eq!(r.len(), 2);
        assert_eq!(r[0].id, "9NKSQGP7F2NH");
        assert_eq!(r[0].source, SourceKind::Msstore);
        assert_eq!(r[0].version, None);

        let s = src(FakeRunner::new().on(
            "list",
            0,
            include_str!("../../../tests/fixtures/winget/list-msstore.en.txt"),
        ));
        let items = s.list_installed().await.unwrap();
        let todo = items.iter().find(|p| p.id == "9NBLGGH5R558").unwrap();
        assert_eq!(todo.source, Some(SourceKind::Msstore));
        let docker = items
            .iter()
            .find(|p| p.id == "Docker.DockerDesktop")
            .unwrap();
        assert_eq!(docker.source, Some(SourceKind::Winget));
        assert!(items
            .iter()
            .any(|p| p.id.starts_with("MSIX\\") && p.source.is_none()));
    }

    fn src(r: FakeRunner) -> WingetSource<FakeRunner> {
        WingetSource::new(r, Some(r"C:\fake\winget.exe".into()))
    }

    #[tokio::test]
    async fn show_returns_package_details() {
        let show = include_str!("../../../tests/fixtures/winget/show-firefox.en.txt");
        let s = src(FakeRunner::new().on("show", 0, show));
        let info = s
            .show(&WingetId::try_from("Mozilla.Firefox".to_string()).unwrap())
            .await
            .unwrap();
        assert_eq!(info.publisher.as_deref(), Some("Mozilla"));
        assert_eq!(
            info.homepage.as_deref(),
            Some("https://www.mozilla.org/firefox/")
        );
        let call = &s.runner.calls()[0];
        assert!(
            call.windows(2).any(|w| w == ["--id", "Mozilla.Firefox"])
                && call.contains(&"--exact".to_string())
        );
    }

    #[tokio::test]
    async fn status_ready_strips_v() {
        let s = src(FakeRunner::new().on("--version", 0, "v1.29.380\r\n"))
            .status()
            .await;
        assert_eq!(s.kind, SourceKind::Winget);
        assert_eq!(
            s.state,
            SourceState::Ready {
                version: "1.29.380".into()
            }
        );
    }

    #[tokio::test]
    async fn status_missing_without_exe() {
        let s = WingetSource::new(FakeRunner::new(), None).status().await;
        assert_eq!(s.state, SourceState::Missing);
    }

    #[tokio::test]
    async fn installed_marks_local_rows_display_only() {
        let items = src(FakeRunner::new().on("list", 0, LIST))
            .list_installed()
            .await
            .unwrap();
        assert_eq!(items.len(), 245);
        let pro = items.iter().find(|p| p.id == "Adobe.Acrobat.Pro").unwrap();
        assert_eq!(pro.source, Some(SourceKind::Winget));
        let studio = items.iter().find(|p| p.name == "Android Studio").unwrap();
        assert_eq!(studio.source, None);
    }

    #[tokio::test]
    async fn upgrades_parse_and_pass_include_unknown() {
        let s = src(FakeRunner::new().on("upgrade", 0, UPGRADE));
        let u = s.list_upgrades(true).await.unwrap();
        assert_eq!(u.len(), 1);
        assert_eq!(u[0].installed_version, "9.9.2");
        assert_eq!(u[0].available_version, "9.9.3");
        assert!(s.runner.calls()[0].contains(&"--include-unknown".to_string()));
        let s2 = src(FakeRunner::new().on("upgrade", 0, UPGRADE));
        s2.list_upgrades(false).await.unwrap();
        assert!(!s2.runner.calls()[0].contains(&"--include-unknown".to_string()));
    }

    #[tokio::test]
    async fn self_is_excluded_from_upgrades() {
        let u = src(FakeRunner::new().on("upgrade", 0, UPGRADE_SELF))
            .list_upgrades(false)
            .await
            .unwrap();
        assert_eq!(u.len(), 1);
        assert!(u.iter().all(|c| c.id != "QuantumStep.QuantumUpdater"));
    }

    #[tokio::test]
    async fn truncated_ids_resolve_via_export_or_drop() {
        let s = src(FakeRunner::new()
            .on("upgrade", 0, TRUNC)
            .export(TRUNC_EXPORT));
        let ids: Vec<_> = s
            .list_upgrades(false)
            .await
            .unwrap()
            .into_iter()
            .map(|c| c.id)
            .collect();
        assert_eq!(
            ids,
            [
                "CrystalDewWorld.CrystalDiskInfo",
                "Contoso.VeryLongPackageName"
            ]
        );
    }

    #[tokio::test]
    async fn search_pins_winget_source_so_msstore_never_appears() {
        let s = src(FakeRunner::new().on("search", 0, SEARCH_SOURCED));
        let r = s
            .search(&SearchQuery::try_from("firefox".to_string()).unwrap())
            .await
            .unwrap();
        assert!(!r.is_empty());
        assert!(r.iter().all(|p| p.source == SourceKind::Winget));
        let call = &s.runner.calls()[0];
        assert_eq!(&call[..3], ["search", "--query", "firefox"]);
        assert!(call.windows(2).any(|w| w == ["--source", "winget"]));
    }

    /// Real output of `search --source winget`: winget omits the Source column (found live).
    #[tokio::test]
    async fn search_with_source_flag_has_no_source_column() {
        let s = src(FakeRunner::new().on("search", 0, SEARCH_SOURCED));
        let r = s
            .search(&SearchQuery::try_from("firefox".to_string()).unwrap())
            .await
            .unwrap();
        assert!(r.len() >= 40, "got {}", r.len());
        assert_eq!(r[0].id, "Mozilla.Firefox");
        assert_eq!(r[0].version.as_deref(), Some("157.0.1"));
        assert_eq!(r[0].matched_on.as_deref(), Some("Moniker: firefox"));
    }

    #[tokio::test]
    async fn no_results_code_is_empty_not_error() {
        let q = SearchQuery::try_from("zzzz".to_string()).unwrap();
        let ok = src(FakeRunner::new().on("search", -1978335212, NO_RESULTS))
            .search(&q)
            .await;
        assert_eq!(ok.unwrap(), vec![]);
        let err = src(FakeRunner::new().on("search", 1, NO_RESULTS))
            .search(&q)
            .await;
        assert_eq!(err.unwrap_err().code, "process_failed");
        let none = src(FakeRunner::new().on(
            "upgrade",
            0,
            "No installed package found matching input criteria.\r\n",
        ));
        assert_eq!(none.list_upgrades(false).await.unwrap(), vec![]);
    }

    #[tokio::test]
    async fn runner_missing_exe_propagates_source_missing() {
        let s = src(FakeRunner::new().fail("list", AppError::new("source_missing", "gone")));
        assert_eq!(s.list_installed().await.unwrap_err().code, "source_missing");
    }
}
