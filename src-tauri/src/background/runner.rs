//! The background check loop: wakes every few minutes (so sleep and clock changes are handled),
//! checks when due, updates the tray and the Updates page, starts updates the person set to
//! Automatic, notifies about new updates while the window is hidden, and saves scheduled
//! backups of the app list.

use super::auto::{may_auto_install, pick, read_probe, remember_seen, update_key, PickInput};
use super::schedule::{diff_new, due, BackgroundState};
use crate::catalog::Catalog;
use crate::commands::jobs::{gates, permitted, upgrades_to_ops};
use crate::jobs::model::now_ms;
use crate::settings::schema::Settings;
use crate::sources::model::UpgradeCandidate;
use crate::state::AppState;
use std::collections::{BTreeMap, BTreeSet};
use std::path::PathBuf;
use std::time::Duration;
use tauri::{AppHandle, Emitter, Manager};

const TICK: Duration = Duration::from_secs(5 * 60);
/// A check waits while a job runs, but not forever.
const MAX_DEFER_MS: u64 = 30 * 60_000;
/// A failed backup (folder gone, list unreadable) tries again after this long.
const BACKUP_RETRY_MS: u64 = 60 * 60_000;

/// Wakes the loop early ("Check now", interval changed).
pub struct Wake(pub tokio::sync::Notify);

pub fn start(app: AppHandle, data_dir: PathBuf) {
    app.manage(Wake(tokio::sync::Notify::new()));
    let launched_at = now_ms();
    tauri::async_runtime::spawn(async move {
        let path = data_dir.join("background.json");
        let mut deferred_since: Option<u64> = None;
        loop {
            let wake = &app.state::<Wake>().0;
            tokio::select! {
                _ = tokio::time::sleep(TICK) => {}
                _ = wake.notified() => {}
            }
            let state = app.state::<AppState>();
            if state.queue.is_paused() {
                continue;
            }
            let settings = state.settings();
            let mut saved = BackgroundState::load(&path);
            let before = saved.clone();
            let now = now_ms();
            let last = saved.last_check_at.max(state.last_check());
            let busy = state
                .queue
                .snapshot()
                .iter()
                .any(|j| !j.phase.is_finished());
            let check = due(now, launched_at, last, settings.check_interval_hours)
                && !(busy && now - *deferred_since.get_or_insert(now) < MAX_DEFER_MS);

            let cat = Catalog::embedded();
            let mut ready: Option<(BTreeMap<String, String>, BTreeSet<String>)> = None;
            if check {
                deferred_since = None;
                let listing = crate::commands::library::collect_updates(&state).await;
                super::shell::set_count(&app, listing.items.len());
                let _ = app.emit("updates://changed", &listing);
                crate::commands::library::refresh_update_info(app.clone(), listing.items.clone());
                let current: BTreeMap<String, String> = listing
                    .items
                    .iter()
                    .filter_map(|u| Some((update_key(u, cat)?, u.name.clone())))
                    .collect();
                let keys: BTreeSet<String> = current.keys().cloned().collect();
                remember_seen(&mut saved.first_seen, &keys, now);
                saved.auto_tried.retain(|k| keys.contains(k));
                saved.last_check_at = Some(now);
                check_sites(&state, &listing.items, &keys).await;
                ready = Some((current, keys));
            }

            // Every tick, not only after a check: a hold (battery, quiet hours) clears within
            // minutes. Uses the last checked list; nothing runs before the first check.
            let started = if state.has_checked() {
                let items = crate::commands::library::current_updates(&state).items;
                start_automatic(&state, &items, &settings, &saved, now).await
            } else {
                BTreeSet::new()
            };
            saved.auto_tried.extend(started.iter().cloned());

            if let Some((current, keys)) = ready {
                // Updates that just started by themselves aren't announced as "ready".
                let new: Vec<String> = diff_new(&saved.notified, &keys)
                    .into_iter()
                    .filter(|k| !started.contains(k))
                    .collect();
                if !new.is_empty() && settings.notify_updates && super::shell::should_notify(&app) {
                    let names: Vec<String> =
                        new.iter().filter_map(|k| current.get(k).cloned()).collect();
                    let (title, body) = super::toast::update_lines(&names);
                    super::toast::show(&title, &body);
                }
                saved.notified = keys;
            }

            if settings.backup.enabled
                && super::backup::due(now, saved.last_backup_at, settings.backup.every)
                && saved
                    .backup_failed_at
                    .is_none_or(|f| now.saturating_sub(f) >= BACKUP_RETRY_MS || f > now)
            {
                match crate::commands::bundles::backup_now_inner(&state).await {
                    Ok(_) => {
                        saved.last_backup_at = Some(now);
                        saved.backup_failed_at = None;
                    }
                    Err(_) => saved.backup_failed_at = Some(now),
                }
            }

            if saved != before {
                let _ = saved.save(&path);
            }
        }
    });
}

