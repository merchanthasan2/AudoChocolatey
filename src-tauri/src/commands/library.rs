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
    Ok(collect_installed(&state).await)
}

/// Installed apps from every source that's on, with friendly names.
pub async fn collect_installed(state: &AppState) -> Listing<InstalledPackage> {
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
        state,
        listing
            .items
            .iter()
            .filter(|p| p.source == Some(SourceKind::Choco))
            .map(|p| &p.id),
    )
    .await;
    let cat = Catalog::embedded();
    listing.items = crate::sources::components::fold_local_duplicates(listing.items);
    link_local(state, &mut listing.items, choco_on).await;
    for p in &mut listing.items {
        // Catalog apps are never components.
        let known = p.source.is_some_and(|s| cat.by_source(s, &p.id).is_some());
        p.component = !known && crate::sources::components::is_component(&p.name, &p.id);
        p.removable = false;
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
    crate::sources::direct::mark_removable(
        &mut listing.items,
        crate::commands::system::shell_user_matches(),
    );
    listing
}

/// Marks apps installed directly that can be kept up to date here, and remembers them for
/// "Also updatable here" (see `catalog::local_match`).
async fn link_local(state: &AppState, items: &mut [InstalledPackage], choco_on: bool) {
    use crate::catalog::local_match::link_all;
    use crate::sources::label::parse_arp_id;
    let cat = Catalog::embedded();
    let ids: Vec<String> = items
        .iter()
        .filter(|p| p.source.is_none())
        .map(|p| p.id.clone())
        .collect();
    let entries = tauri::async_runtime::spawn_blocking(move || {
        ids.into_iter()
            .filter_map(|id| {
                let e = crate::sources::arp::read_entry(&parse_arp_id(&id)?)?;
                Some((id, e))
            })
            .collect::<Vec<_>>()
    })
    .await
    .unwrap_or_default();
    let managed: std::collections::HashSet<String> = items
        .iter()
        .filter_map(|p| Some(cat.by_source(p.source?, &p.id)?.key.clone()))
        .collect();
    let links = link_all(&entries, &managed, cat, |s| match s {
        SourceKind::Winget => true,
        SourceKind::Choco => choco_on,
        SourceKind::Msstore => false,
    });
    let mut kept = std::collections::HashMap::new();
    for p in items.iter_mut() {
        if let Some(l) = links.get(&p.id) {
            p.linked = true;
            let name = cat
                .by_key(&l.app_key)
                .map(|a| a.name.clone())
                .unwrap_or_else(|| p.name.clone());
            kept.insert(p.id.clone(), (l.clone(), name, p.version.clone()));
        }
    }
    *state.links.lock().unwrap_or_else(|p| p.into_inner()) = kept;
}

/// An update for an app installed directly ("Also updatable here"). Never part of "Update all",
/// automatic updates or notifications: each one is started by the person, after a confirm.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct LinkedUpdate {
    pub name: String,
    pub installed_version: String,
    pub available_version: String,
    /// What an update installs over the copy on this PC.
    pub target: SourceRef,
}

