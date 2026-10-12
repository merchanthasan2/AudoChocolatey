//! Long-lived services shared by all commands.

use crate::settings::SettingsStore;
use crate::sources::choco::ChocoSource;
use crate::sources::process::{ExePaths, SystemRunner};
use crate::sources::winget::WingetSource;
use std::path::PathBuf;
use std::sync::Mutex;

pub struct AppState {
    pub winget: WingetSource<SystemRunner>,
    pub choco: ChocoSource<SystemRunner>,
    pub settings: Mutex<SettingsStore>,
    pub config_dir: PathBuf,
}

impl AppState {
    pub fn new(config_dir: PathBuf) -> Self {
        let paths = ExePaths::detect();
        Self {
            winget: WingetSource::new(SystemRunner, paths.winget),
            choco: ChocoSource::new(SystemRunner, paths.choco),
            settings: Mutex::new(SettingsStore::open(config_dir.join("settings.json"))),
            config_dir,
        }
    }

    pub fn settings(&self) -> crate::settings::schema::Settings {
        self.settings.lock().expect("settings lock").get()
    }
}
