//! My bundles, exporting and importing app lists, and scheduled backups. Files are picked with
//! Windows dialogs opened here in Rust, so no file path ever comes from the page. Imports are
//! held in Rust; the page only sends back which rows to install.

use crate::apps_file::{self, iso_utc, FileApp};
use crate::catalog::{AppEntry, Catalog};
use crate::commands::jobs::{gates, permitted, refuse_unelevated};
use crate::error::AppError;
use crate::ids::SourceKind;
use crate::jobs::model::now_ms;
use crate::settings::schema::Settings;
use crate::sources::model::{OpKind, OpRequest};
use crate::state::AppState;
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;
use std::path::PathBuf;
use tauri::{AppHandle, Manager, State};
use tauri_plugin_dialog::DialogExt;
use ts_rs::TS;

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct MyBundleView {
    pub id: String,
    pub name: String,
    pub apps: Vec<AppEntry>,
}

/// An app the page asks to save; validated like an imported row.
#[derive(Debug, Clone, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct BundleAppIn {
    pub source: SourceKind,
    pub id: String,
    pub name: String,
}

#[derive(Debug, Clone, Deserialize, TS)]
#[serde(rename_all = "camelCase", tag = "kind")]
#[ts(export)]
pub enum ExportWhat {
    /// Every installed app a source can reinstall.
    Installed,
    Bundle {
        id: String,
    },
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ImportRow {
    pub index: u32,
    pub entry: AppEntry,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ImportPreview {
    pub token: String,
    pub name: Option<String>,
    pub rows: Vec<ImportRow>,
    /// Rows left out (unreadable, an id that isn't valid, or duplicates).
    pub skipped: u32,
}

pub struct PendingImport {
    token: String,
    name: Option<String>,
    apps: Vec<FileApp>,
}

fn views(state: &AppState) -> Vec<MyBundleView> {
    let cat = Catalog::embedded();
    state
        .bundles
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .list()
        .iter()
        .map(|b| MyBundleView {
            id: b.id.clone(),
            name: b.name.clone(),
            apps: b.apps.iter().map(|a| apps_file::entry(a, cat)).collect(),
        })
        .collect()
}

fn short(s: &str, max: usize) -> Result<(), AppError> {
    if s.len() > max {
        return Err(AppError::new("invalid_input", "too long"));
    }
    Ok(())
}

#[tauri::command]
pub fn my_bundles(state: State<'_, AppState>) -> Vec<MyBundleView> {
    views(&state)
}

#[tauri::command]
pub fn add_to_bundle(
    state: State<'_, AppState>,
    bundle: Option<String>,
    name: Option<String>,
    apps: Vec<BundleAppIn>,
) -> Result<Vec<MyBundleView>, AppError> {
    if apps.len() > apps_file::MAX_APPS {
        return Err(AppError::new("invalid_input", "too many apps"));
    }
    short(bundle.as_deref().unwrap_or(""), 100)?;
    short(name.as_deref().unwrap_or(""), 400)?;
    let cat = Catalog::embedded();
    let apps: Vec<FileApp> = apps
        .into_iter()
        .filter_map(|a| apps_file::validate(a.source, &a.id, &a.name, cat))
        .collect();
    state
        .bundles
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .add(bundle.as_deref(), name.as_deref(), apps, now_ms())?;
    Ok(views(&state))
}

#[tauri::command]
pub fn remove_from_bundle(
    state: State<'_, AppState>,
    bundle: String,
    key: String,
) -> Result<Vec<MyBundleView>, AppError> {
    short(&bundle, 100)?;
    short(&key, 300)?;
    state
        .bundles
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .remove_app(&bundle, &key)?;
    Ok(views(&state))
}

#[tauri::command]
pub fn delete_bundle(
    state: State<'_, AppState>,
    bundle: String,
) -> Result<Vec<MyBundleView>, AppError> {
    short(&bundle, 100)?;
    state
        .bundles
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .delete(&bundle)?;
    Ok(views(&state))
}

/// Installed apps a source can reinstall, as list rows. Refused when a source couldn't be read,
/// so a backup never silently leaves apps out.
async fn installed_apps(state: &AppState) -> Result<Vec<FileApp>, AppError> {
    let listing = crate::commands::library::collect_installed(state).await;
    if !listing.source_errors.is_empty() {
        return Err(AppError::new(
            "source",
            "Some installed apps couldn't be read, so the list would be incomplete. Try again in a moment.",
        ));
    }
    let cat = Catalog::embedded();
    let apps = listing
        .items
        .iter()
        .filter_map(|p| apps_file::validate(p.source?, &p.id, &p.name, cat))
        .collect();
    Ok(apps_file::dedupe(apps).0)
}

fn safe_file_name(name: &str) -> String {
    let n: String = name
        .chars()
        .map(|c| {
            if c.is_alphanumeric() || c == ' ' || c == '-' {
                c
            } else {
                '_'
            }
        })
        .take(60)
        .collect();
    let n = n.trim();
    if n.is_empty() {
        "My apps".into()
    } else {
        n.into()
    }
}

/// Runs a blocking Windows dialog off the async threads, in front of the main window.
async fn dialog<T: Send + 'static>(
    app: &AppHandle,
    f: impl FnOnce(tauri_plugin_dialog::FileDialogBuilder<tauri::Wry>) -> Option<T> + Send + 'static,
) -> Option<T> {
    let mut b = app.dialog().file();
    if let Some(w) = app.get_webview_window("main") {
        b = b.set_parent(&w);
    }
    tauri::async_runtime::spawn_blocking(move || f(b))
        .await
        .ok()
        .flatten()
}

#[derive(Debug, Clone, Copy, Deserialize, TS)]
#[serde(rename_all = "lowercase")]
#[ts(export)]
pub enum ListPage {
    Updates,
    Installed,
}

/// One shown row, with the friendly source name the page shows.
#[derive(Debug, Clone, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct CsvRow {
    pub name: String,
    pub id: String,
    pub version: String,
    #[ts(optional)]
    pub new_version: Option<String>,
    pub source: String,
    #[ts(optional)]
    pub released: Option<String>,
}

