#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    // Development builds run unelevated; give them their own WebView2 profile so they can run
    // next to an installed (elevated) copy, which WebView2 won't share a profile with.
    #[cfg(debug_assertions)]
    if let Some(local) = std::env::var_os("LOCALAPPDATA") {
        let dir = std::path::Path::new(&local)
            .join("in.quantumstep.quantumupdater")
            .join("EBWebView-dev");
        std::env::set_var("WEBVIEW2_USER_DATA_FOLDER", dir);
    }
    let args: Vec<String> = std::env::args().skip(1).collect();
    let launch = quantum_updater_lib::launch::parse_args(&args);
    if launch.mode == quantum_updater_lib::launch::LaunchMode::UnregisterAutostart {
        // Run by the uninstaller; must finish before single-instance would forward it.
        quantum_updater_lib::unregister_autostart();
        return;
    }
    if let Some(pid) = launch.after {
        // We were relaunched as admin: let the old copy exit first, or single-instance would
        // hand this launch back to it.
        quantum_updater_lib::wait_for_exit(pid, std::time::Duration::from_secs(15));
    }
    quantum_updater_lib::set_app_id();
    quantum_updater_lib::run(launch);
}