#[tauri::command]
pub async fn list_linked_updates(
    state: State<'_, AppState>,
) -> Result<Vec<LinkedUpdate>, AppError> {
    let links: Vec<_> = state
        .links
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .values()
        .cloned()
        .collect();
    let cat = Catalog::embedded();
    let mut out = Vec::new();
    for (link, name, installed) in links {
        let latest = match link.target.source() {
            SourceKind::Winget => {
                let Ok(id) = crate::ids::WingetId::try_from(link.target.id().to_string()) else {
                    continue;
                };
                state.winget.show(&id).await.ok().and_then(|s| s.version)
            }
            SourceKind::Choco => {
                let Ok(id) = crate::ids::ChocoId::try_from(link.target.id().to_string()) else {
                    continue;
                };
                state
                    .choco
                    .package(&id)
                    .await
                    .ok()
                    .flatten()
                    .and_then(|p| p.version)
            }
            SourceKind::Msstore => None,
        };
        let Some(latest) = latest else { continue };
        let same_major = cat
            .by_key(&link.app_key)
            .and_then(|a| a.local_match.as_ref())
            .is_some_and(|r| r.same_major);
        if !crate::catalog::local_match::offers(&installed, &latest, same_major) {
            continue;
        }
        // Skip and Never apply here too.
        let key = rule_key(&link.target, cat);
        let ruled = state
            .rules
            .lock()
            .unwrap_or_else(|p| p.into_inner())
            .rules()
            .iter()
            .any(|r| {
                r.key == key
                    && match &r.kind {
                        RuleKind::Never => true,
                        RuleKind::SkipVersion { version, .. } => {
                            crate::versions::compare(version.as_str(), &latest)
                                == std::cmp::Ordering::Equal
                        }
                        _ => false,
                    }
            });
        if ruled {
            continue;
        }
        out.push(LinkedUpdate {
            name,
            installed_version: installed,
            available_version: latest,
            target: link.target,
        });
    }
    Ok(out)
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
    /// About each item, in the same order.
    pub info: Vec<ItemInfo>,
    pub hidden: Vec<HiddenUpdate>,
    pub rules: Vec<Rule>,
    pub source_errors: Vec<SourceError>,
}

/// What the Updates page needs to know about one update besides the update itself.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ItemInfo {
    /// The app's rule key, to find its choice in `rules`.
    pub key: String,
    /// An app attackers often target ("Important to keep current"); these sort first.
    pub important: bool,
    /// Its installer now downloads from a different site than this app's usual one.
    pub site_changed: bool,
}

/// Security-relevant apps first, otherwise the order the sources gave.
pub fn important_first(
    items: Vec<UpgradeCandidate>,
    cat: &Catalog,
) -> Vec<(UpgradeCandidate, ItemInfo)> {
    let mut out: Vec<(UpgradeCandidate, ItemInfo)> = items
        .into_iter()
        .map(|c| {
            let app = cat.by_source(c.source, &c.id);
            let key = SourceRef::new(c.source, c.id.clone())
                .map(|t| rule_key(&t, cat))
                .unwrap_or_default();
            let important = app.is_some_and(|a| a.security);
            (
                c,
                ItemInfo {
                    key,
                    important,
                    site_changed: false,
                },
            )
        })
        .collect();
    out.sort_by_key(|(_, i)| !i.important);
    out
}