/// Saves the shown list as a spreadsheet (CSV). `None` when they cancelled.
#[tauri::command]
pub async fn export_csv(
    app: AppHandle,
    page: ListPage,
    rows: Vec<CsvRow>,
) -> Result<Option<u32>, AppError> {
    if rows.len() > crate::csv::MAX_ROWS {
        return Err(AppError::new("invalid_input", "too many rows"));
    }
    let (header, cells): (&[&str], Vec<Vec<String>>) = match page {
        ListPage::Updates => (
            &["App", "Package", "Installed", "New version", "Source", "Released"],
            rows.iter()
                .map(|r| {
                    vec![
                        r.name.clone(),
                        r.id.clone(),
                        r.version.clone(),
                        r.new_version.clone().unwrap_or_default(),
                        r.source.clone(),
                        r.released.clone().unwrap_or_default(),
                    ]
                })
                .collect(),
        ),
        ListPage::Installed => (
            &["App", "Package", "Version", "Source"],
            rows.iter()
                .map(|r| vec![r.name.clone(), r.id.clone(), r.version.clone(), r.source.clone()])
                .collect(),
        ),
    };
    let text = crate::csv::to_csv(header, &cells);
    let (y, m, d) = crate::background::backup::local_date();
    let what = match page {
        ListPage::Updates => "Updates",
        ListPage::Installed => "Installed",
    };
    let file = format!("Quantum Updater - {what} {y:04}-{m:02}-{d:02}.csv");
    let Some(path) = dialog(&app, move |b| {
        b.add_filter("Spreadsheet (CSV)", &["csv"])
            .set_file_name(file)
            .blocking_save_file()?
            .into_path()
            .ok()
    })
    .await
    else {
        return Ok(None);
    };
    crate::fsutil::write_atomic(&path, text.as_bytes())
        .map_err(|e| AppError::new("io", format!("Couldn't save the spreadsheet: {e}")))?;
    Ok(Some(rows.len() as u32))
}

/// Saves an app list where the person chooses. `None` when they cancelled.
#[tauri::command]
pub async fn export_apps(
    app: AppHandle,
    state: State<'_, AppState>,
    what: ExportWhat,
) -> Result<Option<u32>, AppError> {
    let (name, apps) = match what {
        ExportWhat::Installed => ("My apps".to_string(), installed_apps(&state).await?),
        ExportWhat::Bundle { id } => {
            let b = state
                .bundles
                .lock()
                .unwrap_or_else(|p| p.into_inner())
                .get(&id)
                .cloned()
                .ok_or_else(|| AppError::new("invalid_input", "That bundle no longer exists."))?;
            (b.name, b.apps)
        }
    };
    let file = format!("{}.json", safe_file_name(&name));
    let Some(path) = dialog(&app, move |b| {
        b.add_filter("App list", &["json"])
            .set_file_name(file)
            .blocking_save_file()?
            .into_path()
            .ok()
    })
    .await
    else {
        return Ok(None);
    };
    let bytes = apps_file::to_json(Some(&name), &apps, iso_utc(now_ms()));
    crate::fsutil::write_atomic(&path, &bytes)
        .map_err(|e| AppError::new("io", format!("Couldn't save the app list: {e}")))?;
    Ok(Some(apps.len() as u32))
}

