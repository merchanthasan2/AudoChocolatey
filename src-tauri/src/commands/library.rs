//! Library commands: installed apps, available updates and search across both sources.
//! Sources are queried concurrently; one failing source never hides the other's results.

use crate::catalog::{merge::merge, names, AppEntry, Catalog};
use crate::error::{AppError, AppResult};
use crate::ids::{SearchQuery, SourceKind, SourceRef};
use crate::rules::{apply_rules, rule_key, HiddenUpdate, Rule, RuleKind};
use crate::sources::model::{InstalledPackage, UpgradeCandidate};
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
    let mut listing = collect(vec![(SourceKind::Winget, w), (SourceKind::Choco, c)]);
    let titles = choco_titles(
        &state,
        listing
            .items
            .iter()
            .filter(|p| p.source == Some(SourceKind::Choco))
            .map(|p| &p.id),
    )
    .await;
    let cat = Catalog::embedded();
    for p in &mut listing.items {
        p.name = match p.source {
            Some(src) => names::display_name(
                src,
                &p.id,
                &p.name,
                &p.version,
                cat,
                titles.get(&p.id).map(String::as_str),
            ),
            None => names::strip_version_tail(&p.name, &p.version),
        };
    }
    Ok(listing)
}

async fn choco_titles<'a>(
    state: &AppState,
    ids: impl Iterator<Item = &'a String>,
) -> std::collections::HashMap<String, String> {
    let ids: Vec<String> = ids
        .filter(|id| Catalog::embedded().by_choco(id).is_none())
        .cloned()
        .collect();
    if ids.is_empty() {
        return Default::default();
    }
    state.choco.titles(&ids).await
}

/// The Updates screen: what can be updated, what the user hid, and the rules behind that.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct UpdatesListing {
    pub items: Vec<UpgradeCandidate>,
    pub hidden: Vec<HiddenUpdate>,
    pub rules: Vec<Rule>,
    pub source_errors: Vec<SourceError>,
}

/// Applies rules to the raw list; the bool says whether an expired skip was removed.
pub fn updates_listing(
    raw: Listing<UpgradeCandidate>,
    rules: &mut Vec<Rule>,
    cat: &Catalog,
) -> (UpdatesListing, bool) {
    let (items, hidden, changed) = apply_rules(raw.items, rules, cat);
    (
        UpdatesListing {
            items,
            hidden,
            rules: rules.clone(),
            source_errors: raw.source_errors,
        },
        changed,
    )
}

/// The last checked list with the current rules applied (no new check).
pub fn current_updates(state: &AppState) -> UpdatesListing {
    let raw = state
        .last_updates
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .clone()
        .unwrap_or(Listing {
            items: Vec::new(),
            source_errors: Vec::new(),
        });
    let mut store = state.rules.lock().unwrap_or_else(|p| p.into_inner());
    let (listing, changed) = updates_listing(raw, store.rules_mut(), Catalog::embedded());
    if changed {
        let _ = store.save();
    }
    listing
}

#[tauri::command]
pub async fn list_updates(
    app: tauri::AppHandle,
    state: State<'_, AppState>,
) -> Result<UpdatesListing, AppError> {
    let listing = collect_updates(&state).await;
    crate::background::shell::set_count(&app, listing.items.len());
    Ok(listing)
}

/// Checks again and applies rules. "Update all" uses exactly `items` of this.
pub async fn collect_updates(state: &AppState) -> UpdatesListing {
    let _one_at_a_time = state.check_lock.lock().await;
    let raw = collect_raw_updates(state).await;
    state.mark_checked();
    *state.last_updates.lock().unwrap_or_else(|p| p.into_inner()) = Some(raw);
    current_updates(state)
}

#[tauri::command]
pub fn set_rule(
    state: State<'_, AppState>,
    target: SourceRef,
    name: String,
    kind: RuleKind,
) -> Result<UpdatesListing, AppError> {
    let name: String = name.chars().filter(|c| !c.is_control()).take(120).collect();
    let rule = Rule {
        key: rule_key(&target, Catalog::embedded()),
        target,
        name,
        kind,
        created_at: crate::jobs::model::now_ms(),
    };
    state
        .rules
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .set(rule)?;
    Ok(current_updates(&state))
}

