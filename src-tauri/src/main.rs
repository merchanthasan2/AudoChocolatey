#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    // Development builds run unelevated; give them their own WebView2 profile so they can run
    // next to an installed (elevated) copy, which WebView2 won't share a profile with.
    #[cfg(debug_assertions)]
    if let Some(local) = std::env::var_os("LOCALAPPDATA") {
        let dir = std::path::Path::new(&local).join("in.quantumstep.quantumupdater").join("EBWebView-dev");
        std::env::set_var("WEBVIEW2_USER_DATA_FOLDER", dir);
    }
    quantum_updater_lib::run();
}
