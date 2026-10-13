mod catalog;
mod commands;
mod error;
mod fsutil;
mod icons;
mod ids;
mod jobs;
pub mod launch;
mod rules;
mod settings;
mod sources;
mod state;
mod versions;

use tauri::{Emitter, Manager};

/// Removes this user's start-at-login task (called by the uninstaller). Filled in by P6a T6.
pub fn unregister_autostart() {}

/// The id Windows uses for this app's notifications and taskbar grouping; matches the bundle
/// identifier the installer puts on the Start menu shortcut.
pub const APP_ID: &str = "in.quantumstep.quantumupdater";

pub fn set_app_id() {
    let id: Vec<u16> = APP_ID.encode_utf16().chain(std::iter::once(0)).collect();
    // SAFETY: NUL-terminated UTF-16 that outlives the call.
    unsafe { windows_sys::Win32::UI::Shell::SetCurrentProcessExplicitAppUserModelID(id.as_ptr()) };
}

/// Waits (bounded) for another process to exit.
pub fn wait_for_exit(pid: u32, limit: std::time::Duration) {
    use windows_sys::Win32::Foundation::CloseHandle;
    use windows_sys::Win32::System::Threading::{
        OpenProcess, WaitForSingleObject, PROCESS_SYNCHRONIZE,
    };
    // SAFETY: plain OpenProcess with SYNCHRONIZE; the handle is closed below.
    let h = unsafe { OpenProcess(PROCESS_SYNCHRONIZE, 0, pid) };
    if h.is_null() {
        return; // already gone, or not ours to wait on
    }
    // SAFETY: valid handle; bounded wait.
    unsafe {
        WaitForSingleObject(h, limit.as_millis().min(u32::MAX as u128) as u32);
        CloseHandle(h);
    }
}

pub fn run(launch: launch::Launch) {
    tauri::Builder::default()
        // First plugin: a second launch focuses this window. Its arguments are never acted on
        // beyond recognising a sign-in (background) start.
        .plugin(tauri_plugin_single_instance::init(|app, argv, _cwd| {
            let second = launch::parse_args(argv.get(1..).unwrap_or_default());
            if second.mode == launch::LaunchMode::Normal {
                if let Some(w) = app.get_webview_window("main") {
                    let _ = w.show();
                    let _ = w.unminimize();
                    let _ = w.set_focus();
                }
            }
        }))
        .setup(move |app| {
            app.manage(launch);
            let config_dir = app.path().app_config_dir()?;
            let data_dir = app.path().app_local_data_dir()?;
            app.manage(state::AppState::new(
                config_dir,
                data_dir,
                app.handle().clone(),
            ));
            Ok(())
        })
        .on_window_event(|window, event| {
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                let state = window.state::<state::AppState>();
                if commands::jobs::close_blocked(&state.queue.snapshot()) {
                    api.prevent_close();
                    let _ = window.emit("app://close-blocked", ());
                }
            }
        })
        .register_asynchronous_uri_scheme_protocol("icon", |ctx, request, responder| {
            let app = ctx.app_handle().clone();
            tauri::async_runtime::spawn(async move {
                let not_found = || {
                    tauri::http::Response::builder()
                        .status(404)
                        .header("Cache-Control", "max-age=600")
                        .body(Vec::new())
                        .unwrap()
                };
                let Some((key, name)) =
                    icons::parse_request(request.uri().path(), request.uri().query())
                else {
                    return responder.respond(not_found());
                };
                let state = app.state::<state::AppState>();
                let response = match state.icons.resolve(&key, name.as_deref()).await {
                    Some(png) => tauri::http::Response::builder()
                        .header("Content-Type", "image/png")
                        .header("Cache-Control", "max-age=604800")
                        .body(png.to_vec())
                        .unwrap(),
                    None => not_found(),
                };
                responder.respond(response);
            });
        })
        .invoke_handler(tauri::generate_handler![
            commands::system::get_overview,
            commands::system::get_diagnostics,
            commands::library::list_installed,
            commands::library::list_updates,
            commands::library::search,
            commands::settings::get_settings,
            commands::settings::update_settings,
            commands::jobs::enqueue,
            commands::jobs::update_all,
            commands::catalog::marketplace_category_more,
            commands::library::set_rule,
            commands::library::remove_rule,
            commands::jobs::get_queue,
            commands::jobs::pause_queue,
            commands::jobs::resume_queue,
            commands::jobs::quit_anyway,
            commands::system::relaunch_as_admin,
            commands::jobs::cancel_job,
            commands::jobs::retry_job,
            commands::jobs::clear_finished,
            commands::jobs::get_job_log,
            commands::catalog::marketplace_home,
            commands::catalog::marketplace_category,
            commands::catalog::get_app,
        ])
        .run(tauri::generate_context!())
        .expect("error while running Quantum Updater");
}