fn new_token() -> String {
    use std::hash::{BuildHasher, Hasher};
    let mut h = std::collections::hash_map::RandomState::new().build_hasher();
    h.write_u64(now_ms());
    format!("{:016x}", h.finish())
}

fn preview(p: &PendingImport) -> ImportPreview {
    let cat = Catalog::embedded();
    ImportPreview {
        token: p.token.clone(),
        name: p.name.clone(),
        rows: p
            .apps
            .iter()
            .enumerate()
            .map(|(i, a)| ImportRow {
                index: i as u32,
                entry: apps_file::entry(a, cat),
            })
            .collect(),
        skipped: 0,
    }
}

/// Opens an app list and returns what it would install. `None` when they cancelled.
#[tauri::command]
pub async fn import_pick(
    app: AppHandle,
    state: State<'_, AppState>,
) -> Result<Option<ImportPreview>, AppError> {
    let Some(path) = dialog(&app, |b| {
        b.add_filter("App list", &["json"])
            .blocking_pick_file()?
            .into_path()
            .ok()
    })
    .await
    else {
        return Ok(None);
    };
    let bytes = read_capped(&path)?;
    let parsed = apps_file::parse(&bytes, Catalog::embedded())
        .map_err(|e| AppError::new("invalid_input", e.message()))?;
    let pending = PendingImport {
        token: new_token(),
        name: parsed.name,
        apps: parsed.apps,
    };
    let out = ImportPreview {
        skipped: parsed.skipped,
        ..preview(&pending)
    };
    *state
        .pending_import
        .lock()
        .unwrap_or_else(|p| p.into_inner()) = Some(pending);
    Ok(Some(out))
}

fn read_capped(path: &PathBuf) -> Result<Vec<u8>, AppError> {
    let too_big = || AppError::new("invalid_input", apps_file::ParseError::TooLarge.message());
    let meta = std::fs::metadata(path)
        .map_err(|e| AppError::new("io", format!("Couldn't open that file: {e}")))?;
    if meta.len() > apps_file::MAX_BYTES {
        return Err(too_big());
    }
    use std::io::Read;
    let mut buf = Vec::new();
    std::fs::File::open(path)
        .and_then(|f| f.take(apps_file::MAX_BYTES + 1).read_to_end(&mut buf))
        .map_err(|e| AppError::new("io", format!("Couldn't open that file: {e}")))?;
    if buf.len() as u64 > apps_file::MAX_BYTES {
        return Err(too_big());
    }
    Ok(buf)
}

/// The held rows the page picked, in order, once each; anything else is ignored.
pub fn chosen<'a>(apps: &'a [FileApp], indices: &[u32]) -> Vec<&'a FileApp> {
    let mut seen = BTreeSet::new();
    indices
        .iter()
        .filter(|&&i| seen.insert(i))
        .filter_map(|&i| apps.get(i as usize))
        .collect()
}

pub fn install_ops(apps: &[&FileApp], cat: &Catalog) -> Vec<(OpRequest, String)> {
    apps.iter()
        .filter_map(|a| {
            Some((
                OpRequest {
                    kind: OpKind::Install,
                    target: apps_file::install_target(a, cat)?,
                    version: None,
                },
                a.name.clone(),
            ))
        })
        .collect()
}

fn with_pending<T>(
    slot: &std::sync::Mutex<Option<PendingImport>>,
    token: &str,
    f: impl FnOnce(&PendingImport) -> T,
) -> Result<T, AppError> {
    let guard = slot.lock().unwrap_or_else(|p| p.into_inner());
    match guard.as_ref() {
        Some(p) if p.token == token => Ok(f(p)),
        _ => Err(AppError::new(
            "invalid_input",
            "That app list is no longer open. Open it again.",
        )),
    }
}

