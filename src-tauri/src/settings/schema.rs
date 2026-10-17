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

/// Whether shown updates install by themselves. Each app's own choice (rules) wins.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS, Default)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum AutoUpdate {
    #[default]
    Notify,
    Automatic,
}

/// Times of day when automatic updates wait. Minutes after midnight; `start > end` spans
/// midnight.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase", default)]
#[ts(export)]
pub struct QuietHours {
    pub enabled: bool,
    pub start: u16,
    pub end: u16,
}

impl Default for QuietHours {
    fn default() -> Self {
        Self {
            enabled: false,
            start: 9 * 60,
            end: 18 * 60,
        }
    }
}

/// The allowed "wait this many days after a new version appears" choices.
pub const MIN_AGE_CHOICES: [u32; 4] = [0, 1, 2, 7];

/// When automatic updates hold back. Manual updates ignore all of these.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase", default)]
#[ts(export)]
pub struct AutoRules {
    pub skip_on_battery: bool,
    pub skip_on_metered: bool,
    pub quiet_hours: QuietHours,
    pub min_age_days: u32,
}

impl Default for AutoRules {
    fn default() -> Self {
        Self {
            skip_on_battery: true,
            skip_on_metered: true,
            quiet_hours: QuietHours::default(),
            min_age_days: 2,
        }
    }
}

impl AutoRules {
    pub fn is_valid(&self) -> bool {
        self.quiet_hours.start < 1440
            && self.quiet_hours.end < 1440
            && MIN_AGE_CHOICES.contains(&self.min_age_days)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS, Default)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum BackupEvery {
    #[default]
    Weekly,
    Daily,
}

pub const BACKUP_KEEP_MAX: u32 = 30;

/// Saving the app list to a folder now and then. The folder is picked through a Windows
/// dialog in Rust; the UI can't set it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase", default)]
#[ts(export)]
pub struct BackupSettings {
    pub enabled: bool,
    pub folder: Option<String>,
    pub every: BackupEvery,
    pub keep: u32,
}

impl Default for BackupSettings {
    fn default() -> Self {
        Self {
            enabled: false,
            folder: None,
            every: BackupEvery::Weekly,
            keep: 5,
        }
    }
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
pub struct StoreSettings {
    pub enabled: bool,
}

impl Default for StoreSettings {
    fn default() -> Self {
        Self { enabled: true }
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
    /// Notify when a batch of changes finishes (window closed).
    pub notify_finished: bool,
    /// Notify when a change fails (window closed).
    pub notify_failed: bool,
    pub start_at_login: bool,
    pub start_in_tray: bool,
    pub close_to_tray: bool,
    pub show_unknown_versions: bool,
    pub self_update: SelfUpdate,
    pub auto_update: AutoUpdate,
    pub auto_rules: AutoRules,
    pub choco: ChocoSettings,
    /// Microsoft Store apps (through winget's Store source).
    pub store: StoreSettings,
    pub backup: BackupSettings,
    pub last_check_at: Option<String>,
    /// The one-time "still running in the tray" note was shown.
    pub tray_hint_shown: bool,
    /// The person answered "Keep apps updated in the background?" (start at sign-in is never
    /// set up before that).
    pub background_prompted: bool,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            schema_version: SCHEMA_VERSION,
            theme: Theme::System,
            motion: Motion::System,
            check_interval_hours: 6,
            notify_updates: true,
            notify_finished: false,
            notify_failed: true,
            start_at_login: false,
            start_in_tray: true,
            close_to_tray: true,
            show_unknown_versions: false,
            self_update: SelfUpdate::Notify,
            auto_update: AutoUpdate::Notify,
            auto_rules: AutoRules::default(),
            choco: ChocoSettings::default(),
            store: StoreSettings::default(),
            backup: BackupSettings::default(),
            last_check_at: None,
            tray_hint_shown: false,
            background_prompted: false,
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
    pub notify_finished: Option<bool>,
    pub notify_failed: Option<bool>,
    pub start_at_login: Option<bool>,
    pub start_in_tray: Option<bool>,
    pub close_to_tray: Option<bool>,
    pub show_unknown_versions: Option<bool>,
    pub self_update: Option<SelfUpdate>,
    pub auto_update: Option<AutoUpdate>,
    pub auto_rules: Option<AutoRules>,
    pub choco_enabled: Option<bool>,
    pub choco_bootstrap_prompted: Option<bool>,
    pub store_enabled: Option<bool>,
    pub background_prompted: Option<bool>,
    pub backup_enabled: Option<bool>,
    pub backup_every: Option<BackupEvery>,
    pub backup_keep: Option<u32>,
}
