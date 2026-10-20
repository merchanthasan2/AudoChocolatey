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

    /// Records that the tray note was shown (Rust-only flag, not part of the UI patch).
    pub fn mark_tray_hint_shown(&mut self) {
        if self.current.tray_hint_shown || self.read_only {
            return;
        }
        let mut next = self.current.clone();
        next.tray_hint_shown = true;
        if write_atomic(&self.path, &next).is_ok() {
            self.current = next;
        }
    }

    /// Turns "always remove new desktop icons" on or off for one app.
    pub fn set_remove_shortcuts(
        &mut self,
        target: &crate::ids::SourceRef,
        on: bool,
    ) -> AppResult<Settings> {
        if self.read_only {
            return Err(AppError::new(
                "settings",
                "settings were saved by a newer version of Quantum Updater and are read-only",
            ));
        }
        let mut next = self.current.clone();
        next.remove_new_shortcuts.retain(|t| t != target);
        if on {
            if next.remove_new_shortcuts.len() >= schema::SHORTCUT_RULES_MAX {
                return Err(AppError::new("invalid_input", "too many apps"));
            }
            next.remove_new_shortcuts.push(target.clone());
        }
        write_atomic(&self.path, &next)?;
        self.current = next;
        Ok(self.get())
    }

    /// The backup folder, from a Windows folder dialog (never from the UI). Picking one turns
    /// backups on.
    pub fn set_backup_folder(&mut self, folder: &std::path::Path) -> AppResult<Settings> {
        if self.read_only {
            return Err(AppError::new(
                "settings",
                "settings were saved by a newer version of Quantum Updater and are read-only",
            ));
        }
        let Some(f) = folder.to_str().filter(|_| folder.is_absolute()) else {
            return Err(AppError::new("invalid_input", "That folder can't be used."));
        };
        let mut next = self.current.clone();
        next.backup.folder = Some(f.to_string());
        next.backup.enabled = true;
        write_atomic(&self.path, &next)?;
        self.current = next;
        Ok(self.get())
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
        if patch.auto_rules.is_some_and(|r| !r.is_valid()) {
            return Err(AppError::new(
                "invalid_input",
                "quiet hours or waiting days are out of range",
            ));
        }
        if patch
            .backup_keep
            .is_some_and(|k| !(1..=schema::BACKUP_KEEP_MAX).contains(&k))
        {
            return Err(AppError::new("invalid_input", "keep 1 to 30 backups"));
        }
        if patch
            .remove_new_shortcuts
            .as_ref()
            .is_some_and(|l| l.len() > schema::SHORTCUT_RULES_MAX)
        {
            return Err(AppError::new("invalid_input", "too many apps"));
        }
        if patch.backup_enabled == Some(true) && self.current.backup.folder.is_none() {
            return Err(AppError::new(
                "invalid_input",
                "Pick a folder for the backups first.",
            ));
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
            notify_finished,
            notify_failed,
            start_at_login,
            start_in_tray,
            close_to_tray,
            show_unknown_versions,
            self_update,
            auto_update,
            auto_rules,
            restore_point,
            lists,
            remove_new_shortcuts
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
        if let Some(v) = patch.background_prompted {
            next.background_prompted = v;
        }
        if let Some(v) = patch.backup_enabled {
            next.backup.enabled = v;
        }
        if let Some(v) = patch.backup_every {
            next.backup.every = v;
        }
        if let Some(v) = patch.backup_keep {
            next.backup.keep = v;
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
    if !(1..=schema::BACKUP_KEEP_MAX).contains(&s.backup.keep) {
        s.backup.keep = schema::BackupSettings::default().keep;
    }
    if !s
        .backup
        .folder
        .as_deref()
        .is_some_and(|f| std::path::Path::new(f).is_absolute())
    {
        s.backup.folder = None;
        s.backup.enabled = false;
    }
    if !s.auto_rules.is_valid() {
        s.auto_rules = Default::default();
    }
    // Start at sign-in is off until the person says yes (older files defaulted it on).
    if !s.background_prompted {
        s.start_at_login = false;
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
    fn start_at_sign_in_stays_off_until_the_person_said_yes() {
        let dir = tempfile::tempdir().unwrap();
        let (_, path) = store_in(&dir);
        std::fs::write(&path, r#"{"schemaVersion":1,"startAtLogin":true}"#).unwrap();
        assert!(!SettingsStore::open(path.clone()).get().start_at_login);
        std::fs::write(
            &path,
            r#"{"schemaVersion":1,"startAtLogin":true,"backgroundPrompted":true}"#,
        )
        .unwrap();
        assert!(SettingsStore::open(path).get().start_at_login);
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
    fn automatic_update_choices_are_checked() {
        use schema::{AutoRules, AutoUpdate, QuietHours};
        let dir = tempfile::tempdir().unwrap();
        let (mut s, _) = store_in(&dir);
        assert_eq!(s.get().auto_update, AutoUpdate::Notify, "off until chosen");
        assert_eq!(s.get().auto_rules.min_age_days, 2);
        assert!(s.get().auto_rules.skip_on_battery && s.get().auto_rules.skip_on_metered);
        let bad = [
            AutoRules {
                min_age_days: 3,
                ..Default::default()
            },
            AutoRules {
                quiet_hours: QuietHours {
                    enabled: true,
                    start: 1440,
                    end: 0,
                },
                ..Default::default()
            },
        ];
        for r in bad {
            let err = s
                .apply(SettingsPatch {
                    auto_rules: Some(r),
                    ..Default::default()
                })
                .unwrap_err();
            assert_eq!(err.code, "invalid_input");
        }
        let good = AutoRules {
            min_age_days: 7,
            quiet_hours: QuietHours {
                enabled: true,
                start: 22 * 60,
                end: 7 * 60,
            },
            ..Default::default()
        };
        let next = s
            .apply(SettingsPatch {
                auto_update: Some(AutoUpdate::Automatic),
                auto_rules: Some(good),
                ..Default::default()
            })
            .unwrap();
        assert_eq!(next.auto_update, AutoUpdate::Automatic);
        assert_eq!(next.auto_rules, good);
    }

    #[test]
    fn list_choices_default_and_survive_old_files() {
        use schema::{ListSort, ListView};
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("settings.json");
        std::fs::write(&path, r#"{"schemaVersion":1,"theme":"dark"}"#).unwrap();
        let s = SettingsStore::open(path.clone()).get();
        assert_eq!(s.lists.updates.sort, ListSort::Suggested);
        assert_eq!(s.lists.installed.sort, ListSort::Name);
        assert!(s.lists.installed.hide_components);
        std::fs::write(
            &path,
            r#"{"schemaVersion":1,"theme":"dark","lists":{"installed":{"view":"grid","sort":"bogus"}}}"#,
        )
        .unwrap();
        // A bad value resets the list choices only; the rest of the file is kept.
        let s = SettingsStore::open(path).get();
        assert_eq!(s.lists.installed.view, ListView::List);
        assert_eq!(s.theme, schema::Theme::Dark);
    }

    #[test]
    fn backups_need_a_folder_picked_in_rust() {
        let dir = tempfile::tempdir().unwrap();
        let (mut s, path) = store_in(&dir);
        let on = SettingsPatch {
            backup_enabled: Some(true),
            ..Default::default()
        };
        assert_eq!(s.apply(on.clone()).unwrap_err().code, "invalid_input");
        assert!(s
            .set_backup_folder(std::path::Path::new("relative"))
            .is_err());
        let next = s.set_backup_folder(dir.path()).unwrap();
        assert!(next.backup.enabled);
        assert!(s.apply(on).is_ok());
        for bad in [0, 31] {
            let p = SettingsPatch {
                backup_keep: Some(bad),
                ..Default::default()
            };
            assert!(s.apply(p).is_err());
        }
        // A hand-edited relative folder is dropped and backups switch off.
        std::fs::write(
            &path,
            r#"{"schemaVersion":1,"backup":{"enabled":true,"folder":"x/y","keep":99}}"#,
        )
        .unwrap();
        let b = SettingsStore::open(path).get().backup;
        assert!(!b.enabled && b.folder.is_none() && b.keep == 5);
    }

    #[test]
    fn hand_edited_waiting_days_fall_back_to_the_default() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("settings.json");
        std::fs::write(
            &path,
            r#"{"schemaVersion":1,"autoUpdate":"automatic","autoRules":{"minAgeDays":30}}"#,
        )
        .unwrap();
        let s = SettingsStore::open(path);
        assert_eq!(s.get().auto_update, schema::AutoUpdate::Automatic);
        assert_eq!(s.get().auto_rules, schema::AutoRules::default());
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
