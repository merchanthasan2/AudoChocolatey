//! Long-lived services shared by all commands.

use crate::catalog::Catalog;
use crate::commands::jobs::TauriSink;
use crate::commands::library::Listing;
use crate::error::AppResult;
use crate::icons::IconService;
use crate::ids::{ChocoId, SourceKind, SourceRef, WingetId};
use crate::jobs::failure::FailureReason;
use crate::jobs::fallback;
use crate::jobs::worker::{Fallback, Queue, SystemMsiGate, Verifier, WingetSizer, WorkerDeps};
use crate::rules::RuleStore;
use crate::settings::SettingsStore;
use crate::sources::choco::ChocoSource;
use crate::sources::model::UpgradeCandidate;
use crate::sources::model::{OpKind, OpRequest};
use crate::sources::process::{ExePaths, SystemRunner};
use crate::sources::stream::SystemStreamRunner;
use crate::sources::winget::WingetSource;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::Duration;

/// Reads one app's installed version from the source that manages it.
struct SystemVerifier {
    winget: WingetSource<SystemRunner>,
    choco: ChocoSource<SystemRunner>,
}

#[async_trait::async_trait]
impl Verifier for SystemVerifier {
    async fn installed_version(&self, target: &SourceRef) -> AppResult<Option<String>> {
        match target.source() {
            SourceKind::Winget => {
                self.winget
                    .installed_version(&WingetId::try_from(target.id().to_string())?)
                    .await
            }
            SourceKind::Choco => {
                self.choco
                    .installed_version(&ChocoId::try_from(target.id().to_string())?)
                    .await
            }
            // Store ids are valid winget ids; `winget list --id` finds Store apps too.
            SourceKind::Msstore => {
                self.winget
                    .installed_version(&WingetId::try_from(target.id().to_string())?)
                    .await
            }
        }
    }
}

/// Backup sources for the queue: catalog alternates, the user's source settings, and for
/// updates a Chocolatey version newer than what's installed.
struct SystemFallback {
    settings: Arc<Mutex<SettingsStore>>,
    winget: WingetSource<SystemRunner>,
    choco: ChocoSource<SystemRunner>,
}

#[async_trait::async_trait]
impl Fallback for SystemFallback {
    async fn alternate(
        &self,
        op: &OpRequest,
        reason: FailureReason,
        reached_installing: bool,
    ) -> Option<SourceRef> {
        let s = self
            .settings
            .lock()
            .unwrap_or_else(|p| p.into_inner())
            .get();
        let on = fallback::Enabled {
            choco: s.choco.enabled && self.choco.exe().is_some(),
            store: s.store.enabled && crate::commands::system::shell_user_matches() == Some(true),
        };
        let alt = fallback::candidate(
            op.kind,
            &op.target,
            reason,
            reached_installing,
            Catalog::embedded(),
            on,
        )?;
        if op.kind == OpKind::Upgrade {
            let installed = self
                .winget
                .installed_version(&WingetId::try_from(op.target.id().to_string()).ok()?)
                .await
                .ok()??;
            let latest = self
                .choco
                .package(&ChocoId::try_from(alt.id().to_string()).ok()?)
                .await
                .ok()??
                .version?;
            if crate::versions::compare(&latest, &installed) != std::cmp::Ordering::Greater {
                return None;
            }
        }
        Some(alt)
    }
}

pub struct AppState {
    pub winget: WingetSource<SystemRunner>,
    pub choco: ChocoSource<SystemRunner>,
    pub settings: Arc<Mutex<SettingsStore>>,
    pub queue: Queue,
    pub icons: IconService,
    pub config_dir: PathBuf,
    pub rules: Mutex<RuleStore>,
    /// The last checked update list before rules (so rule changes don't need a new check).
    pub last_updates: Mutex<Option<Listing<UpgradeCandidate>>>,
    /// Category "more apps" lists, by category id (see commands::catalog).
    pub more_apps: tokio::sync::Mutex<
        std::collections::HashMap<String, (std::time::Instant, Vec<crate::catalog::AppEntry>)>,
    >,
}

impl AppState {
    pub fn new(config_dir: PathBuf, data_dir: PathBuf, app: tauri::AppHandle) -> Self {
        let paths = ExePaths::detect();
        let sizer = paths.winget.clone().and_then(|winget| {
            let http = reqwest::Client::builder()
                .https_only(true)
                .timeout(Duration::from_secs(10))
                .build()
                .ok()?;
            Some(Arc::new(WingetSizer {
                runner: Arc::new(SystemRunner),
                winget,
                http,
            }) as _)
        });
        let settings = Arc::new(Mutex::new(SettingsStore::open(
            config_dir.join("settings.json"),
        )));
        let queue = Queue::start(
            WorkerDeps {
                stream: Arc::new(SystemStreamRunner),
                winget: paths.winget.clone(),
                choco: paths.choco.clone(),
                gate: Arc::new(SystemMsiGate),
                sizer,
                winget_temp: std::env::temp_dir().join("WinGet"),
                sample_every: Duration::from_millis(250),
                gate_poll: Duration::from_millis(500),
                download_timeout: Duration::from_secs(60 * 60),
                elevated: crate::commands::system::is_elevated(),
                jobs_file: Some(data_dir.join("jobs.json")),
                fallback: Some(Arc::new(SystemFallback {
                    settings: settings.clone(),
                    winget: WingetSource::new(SystemRunner, paths.winget.clone()),
                    choco: ChocoSource::new(SystemRunner, paths.choco.clone()),
                })),
                verifier: Some(Arc::new(SystemVerifier {
                    winget: WingetSource::new(SystemRunner, paths.winget.clone()),
                    choco: ChocoSource::new(SystemRunner, paths.choco.clone()),
                })),
            },
            Arc::new(TauriSink(app)),
        );
        let icons = IconService::new(
            data_dir.join("icons"),
            Arc::new(SystemRunner),
            paths.winget.clone(),
        );
        Self {
            winget: WingetSource::new(SystemRunner, paths.winget),
            choco: ChocoSource::new(SystemRunner, paths.choco),
            settings,
            queue,
            icons,
            rules: Mutex::new(RuleStore::open(config_dir.join("rules.json"))),
            last_updates: Mutex::new(None),
            more_apps: tokio::sync::Mutex::new(Default::default()),
            config_dir,
        }
    }

    pub fn settings(&self) -> crate::settings::schema::Settings {
        self.settings.lock().expect("settings lock").get()
    }
}