/// Applies rules to the raw list; the bool says whether an expired skip was removed.
pub fn updates_listing(
    raw: Listing<UpgradeCandidate>,
    rules: &mut Vec<Rule>,
    cat: &Catalog,
) -> (UpdatesListing, bool) {
    let (items, hidden, changed) = apply_rules(raw.items, rules, cat);
    let (items, info) = important_first(items, cat).into_iter().unzip();
    (
        UpdatesListing {
            items,
            info,
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
    let cat = Catalog::embedded();
    let (mut listing, changed) = updates_listing(raw, store.rules_mut(), cat);
    if changed {
        let _ = store.save();
    }
    let sites = state.sites.lock().unwrap_or_else(|p| p.into_inner());
    for (c, info) in listing.items.iter().zip(listing.info.iter_mut()) {
        info.site_changed = crate::background::auto::update_key(c, cat).is_some_and(|k| {
            matches!(sites.checked(&k), Some(crate::sites::Check::Changed { .. }))
        });
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
    refresh_update_info(app, listing.items.clone());
    Ok(listing)
}

/// Looks up "What's new" and release dates in the background, then tells the page.
pub fn refresh_update_info(app: tauri::AppHandle, items: Vec<UpgradeCandidate>) {
    use tauri::{Emitter, Manager};
    tauri::async_runtime::spawn(async move {
        let state = app.state::<AppState>();
        if crate::update_info::refresh(&state, &items).await {
            let info = crate::update_info::infos(
                &state.update_info.lock().unwrap_or_else(|p| p.into_inner()),
                &items,
            );
            let _ = app.emit("updates://info", info);
        }
    });
}

/// "What's new" and release dates known so far for the shown updates.
#[tauri::command]
pub fn get_update_info(state: State<'_, AppState>) -> Vec<crate::update_info::UpdateInfo> {
    let items = current_updates(&state).items;
    crate::update_info::infos(
        &state.update_info.lock().unwrap_or_else(|p| p.into_inner()),
        &items,
    )
}

/// Opens an update's release notes in the person's own browser (never as administrator).
#[tauri::command]
pub fn open_release_notes(
    state: State<'_, AppState>,
    target: SourceRef,
    version: String,
) -> Result<(), AppError> {
    let url = crate::update_info::notes_url(
        &state.update_info.lock().unwrap_or_else(|p| p.into_inner()),
        &target,
        &version,
    )
    .ok_or_else(|| {
        AppError::new(
            "invalid_input",
            "There's no release notes link for that update.",
        )
    })?;
    crate::shell_open::open_unelevated(&url).map_err(|m| AppError::new("open", m))
}

/// Checks again and applies rules. "Update all" uses exactly `items` of this.
pub async fn collect_updates(state: &AppState) -> UpdatesListing {
    let _one_at_a_time = state.check_lock.lock().await;
    let raw = collect_raw_updates(state).await;
    state.mark_checked();
    *state.last_updates.lock().unwrap_or_else(|p| p.into_inner()) = Some(raw);
    current_updates(state)
}

#[cfg(test)]
mod important_tests {
    use super::*;

    fn cand(source: SourceKind, id: &str) -> UpgradeCandidate {
        UpgradeCandidate {
            name: id.into(),
            id: id.into(),
            installed_version: "1".into(),
            available_version: "2".into(),
            source,
        }
    }

    #[test]
    fn security_apps_sort_first_from_any_source() {
        let cat = Catalog::embedded();
        let out = important_first(
            vec![
                cand(SourceKind::Winget, "Spotify.Spotify"),
                cand(SourceKind::Choco, "vlc"),
                cand(SourceKind::Winget, "Some.Tool"),
                cand(SourceKind::Winget, "Mozilla.Firefox"),
            ],
            cat,
        );
        let ids: Vec<(&str, bool)> = out
            .iter()
            .map(|(c, i)| (c.id.as_str(), i.important))
            .collect();
        assert_eq!(
            ids,
            vec![
                ("vlc", true),
                ("Mozilla.Firefox", true),
                ("Spotify.Spotify", false),
                ("Some.Tool", false)
            ]
        );
        assert_eq!(out[0].1.key, "app:vlc");
    }
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

/// One app's rule from the page ("Skip selected").
#[derive(Debug, Clone, serde::Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct RuleRequest {
    pub target: SourceRef,
    pub name: String,
    pub kind: RuleKind,
}

/// Sets several apps' rules at once (at most 200), saved once.
#[tauri::command]
pub fn set_rules(
    state: State<'_, AppState>,
    items: Vec<RuleRequest>,
) -> Result<UpdatesListing, AppError> {
    if items.len() > 200 {
        return Err(AppError::new(
            "invalid_input",
            "Choose at most 200 apps at a time.",
        ));
    }
    let now = crate::jobs::model::now_ms();
    let rules = items
        .into_iter()
        .map(|i| Rule {
            key: rule_key(&i.target, Catalog::embedded()),
            target: i.target,
            name: i
                .name
                .chars()
                .filter(|c| !c.is_control())
                .take(120)
                .collect(),
            kind: i.kind,
            created_at: now,
        })
        .collect();
    state
        .rules
        .lock()
        .unwrap_or_else(|p| p.into_inner())
        .set_many(rules)?;
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
