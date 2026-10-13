//! Rust-owned settings file. JS can only send a validated `SettingsPatch`; it never writes the
//! file. Writes are atomic (temp file + rename). A corrupt file is backed up and replaced with
//! defaults; a file from a newer app version is never overwritten (read-only defaults).

pub mod schema;

use crate::error::{AppError, AppResult};
use schema::{Settings, SettingsPatch, SCHEMA_VERSION};
use std::path::PathBuf;

pub struct SettingsStore {
    path: PathBuf,
    current: Settings,
    read_only: bool,
}

impl SettingsStore {
    pub fn open(path: PathBuf) -> Self {
        let (current, read_only) = load(&path);
        Self {
            path,
            current,
            read_only,
        }
    }

    pub fn get(&self) -> Settings {
        self.current.clone()
    }

    pub fn read_only(&self) -> bool {
        self.read_only
    }

    pub fn apply(&mut self, patch: SettingsPatch) -> AppResult<Settings> {
        if self.read_only {
            return Err(AppError::new(
                "settings",
                "settings were saved by a newer version of Quantum Updater and are read-only",
            ));
        }
        if let Some(h) = patch.check_interval_hours {
            if !(1..=168).contains(&h) {
                return Err(AppError::new(
                    "invalid_input",
                    "check interval must be 1 to 168 hours",
                ));
            }
        }
        let mut next = self.current.clone();
        macro_rules! set {
            ($($field:ident),*) => { $(if let Some(v) = patch.$field { next.$field = v; })* };
        }
        set!(
            theme,
            motion,
            check_interval_hours,
            notify_updates,
            start_at_login,
            start_in_tray,
            close_to_tray,
            show_unknown_versions,
            self_update
        );
        if let Some(v) = patch.choco_enabled {
            next.choco.enabled = v;
        }
        if let Some(v) = patch.choco_bootstrap_prompted {
            next.choco.bootstrap_prompted = v;
        }
        if let Some(v) = patch.store_enabled {
            next.store.enabled = v;
        }
        write_atomic(&self.path, &next)?;
        self.current = next;
        Ok(self.get())
    }
}

/// Returns (settings, read_only). Only a missing file means "start fresh"; any other read
/// problem (locked by antivirus, permissions, bad encoding) loads defaults read-only so the
/// user's real file is never overwritten.
fn load(path: &PathBuf) -> (Settings, bool) {
    let text = match std::fs::read_to_string(path) {
        Ok(t) => t,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return (Settings::default(), false),
        Err(_) => return (Settings::default(), true),
    };
    let value: serde_json::Value = match serde_json::from_str(&text) {
        Ok(v) => v,
        Err(_) => return backup_and_default(path),
    };
    let version = value
        .get("schemaVersion")
        .and_then(|v| v.as_u64())
        .unwrap_or(1);
    if version > u64::from(SCHEMA_VERSION) {
        return (Settings::default(), true);
    }
    match serde_json::from_value::<Settings>(value) {
        Ok(s) => (
            sanitize(Settings {
                schema_version: SCHEMA_VERSION,
                ..s
            }),
            false,
        ),
        Err(_) => backup_and_default(path),
    }
}

/// Values hand-edited out of range fall back to their defaults.
fn sanitize(mut s: Settings) -> Settings {
    if !(1..=168).contains(&s.check_interval_hours) {
        s.check_interval_hours = Settings::default().check_interval_hours;
    }
    s
}

/// Keep the unreadable file as `.bak`; if it can't be moved aside, stay read-only rather than
/// overwrite it later without a backup.
fn backup_and_default(path: &PathBuf) -> (Settings, bool) {
    let moved = std::fs::rename(path, path.with_extension("json.bak")).is_ok();
    (Settings::default(), !moved)
}

