use std::process::Command;

/// Every #[tauri::command] must be listed here AND granted as `allow-<kebab-name>` in capabilities/main.json.
const COMMANDS: &[&str] = &[
    "get_overview",
    "get_diagnostics",
    "list_installed",
    "list_updates",
    "search",
    "get_settings",
    "update_settings",
    "enqueue",
    "update_all",
    "get_queue",
    "cancel_job",
    "retry_job",
    "clear_finished",
    "get_job_log",
    "marketplace_home",
    "marketplace_category",
    "get_app",
];

fn git(args: &[&str]) -> Option<String> {
    let out = Command::new("git").args(args).output().ok()?;
    if !out.status.success() {
        return None;
    }
    Some(String::from_utf8_lossy(&out.stdout).trim().to_string())
}

fn main() {
    // Build identity shown in Settings > About, so a stale exe is obvious. Never fails the build.
    let sha = git(&["rev-parse", "--short=8", "HEAD"])
        .filter(|s| !s.is_empty())
        .unwrap_or_else(|| "unknown".into());
    let dirty = git(&["status", "--porcelain"]).is_some_and(|s| !s.is_empty());
    println!(
        "cargo:rustc-env=BUILD_SHA={sha}{}",
        if dirty { "-dirty" } else { "" }
    );
    println!("cargo:rerun-if-changed=../.git/HEAD");
    println!("cargo:rerun-if-changed=../.git/logs/HEAD");
    println!("cargo:rerun-if-changed=../.git/index");
    println!("cargo:rerun-if-changed=windows");
    // Any rerun-if-changed disables Cargo's default "rerun on any package file", so list the sources whose
    // unstaged edits must flip the -dirty flag.
    for path in [
        "src",
        "Cargo.toml",
        "../app/src",
        "../app/index.html",
        "../package.json",
    ] {
        println!("cargo:rerun-if-changed={path}");
    }

    // Release requires admin; debug runs as the invoking user so `tauri dev` works from a normal terminal.
    let manifest = if std::env::var("PROFILE").as_deref() == Ok("release") {
        include_str!("windows/manifest.xml")
    } else {
        include_str!("windows/manifest.debug.xml")
    };

    let attrs = tauri_build::Attributes::new()
        .windows_attributes(tauri_build::WindowsAttributes::new().app_manifest(manifest))
        .app_manifest(tauri_build::AppManifest::new().commands(COMMANDS));
    tauri_build::try_build(attrs).expect("tauri-build failed");
}
