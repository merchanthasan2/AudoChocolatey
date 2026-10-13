//! Long-lived services shared by all commands.

use crate::commands::jobs::TauriSink;
use crate::commands::library::Listing;
use crate::error::AppResult;
use crate::icons::IconService;
use crate::ids::{ChocoId, SourceKind, SourceRef, WingetId};
use crate::jobs::worker::{Queue, SystemMsiGate, Verifier, WingetSizer, WorkerDeps};
use crate::rules::RuleStore;
use crate::settings::SettingsStore;
use crate::sources::choco::ChocoSource;
use crate::sources::model::UpgradeCandidate;
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

pub struct AppState {
    pub winget: WingetSource<SystemRunner>,
    pub choco: ChocoSource<SystemRunner>,
    pub settings: Mutex<SettingsStore>,
    pub queue: Queue,
    pub icons: IconService,
    pub config_dir: PathBuf,
    pub rules: Mutex<RuleStore>,
    /// The last checked update list before rules (so rule changes don't need a new check).
    pub last_updates: Mutex<Option<Listing<UpgradeCandidate>>>,
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
            settings: Mutex::new(SettingsStore::open(config_dir.join("settings.json"))),
            queue,
            icons,
            rules: Mutex::new(RuleStore::open(config_dir.join("rules.json"))),
            last_updates: Mutex::new(None),
            config_dir,
        }
    }

    pub fn settings(&self) -> crate::settings::schema::Settings {
        self.settings.lock().expect("settings lock").get()
    }
}
