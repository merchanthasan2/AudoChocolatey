//! "Close it and update?" The page names apps; Rust finds their programs again each time, so a
//! process id or folder never comes from the page.

use crate::error::AppError;
use crate::ids::SourceKind;
use crate::running::{close_politely, install_folder, processes_in, shared_folders, ArpInfo};
use serde::{Deserialize, Serialize};
use std::time::Duration;
use ts_rs::TS;

#[derive(Debug, Clone, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct RunningQuery {
    pub source: SourceKind,
    pub id: String,
    pub name: String,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct RunningApp {
    pub source: SourceKind,
    pub id: String,
    pub name: String,
    /// The program files that are open ("vlc.exe").
    pub programs: Vec<String>,
}

fn arp() -> Vec<ArpInfo> {
    crate::icons::win::arp_entries()
        .into_iter()
        .map(|e| ArpInfo {
            name: e.name,
            install_location: e.install_location,
            display_icon: e.display_icon,
        })
        .collect()
}

/// Which of these apps are open right now. Blocking.
pub fn find_running(apps: &[RunningQuery]) -> Vec<(RunningApp, Vec<u32>)> {
    let entries = arp();
    let shared = shared_folders();
    apps.iter()
        .filter_map(|q| {
            let folder = install_folder(&q.name, &entries, &shared)?;
            let procs = processes_in(&folder);
            if procs.is_empty() {
                return None;
            }
            let mut programs: Vec<String> = procs.iter().map(|(_, n)| n.clone()).collect();
            programs.sort();
            programs.dedup();
            Some((
                RunningApp {
                    source: q.source,
                    id: q.id.clone(),
                    name: q.name.clone(),
                    programs,
                },
                procs.into_iter().map(|(p, _)| p).collect(),
            ))
        })
        .collect()
}

fn check(apps: &[RunningQuery]) -> Result<(), AppError> {
    if apps.len() > 200 || apps.iter().any(|a| a.name.len() > 300 || a.id.len() > 300) {
        return Err(AppError::new("invalid_input", "too many apps"));
    }
    Ok(())
}

#[tauri::command]
pub async fn running_apps(apps: Vec<RunningQuery>) -> Result<Vec<RunningApp>, AppError> {
    check(&apps)?;
    let found = tauri::async_runtime::spawn_blocking(move || find_running(&apps))
        .await
        .map_err(|e| AppError::new("running", e.to_string()))?;
    Ok(found.into_iter().map(|(a, _)| a).collect())
}

/// Asks these apps to close (as their X button would) and waits up to 15 seconds. Returns the
/// names of apps still open.
#[tauri::command]
pub async fn close_apps(apps: Vec<RunningQuery>) -> Result<Vec<String>, AppError> {
    check(&apps)?;
    tauri::async_runtime::spawn_blocking(move || {
        find_running(&apps)
            .into_iter()
            .filter_map(|(a, pids)| {
                let left = close_politely(&pids, Duration::from_secs(15));
                (!left.is_empty()).then_some(a.name)
            })
            .collect()
    })
    .await
    .map_err(|e| AppError::new("running", e.to_string()))
}
