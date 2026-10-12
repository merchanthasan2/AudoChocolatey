//! Library commands: installed apps, available updates and search across both sources.
//! Sources are queried concurrently; one failing source never hides the other's results.

use crate::error::{AppError, AppResult};
use crate::ids::{SearchQuery, SourceKind};
use crate::sources::model::{InstalledPackage, SourcePackage, UpgradeCandidate};
use crate::state::AppState;
use serde::Serialize;
use tauri::State;
use ts_rs::TS;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[ts(export)]
pub struct SourceError {
    pub source: SourceKind,
    pub code: String,
    pub message: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct Listing<T: TS> {
    pub items: Vec<T>,
    pub source_errors: Vec<SourceError>,
}

/// Merge per-source results. Chocolatey not being installed is normal (most PCs) and is not an
/// error; anything else is surfaced per source.
pub fn collect<T: TS>(results: Vec<(SourceKind, AppResult<Vec<T>>)>) -> Listing<T> {
    let mut listing = Listing {
        items: Vec::new(),
        source_errors: Vec::new(),
    };
    for (source, result) in results {
        match result {
            Ok(items) => listing.items.extend(items),
            Err(e) if source == SourceKind::Choco && e.code == "source_missing" => {}
            Err(e) => listing.source_errors.push(SourceError {
                source,
                code: e.code,
                message: e.message,
            }),
        }
    }
    listing
}

fn disabled<T>() -> AppResult<Vec<T>> {
    Err(AppError::new(
        "source_missing",
        "Chocolatey is turned off in Settings",
    ))
}

#[tauri::command]
pub async fn list_installed(
    state: State<'_, AppState>,
) -> Result<Listing<InstalledPackage>, AppError> {
    let choco_on = state.settings().choco.enabled;
    let (w, c) = tokio::join!(state.winget.list_installed(), async {
        if choco_on {
            state.choco.list_installed().await
        } else {
            disabled()
        }
    });
    Ok(collect(vec![
        (SourceKind::Winget, w),
        (SourceKind::Choco, c),
    ]))
}

#[tauri::command]
pub async fn list_updates(
    state: State<'_, AppState>,
) -> Result<Listing<UpgradeCandidate>, AppError> {
    Ok(collect_updates(&state).await)
}

/// The update list shown on the Updates screen; "Update all" uses exactly this list.
pub async fn collect_updates(state: &AppState) -> Listing<UpgradeCandidate> {
    let s = state.settings();
    let (w, c) = tokio::join!(state.winget.list_upgrades(s.show_unknown_versions), async {
        if s.choco.enabled {
            state.choco.list_upgrades().await
        } else {
            disabled()
        }
    });
    collect(vec![(SourceKind::Winget, w), (SourceKind::Choco, c)])
}

#[tauri::command]
pub async fn search(
    state: State<'_, AppState>,
    query: String,
) -> Result<Listing<SourcePackage>, AppError> {
    let q = SearchQuery::try_from(query)?;
    let choco_on = state.settings().choco.enabled;
    let (w, c) = tokio::join!(state.winget.search(&q), async {
        if choco_on {
            state.choco.search(&q).await
        } else {
            disabled()
        }
    });
    if let Ok(items) = &c {
        for p in items {
            if let Some(url) = &p.icon_url {
                state.icons.remember_choco(&p.id, url);
            }
        }
    }
    Ok(collect(vec![
        (SourceKind::Winget, w),
        (SourceKind::Choco, c),
    ]))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::error::AppError;

    #[test]
    fn missing_choco_is_silent() {
        let l = collect(vec![
            (SourceKind::Winget, Ok(vec![1, 2])),
            (
                SourceKind::Choco,
                Err(AppError::new("source_missing", "not installed")),
            ),
        ]);
        assert_eq!(l.items, vec![1, 2]);
        assert!(l.source_errors.is_empty());
    }

    #[test]
    fn other_failures_are_reported_per_source() {
        let l = collect(vec![
            (
                SourceKind::Winget,
                Err(AppError::new("process_failed", "boom")),
            ),
            (SourceKind::Choco, Ok(vec![3])),
        ]);
        assert_eq!(l.items, vec![3]);
        assert_eq!(l.source_errors.len(), 1);
        assert_eq!(l.source_errors[0].source, SourceKind::Winget);
        assert_eq!(l.source_errors[0].code, "process_failed");
    }

    #[test]
    fn missing_winget_is_reported() {
        let l: Listing<i32> = collect(vec![(
            SourceKind::Winget,
            Err(AppError::new("source_missing", "no winget")),
        )]);
        assert_eq!(l.source_errors.len(), 1);
    }

    #[test]
    fn listing_serializes_camel_case() {
        let l = collect::<i32>(vec![(
            SourceKind::Choco,
            Err(AppError::new("network", "x")),
        )]);
        let v = serde_json::to_value(&l).unwrap();
        assert_eq!(v["sourceErrors"][0]["source"], "choco");
    }
}