/// Queues the shown updates that may install by themselves now, through the same checks a
/// click would pass. Returns their update keys (empty when nothing qualified or a condition
/// holds).
async fn start_automatic(
    state: &AppState,
    items: &[UpgradeCandidate],
    settings: &Settings,
    saved: &BackgroundState,
    now: u64,
) -> BTreeSet<String> {
    let cat = Catalog::embedded();
    let rules = state
        .rules
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .rules()
        .to_vec();
    let sites = state
        .sites
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .all_checked()
        .clone();
    let picked = pick(
        items,
        &PickInput {
            rules: &rules,
            mode: settings.auto_update,
            min_age_days: settings.auto_rules.min_age_days,
            first_seen: &saved.first_seen,
            tried: &saved.auto_tried,
            sites: &sites,
            now,
        },
        cat,
    );
    if picked.is_empty() {
        return BTreeSet::new();
    }
    let probe = tauri::async_runtime::spawn_blocking(read_probe)
        .await
        .unwrap_or_default();
    if may_auto_install(&settings.auto_rules, &probe).is_err() {
        return BTreeSet::new();
    }
    let g = gates(state);
    let mut keys = BTreeSet::new();
    let mut ops = Vec::new();
    for c in picked {
        let op = permitted(upgrades_to_ops(vec![c.clone()]), g);
        if let (false, Some(k)) = (op.is_empty(), update_key(&c, cat)) {
            keys.insert(k);
            ops.extend(op);
        }
    }
    state.queue.enqueue(ops);
    keys
}

/// Looks at which site new Microsoft catalog updates download from (at most 50 per check,
/// three at a time). An app with no usual site yet takes the one its installed version used,
/// so the first look isn't simply trusted.
async fn check_sites(state: &AppState, items: &[UpgradeCandidate], live: &BTreeSet<String>) {
    use crate::ids::{PkgVersion, SourceKind, WingetId};
    use crate::sites::{judge, site_of, Check};
    let cat = Catalog::embedded();
    type Todo = (
        String,
        String,
        WingetId,
        Option<PkgVersion>,
        Option<PkgVersion>,
    );
    let todo: Vec<Todo> = {
        let s = state.sites.lock().unwrap_or_else(|p| p.into_inner());
        items
            .iter()
            .filter(|c| c.source == SourceKind::Winget)
            .filter_map(|c| {
                let uk = update_key(c, cat)?;
                if !matches!(s.checked(&uk), None | Some(Check::Error)) {
                    return None;
                }
                let rk = uk.rsplit_once('@')?.0.to_string();
                Some((
                    rk,
                    uk,
                    WingetId::try_from(c.id.clone()).ok()?,
                    PkgVersion::try_from(c.installed_version.clone()).ok(),
                    PkgVersion::try_from(c.available_version.clone()).ok(),
                ))
            })
            .take(50)
            .collect()
    };
    let lane = |n: usize| {
        let mine: Vec<&Todo> = todo.iter().skip(n).step_by(3).collect();
        async move {
            for (rk, uk, id, installed, available) in mine {
                let usual = state
                    .sites
                    .lock()
                    .unwrap_or_else(|p| p.into_inner())
                    .baseline(rk)
                    .map(String::from);
                let from_installed = match (&usual, installed) {
                    (None, Some(v)) => state
                        .winget
                        .installer_url(id, Some(v))
                        .await
                        .ok()
                        .flatten()
                        .and_then(|u| site_of(&u)),
                    _ => None,
                };
                let base = usual.or_else(|| from_installed.clone());
                let (found, seed) = match state.winget.installer_url(id, available.as_ref()).await {
                    Err(_) => (Check::Error, None),
                    Ok(url) => judge(base.as_deref(), url.as_deref().and_then(site_of).as_deref()),
                };
                state
                    .sites
                    .lock()
                    .unwrap_or_else(|p| p.into_inner())
                    .record(rk, uk, found, from_installed.or(seed));
            }
        }
    };
    tokio::join!(lane(0), lane(1), lane(2));
    let mut s = state.sites.lock().unwrap_or_else(|p| p.into_inner());
    s.prune(live);
    s.save();
}
