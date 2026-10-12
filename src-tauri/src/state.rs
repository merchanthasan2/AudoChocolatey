//! Long-lived services shared by all commands.

use crate::commands::jobs::TauriSink;
use crate::icons::IconService;
use crate::jobs::worker::{Queue, SystemMsiGate, WingetSizer, WorkerDeps};
use crate::settings::SettingsStore;
use crate::sources::choco::ChocoSource;
use crate::sources::process::{ExePaths, SystemRunner};
use crate::sources::stream::SystemStreamRunner;
use crate::sources::winget::WingetSource;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::Duration;

pub struct AppState {
    pub winget: WingetSource<SystemRunner>,
    pub choco: ChocoSource<SystemRunner>,
    pub settings: Mutex<SettingsStore>,
    pub queue: Queue,
    pub icons: IconService,
    pub config_dir: PathBuf,
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
            config_dir,
        }
    }

    pub fn settings(&self) -> crate::settings::schema::Settings {
        self.settings.lock().expect("settings lock").get()
    }
}