fn write_atomic(path: &std::path::Path, settings: &Settings) -> AppResult<()> {
    let json = serde_json::to_vec_pretty(settings).expect("settings serialize");
    crate::fsutil::write_atomic(path, &json)
        .map_err(|e| AppError::new("settings", format!("could not save settings: {e}")))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::settings::schema::{SettingsPatch, Theme};

    fn store_in(dir: &tempfile::TempDir) -> (SettingsStore, std::path::PathBuf) {
        let path = dir.path().join("settings.json");
        (SettingsStore::open(path.clone()), path)
    }

    #[test]
    fn missing_file_gives_defaults_and_first_apply_creates_it() {
        let dir = tempfile::tempdir().unwrap();
        let (mut s, path) = store_in(&dir);
        assert_eq!(s.get().check_interval_hours, 6);
        assert!(!path.exists());
        s.apply(SettingsPatch {
            theme: Some(Theme::Dark),
            ..Default::default()
        })
        .unwrap();
        assert!(path.exists());
        assert!(!dir.path().join("settings.json.tmp").exists());
        let reopened = SettingsStore::open(path);
        assert_eq!(reopened.get().theme, Theme::Dark);
    }

    #[test]
    fn invalid_interval_is_rejected_and_file_untouched() {
        let dir = tempfile::tempdir().unwrap();
        let (mut s, path) = store_in(&dir);
        s.apply(SettingsPatch {
            theme: Some(Theme::Light),
            ..Default::default()
        })
        .unwrap();
        let before = std::fs::read(&path).unwrap();
        for bad in [0, 200] {
            let err = s
                .apply(SettingsPatch {
                    check_interval_hours: Some(bad),
                    ..Default::default()
                })
                .unwrap_err();
            assert_eq!(err.code, "invalid_input");
        }
        assert_eq!(std::fs::read(&path).unwrap(), before);
        assert_eq!(s.get().check_interval_hours, 6);
    }

    #[test]
    fn corrupt_file_is_backed_up_and_defaults_used() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("settings.json");
        std::fs::write(&path, "{ not json").unwrap();
        let s = SettingsStore::open(path.clone());
        assert_eq!(s.get().check_interval_hours, 6);
        assert!(!s.read_only());
        assert_eq!(
            std::fs::read_to_string(dir.path().join("settings.json.bak")).unwrap(),
            "{ not json"
        );
    }

    #[test]
    fn newer_schema_is_read_only_and_never_overwritten() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("settings.json");
        let newer = r#"{"schemaVersion":99,"theme":"dark","futureThing":true}"#;
        std::fs::write(&path, newer).unwrap();
        let mut s = SettingsStore::open(path.clone());
        assert!(s.read_only());
        assert_eq!(s.get().theme, Theme::System);
        let err = s
            .apply(SettingsPatch {
                theme: Some(Theme::Light),
                ..Default::default()
            })
            .unwrap_err();
        assert_eq!(err.code, "settings");
        assert_eq!(std::fs::read_to_string(&path).unwrap(), newer);
    }

    /// Any read error other than NotFound must not lead to overwriting the file (review finding).
    #[test]
    fn unreadable_file_is_read_only() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("settings.json");
        std::fs::create_dir(&path).unwrap(); // reading a directory fails with a non-NotFound error
        let mut s = SettingsStore::open(path.clone());
        assert!(s.read_only());
        assert!(s.apply(SettingsPatch::default()).is_err());
        assert!(path.is_dir());
    }

    #[test]
    fn out_of_range_values_in_file_fall_back_to_defaults() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("settings.json");
        std::fs::write(
            &path,
            r#"{"schemaVersion":1,"checkIntervalHours":0,"theme":"dark"}"#,
        )
        .unwrap();
        let s = SettingsStore::open(path);
        assert_eq!(s.get().check_interval_hours, 6);
        assert_eq!(s.get().theme, Theme::Dark);
    }

    #[test]
    fn partial_file_fills_defaults() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("settings.json");
        std::fs::write(&path, r#"{"schemaVersion":1,"theme":"light"}"#).unwrap();
        let s = SettingsStore::open(path);
        assert_eq!(s.get().theme, Theme::Light);
        assert!(s.get().notify_updates);
        assert!(s.get().choco.enabled);
    }

    #[test]
    fn settings_json_is_camel_case() {
        let json = serde_json::to_value(crate::settings::schema::Settings::default()).unwrap();
        assert_eq!(json["schemaVersion"], 1);
        assert_eq!(json["checkIntervalHours"], 6);
        assert_eq!(json["selfUpdate"], "notify");
        assert_eq!(json["choco"]["bootstrapPrompted"], false);
    }
}
