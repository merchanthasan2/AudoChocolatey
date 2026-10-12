//! Settings schema v1 (spec section 5.7). Missing fields fall back to defaults, so older files
//! keep loading as fields are added.

use serde::{Deserialize, Serialize};
use ts_rs::TS;

pub const SCHEMA_VERSION: u32 = 1;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS, Default)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum Theme {
    #[default]
    System,
    Light,
    Dark,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS, Default)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum Motion {
    #[default]
    System,
    Reduced,
    Full,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS, Default)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum SelfUpdate {
    #[default]
    Notify,
    Auto,
    Off,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase", default)]
#[ts(export)]
pub struct ChocoSettings {
    pub enabled: bool,
    pub bootstrap_prompted: bool,
}

impl Default for ChocoSettings {
    fn default() -> Self {
        Self {
            enabled: true,
            bootstrap_prompted: false,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase", default)]
#[ts(export)]
pub struct Settings {
    pub schema_version: u32,
    pub theme: Theme,
    pub motion: Motion,
    pub check_interval_hours: u32,
    pub notify_updates: bool,
    pub start_at_login: bool,
    pub start_in_tray: bool,
    pub close_to_tray: bool,
    pub show_unknown_versions: bool,
    pub self_update: SelfUpdate,
    pub choco: ChocoSettings,
    pub last_check_at: Option<String>,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            schema_version: SCHEMA_VERSION,
            theme: Theme::System,
            motion: Motion::System,
            check_interval_hours: 6,
            notify_updates: true,
            start_at_login: true,
            start_in_tray: true,
            close_to_tray: true,
            show_unknown_versions: false,
            self_update: SelfUpdate::Notify,
            choco: ChocoSettings::default(),
            last_check_at: None,
        }
    }
}

/// A partial update from the UI; only present fields change.
#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase", default)]
#[ts(export, optional_fields)]
pub struct SettingsPatch {
    pub theme: Option<Theme>,
    pub motion: Option<Motion>,
    pub check_interval_hours: Option<u32>,
    pub notify_updates: Option<bool>,
    pub start_at_login: Option<bool>,
    pub start_in_tray: Option<bool>,
    pub close_to_tray: Option<bool>,
    pub show_unknown_versions: Option<bool>,
    pub self_update: Option<SelfUpdate>,
    pub choco_enabled: Option<bool>,
    pub choco_bootstrap_prompted: Option<bool>,
}
