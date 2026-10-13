//! The background check loop: wakes every few minutes (so sleep and clock changes are handled),
//! checks when due, updates the tray and the Updates page, and notifies about new updates while
//! the window is hidden.

use super::schedule::{diff_new, due, BackgroundState};
use crate::catalog::Catalog;
use crate::ids::SourceRef;
use crate::jobs::model::now_ms;
use crate::rules::rule_key;
use crate::state::AppState;
use std::collections::{BTreeMap, BTreeSet};
use std::path::PathBuf;
use std::time::Duration;
use tauri::{AppHandle, Emitter, Manager};

const TICK: Duration = Duration::from_secs(5 * 60);
/// A check waits while a job runs, but not forever.
const MAX_DEFER_MS: u64 = 30 * 60_000;

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
            let now = now_ms();
            let last = saved.last_check_at.max(state.last_check());
            if !due(now, launched_at, last, settings.check_interval_hours) {
                continue;
            }
            let busy = state
                .queue
                .snapshot()
                .iter()
                .any(|j| !j.phase.is_finished());
            if busy && now - *deferred_since.get_or_insert(now) < MAX_DEFER_MS {
                continue;
            }
            deferred_since = None;
            let listing = crate::commands::library::collect_updates(&state).await;
            super::shell::set_count(&app, listing.items.len());
            let _ = app.emit("updates://changed", &listing);

            let cat = Catalog::embedded();
            let current: BTreeMap<String, String> = listing
                .items
                .iter()
                .filter_map(|u| {
                    let t = SourceRef::new(u.source, u.id.clone()).ok()?;
                    Some((
                        format!("{}@{}", rule_key(&t, cat), u.available_version),
                        u.name.clone(),
                    ))
                })
                .collect();
            let keys: BTreeSet<String> = current.keys().cloned().collect();
            let new = diff_new(&saved.notified, &keys);
            let window_hidden = app
                .get_webview_window("main")
                .and_then(|w| w.is_visible().ok())
                .is_some_and(|v| !v);
            if !new.is_empty()
                && settings.notify_updates
                && window_hidden
                && crate::commands::system::shell_user_matches() == Some(true)
            {
                let names: Vec<String> =
                    new.iter().filter_map(|k| current.get(k).cloned()).collect();
                let (title, body) = super::toast::update_lines(&names);
                super::toast::show(&title, &body);
            }
            saved.last_check_at = Some(now);
            saved.notified = keys;
            let _ = saved.save(&path);
        }
    });
}
