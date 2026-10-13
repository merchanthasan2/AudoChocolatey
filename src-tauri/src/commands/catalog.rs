//! Marketplace commands backed by the embedded catalog, plus app details from the sources.

use crate::catalog::{AppEntry, Catalog};
use crate::error::AppError;
use crate::ids::{ChocoId, SourceKind, StoreId, WingetId};
use crate::state::AppState;
use serde::Serialize;
use tauri::State;
use ts_rs::TS;

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct CategoryCount {
    pub id: String,
    pub name: String,
    pub count: u32,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct BundleView {
    pub id: String,
    pub name: String,
    pub blurb: String,
    pub apps: Vec<AppEntry>,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct Home {
    pub featured: Vec<AppEntry>,
    pub categories: Vec<CategoryCount>,
    pub bundles: Vec<BundleView>,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct AppDetails {
    pub entry: AppEntry,
    pub description: Option<String>,
    pub homepage: Option<String>,
    pub publisher: Option<String>,
    pub license: Option<String>,
    pub version: Option<String>,
}

pub fn home(cat: &Catalog) -> Home {
    let featured = cat
        .apps
        .iter()
        .filter(|a| a.featured)
        .map(|a| cat.entry(a))
        .collect();
    let categories = cat
        .categories
        .iter()
        .map(|c| CategoryCount {
            id: c.id.clone(),
            name: c.name.clone(),
            count: cat.apps.iter().filter(|a| a.category == c.id).count() as u32,
        })
        .collect();
    let bundles = cat
        .bundles
        .iter()
        .map(|b| BundleView {
            id: b.id.clone(),
            name: b.name.clone(),
            blurb: b.blurb.clone(),
            apps: b
                .apps
                .iter()
                .filter_map(|k| cat.by_key(k))
                .map(|a| cat.entry(a))
                .collect(),
        })
        .collect();
    Home {
        featured,
        categories,
        bundles,
    }
}

pub fn category(cat: &Catalog, id: &str) -> Vec<AppEntry> {
    let mut apps: Vec<AppEntry> = cat
        .apps
        .iter()
        .filter(|a| a.category == id)
        .map(|a| cat.entry(a))
        .collect();
    apps.sort_by_key(|a| a.name.to_lowercase());
    apps
}

#[tauri::command]
pub fn marketplace_home() -> Home {
    home(Catalog::embedded())
}

#[tauri::command]
pub fn marketplace_category(id: String) -> Vec<AppEntry> {
    category(Catalog::embedded(), &id)
}

/// Details for any entry: catalog apps by key, or `w:<id>` / `c:<id>` from search results.
#[tauri::command]
pub async fn get_app(state: State<'_, AppState>, entry: AppEntry) -> Result<AppDetails, AppError> {
    let mut d = AppDetails {
        description: entry.blurb.clone(),
        homepage: entry.homepage.clone(),
        publisher: entry.publisher.clone(),
        license: None,
        version: entry.version.clone(),
        entry,
    };
    match d.entry.best {
        SourceKind::Winget => {
            let id = WingetId::try_from(d.entry.winget.clone().unwrap_or_default())?;
            if let Ok(info) = state.winget.show(&id).await {
                d.description = info.description.or(d.description);
                d.homepage = info.homepage.or(d.homepage);
                d.publisher = info.publisher.or(d.publisher);
                d.license = info.license;
                d.version = info.version.or(d.version);
            }
        }
        SourceKind::Choco => {
            let id = ChocoId::try_from(d.entry.choco.clone().unwrap_or_default())?;
            if let Ok(Some(p)) = state.choco.package(&id).await {
                d.description = p.description.or(d.description);
                d.homepage = p.homepage.or(d.homepage);
                d.version = p.version.or(d.version);
            }
        }
        SourceKind::Msstore => {
            let id = StoreId::try_from(d.entry.msstore.clone().unwrap_or_default())?;
            if let Ok(info) = state.winget.show_store(&id).await {
                d.description = info.description.or(d.description);
                d.homepage = info.homepage.or(d.homepage);
                d.publisher = info.publisher.or(d.publisher);
                d.license = info.license;
            }
        }
    }
    Ok(d)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn home_has_featured_categories_and_full_bundles() {
        let cat = Catalog::embedded();
        let h = home(cat);
        assert!(h.featured.len() >= 12);
        assert!(h.featured.iter().all(|e| e.curated));
        assert_eq!(h.categories.len(), cat.categories.len());
        assert_eq!(
            h.categories.iter().map(|c| c.count).sum::<u32>() as usize,
            cat.apps.len()
        );
        let dev = h.bundles.iter().find(|b| b.id == "developer").unwrap();
        assert_eq!(
            dev.apps.len(),
            cat.bundles
                .iter()
                .find(|b| b.id == "developer")
                .unwrap()
                .apps
                .len()
        );
    }

    #[test]
    fn category_lists_sorted_apps_and_unknown_is_empty() {
        let cat = Catalog::embedded();
        let browsers = category(cat, "browsers");
        assert!(browsers.iter().any(|e| e.key == "firefox"));
        let names: Vec<_> = browsers.iter().map(|e| e.name.to_lowercase()).collect();
        let mut sorted = names.clone();
        sorted.sort();
        assert_eq!(names, sorted);
        assert!(category(cat, "nope").is_empty());
    }
}
