mod catalog;
mod commands;
mod error;
mod fsutil;
mod icons;
mod ids;
mod jobs;
mod rules;
mod settings;
mod sources;
mod state;
mod versions;

use tauri::{Emitter, Manager};

pub fn run() {
    tauri::Builder::default()
        .setup(|app| {
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
            commands::library::set_rule,
            commands::library::remove_rule,
            commands::jobs::get_queue,
            commands::jobs::pause_queue,
            commands::jobs::resume_queue,
            commands::jobs::quit_anyway,
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
