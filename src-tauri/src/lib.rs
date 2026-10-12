mod commands;

pub fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![commands::system::get_overview])
        .run(tauri::generate_context!())
        .expect("error while running Quantum Updater");
}
