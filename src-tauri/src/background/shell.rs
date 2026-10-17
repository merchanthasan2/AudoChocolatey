//! The tray icon and the main window's show/hide/quit behaviour (Tauri side of `tray` and
//! `window`).

use super::tray::{tooltip, TrayAction};
use crate::commands::jobs::{close_blocked, gates, permitted, refuse_unelevated, upgrades_to_ops};
use crate::state::AppState;
use tauri::menu::{Menu, MenuItem, PredefinedMenuItem};
use tauri::tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent};
use tauri::{AppHandle, Emitter, Manager, Wry};

const TRAY_ID: &str = "main";

/// Menu items whose text changes.
pub struct TrayItems {
    pause: MenuItem<Wry>,
    update_all: MenuItem<Wry>,
}

pub fn install(app: &AppHandle) -> tauri::Result<()> {
    let item =
        |a: TrayAction| MenuItem::with_id(app, a.id(), a.label(false, 0), true, None::<&str>);
    let (open, check, pause, update_all, quit) = (
        item(TrayAction::Open)?,
        item(TrayAction::CheckNow)?,
        item(TrayAction::PauseResume)?,
        item(TrayAction::UpdateAll)?,
        item(TrayAction::Quit)?,
    );
    let sep = || PredefinedMenuItem::separator(app);
    let menu = Menu::with_items(
        app,
        &[&open, &sep()?, &check, &update_all, &pause, &sep()?, &quit],
    )?;
    let mut builder = TrayIconBuilder::with_id(TRAY_ID)
        .tooltip(tooltip(None))
        .menu(&menu)
        .show_menu_on_left_click(false)
        .on_menu_event(|app, event| {
            if let Some(action) = TrayAction::from_id(event.id().as_ref()) {
                on_action(app, action);
            }
        })
        .on_tray_icon_event(|tray, event| {
            if let TrayIconEvent::Click {
                button: MouseButton::Left,
                button_state: MouseButtonState::Up,
                ..
            } = event
            {
                show_main(tray.app_handle());
            }
        });
    if let Some(icon) = app.default_window_icon() {
        builder = builder.icon(icon.clone());
    }
    builder.build(app)?;
    app.manage(TrayItems { pause, update_all });
    Ok(())
}

pub fn show_main(app: &AppHandle) {
    if let Some(w) = app.get_webview_window("main") {
        let _ = w.show();
        let _ = w.unminimize();
        let _ = w.set_focus();
    }
}

/// Notifications only make sense when the window is closed to the tray and the person at the
/// desktop is the account running the app.
pub fn should_notify(app: &AppHandle) -> bool {
    let hidden = app
        .get_webview_window("main")
        .and_then(|w| w.is_visible().ok())
        .is_some_and(|v| !v);
    hidden && crate::commands::system::shell_user_matches() == Some(true)
}

/// Updates the tooltip and the "Update all (n)" item after a check.
pub fn set_count(app: &AppHandle, updates: usize) {
    if let Some(t) = app.tray_by_id(TRAY_ID) {
        let _ = t.set_tooltip(Some(tooltip(Some(updates))));
    }
    if let Some(items) = app.try_state::<TrayItems>() {
        let _ = items
            .update_all
            .set_text(TrayAction::UpdateAll.label(false, updates));
    }
}

pub fn set_paused(app: &AppHandle, paused: bool) {
    if let Some(items) = app.try_state::<TrayItems>() {
        let _ = items
            .pause
            .set_text(TrayAction::PauseResume.label(paused, 0));
    }
}

/// Quit from the tray: same guard as closing the window while a job runs.
pub fn quit_requested(app: &AppHandle) {
    let state = app.state::<AppState>();
    if close_blocked(&state.queue.snapshot()) {
        show_main(app);
        let _ = app.emit("app://close-blocked", ());
    } else {
        app.exit(0);
    }
}

/// Runs a fresh check and tells the UI and the tray.
pub fn check_now(app: &AppHandle) {
    let app = app.clone();
    tauri::async_runtime::spawn(async move {
        let state = app.state::<AppState>();
        let listing = crate::commands::library::collect_updates(&state).await;
        set_count(&app, listing.items.len());
        let _ = app.emit("updates://changed", &listing);
    });
}

fn on_action(app: &AppHandle, action: TrayAction) {
    let state = app.state::<AppState>();
    match action {
        TrayAction::Open => show_main(app),
        TrayAction::CheckNow => check_now(app),
        TrayAction::PauseResume => {
            if state.queue.is_paused() {
                state.queue.resume();
            } else {
                state.queue.pause();
            }
            set_paused(app, state.queue.is_paused());
        }
        TrayAction::UpdateAll => {
            if refuse_unelevated(crate::commands::system::is_elevated()).is_err() {
                show_main(app);
                return;
            }
            // The last checked list (with the person's rules applied), never a new one.
            let listing = crate::commands::library::current_updates(&state);
            let ops = permitted(upgrades_to_ops(listing.items), gates(&state));
            state.queue.enqueue(ops);
        }
        TrayAction::Quit => quit_requested(app),
    }
}