/// Installs the picked rows of the open import through the same checks as any install.
#[tauri::command]
pub fn import_apply(
    state: State<'_, AppState>,
    token: String,
    indices: Vec<u32>,
) -> Result<Vec<String>, AppError> {
    refuse_unelevated(crate::commands::system::is_elevated())?;
    let cat = Catalog::embedded();
    let ops = with_pending(&state.pending_import, &token, |p| {
        install_ops(&chosen(&p.apps, &indices), cat)
    })?;
    Ok(state.queue.enqueue(permitted(ops, gates(&state))))
}

/// Keeps the picked rows of the open import as one of My bundles.
#[tauri::command]
pub fn import_keep(
    state: State<'_, AppState>,
    token: String,
    indices: Vec<u32>,
    name: String,
) -> Result<Vec<MyBundleView>, AppError> {
    short(&name, 400)?;
    let (apps, file_name) = with_pending(&state.pending_import, &token, |p| {
        (
            chosen(&p.apps, &indices)
                .into_iter()
                .cloned()
                .collect::<Vec<_>>(),
            p.name.clone(),
        )
    })?;
    let name = if name.trim().is_empty() {
        file_name.unwrap_or_else(|| "Imported apps".into())
    } else {
        name
    };
    state
        .bundles
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .add(None, Some(&name), apps, now_ms())?;
    Ok(views(&state))
}

/// Picks the backup folder with a Windows dialog; picking one turns backups on.
#[tauri::command]
pub async fn pick_backup_folder(
    app: AppHandle,
    state: State<'_, AppState>,
) -> Result<Option<Settings>, AppError> {
    let Some(folder) = dialog(&app, |b| b.blocking_pick_folder()?.into_path().ok()).await else {
        return Ok(None);
    };
    let s = state
        .settings
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .set_backup_folder(&folder)?;
    Ok(Some(s))
}

/// Saves a backup now. Returns the file name.
#[tauri::command]
pub async fn backup_now(state: State<'_, AppState>) -> Result<String, AppError> {
    backup_now_inner(&state).await
}

pub async fn backup_now_inner(state: &AppState) -> Result<String, AppError> {
    let b = state.settings().backup;
    let Some(folder) = b.folder.filter(|_| b.enabled) else {
        return Err(AppError::new(
            "invalid_input",
            "Pick a folder for the backups first.",
        ));
    };
    let apps = installed_apps(state).await?;
    let date = crate::background::backup::local_date();
    let at = iso_utc(now_ms());
    tauri::async_runtime::spawn_blocking(move || {
        crate::background::backup::write(std::path::Path::new(&folder), &apps, date, at, b.keep)
    })
    .await
    .map_err(|e| AppError::new("io", e.to_string()))?
    .map_err(|e| AppError::new("io", format!("Couldn't save the backup: {e}")))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn app(id: &str) -> FileApp {
        apps_file::validate(SourceKind::Winget, id, id, Catalog::embedded()).unwrap()
    }

    #[test]
    fn only_held_rows_are_installed_once_each() {
        let apps = vec![app("A.A"), app("B.B"), app("C.C")];
        let picked: Vec<&str> = chosen(&apps, &[2, 0, 2, 9, u32::MAX])
            .iter()
            .map(|a| a.id.as_str())
            .collect();
        assert_eq!(picked, vec!["C.C", "A.A"]);
        let ops = install_ops(&chosen(&apps, &[1]), Catalog::embedded());
        assert_eq!(ops.len(), 1);
        assert_eq!(ops[0].0.kind, OpKind::Install);
        assert_eq!(ops[0].0.target.id(), "B.B");
        assert!(ops[0].0.version.is_none(), "never a pinned version");
    }

    #[test]
    fn a_stale_or_missing_token_is_refused() {
        let slot = std::sync::Mutex::new(None);
        assert!(with_pending(&slot, "abc", |_| ()).is_err());
        *slot.lock().unwrap() = Some(PendingImport {
            token: "new".into(),
            name: None,
            apps: vec![app("A.A")],
        });
        assert!(with_pending(&slot, "old", |_| ()).is_err());
        assert_eq!(with_pending(&slot, "new", |p| p.apps.len()).unwrap(), 1);
    }

    #[test]
    fn file_names_are_safe() {
        assert_eq!(safe_file_name("Work PC"), "Work PC");
        assert_eq!(safe_file_name("..\\..\\evil:name"), "______evil_name");
        assert_eq!(safe_file_name("  "), "My apps");
    }
}