#[tauri::command]
pub fn remove_rule(state: State<'_, AppState>, key: String) -> Result<UpdatesListing, AppError> {
    if key.len() > 200 {
        return Err(AppError::new("invalid_input", "not a rule"));
    }
    state
        .rules
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .remove(&key)?;
    Ok(current_updates(&state))
}

async fn collect_raw_updates(state: &AppState) -> Listing<UpgradeCandidate> {
    let s = state.settings();
    let (w, c) = tokio::join!(state.winget.list_upgrades(s.show_unknown_versions), async {
        if s.choco.enabled {
            state.choco.list_upgrades().await
        } else {
            disabled()
        }
    });
    let mut listing = collect(vec![(SourceKind::Winget, w), (SourceKind::Choco, c)]);
    let cat = Catalog::embedded();
    listing.items = names::owner_dedupe(listing.items, cat);
    let titles = choco_titles(
        state,
        listing
            .items
            .iter()
            .filter(|u| u.source == SourceKind::Choco)
            .map(|u| &u.id),
    )
    .await;
    for u in &mut listing.items {
        u.name = names::display_name(
            u.source,
            &u.id,
            &u.name,
            &u.installed_version,
            cat,
            titles.get(&u.id).map(String::as_str),
        );
    }
    listing
}

#[tauri::command]
pub async fn search(
    state: State<'_, AppState>,
    query: String,
) -> Result<Listing<AppEntry>, AppError> {
    let q = SearchQuery::try_from(query)?;
    let settings = state.settings();
    let (choco_on, store_on) = (settings.choco.enabled, settings.store.enabled);
    let (w, c, m) = tokio::join!(
        state.winget.search(&q),
        async {
            if choco_on {
                state.choco.search(&q).await
            } else {
                disabled()
            }
        },
        async {
            if store_on {
                state.winget.search_store(&q).await
            } else {
                Ok(Vec::new())
            }
        }
    );
    if let Ok(items) = &c {
        for p in items {
            if let Some(url) = &p.icon_url {
                state.icons.remember_choco(&p.id, url);
            }
        }
    }
    let mut source_errors = collect::<AppEntry>(vec![
        (
            SourceKind::Winget,
            w.as_ref().map(|_| vec![]).map_err(Clone::clone),
        ),
        (
            SourceKind::Choco,
            c.as_ref().map(|_| vec![]).map_err(Clone::clone),
        ),
        (
            SourceKind::Msstore,
            m.as_ref().map(|_| vec![]).map_err(Clone::clone),
        ),
    ])
    .source_errors;
    source_errors.dedup_by(|a, b| a.source == b.source);
    Ok(Listing {
        items: merge(
            w.unwrap_or_default(),
            c.unwrap_or_default(),
            m.unwrap_or_default(),
            Catalog::embedded(),
        ),
        source_errors,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::error::AppError;

    #[test]
    fn ruled_apps_leave_the_list_that_update_all_uses() {
        use crate::ids::SourceRef;
        use crate::rules::{rule_key, Rule, RuleKind};
        let cand = |id: &str| UpgradeCandidate {
            name: id.into(),
            id: id.into(),
            installed_version: "1".into(),
            available_version: "2".into(),
            source: SourceKind::Winget,
        };
        let raw = Listing {
            items: vec![cand("A.A"), cand("B.B")],
            source_errors: vec![],
        };
        let target = SourceRef::new(SourceKind::Winget, "B.B").unwrap();
        let mut rules = vec![Rule {
            key: rule_key(&target, Catalog::embedded()),
            target,
            name: "B".into(),
            kind: RuleKind::Never,
            created_at: 1,
        }];
        let (l, changed) = updates_listing(raw, &mut rules, Catalog::embedded());
        assert!(!changed);
        assert_eq!(l.items.len(), 1);
        assert_eq!(l.items[0].id, "A.A");
        assert_eq!(l.hidden[0].candidate.id, "B.B");
        assert_eq!(l.rules.len(), 1);
    }

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
