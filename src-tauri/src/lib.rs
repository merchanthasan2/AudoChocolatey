mod commands;
mod error;
mod ids;
mod jobs;
mod settings;
mod sources;
mod state;
mod versions;

use tauri::Manager;

pub fn run() {
    tauri::Builder::default()
        .setup(|app| {
            let config_dir = app.path().app_config_dir()?;
            app.manage(state::AppState::new(config_dir, app.handle().clone()));
            Ok(())
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
            commands::jobs::get_queue,
            commands::jobs::cancel_job,
            commands::jobs::retry_job,
            commands::jobs::clear_finished,
            commands::jobs::get_job_log,
        ])
        .run(tauri::generate_context!())
        .expect("error while running Quantum Updater");
}
