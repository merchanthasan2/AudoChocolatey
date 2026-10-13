//! The curated catalog (96 apps, 10 categories, 5 bundles) embedded in the app, plus the
//! `AppEntry` shape every list in the UI uses. Ids are validated against the live sources by
//! `scripts/validate-catalog.mjs`.

pub mod merge;
pub mod names;

use crate::ids::SourceKind;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::LazyLock;
use ts_rs::TS;

#[derive(Debug, Clone, Deserialize, Serialize, TS)]
#[ts(export)]
pub struct Category {
    pub id: String,
    pub name: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CatalogApp {
    pub key: String,
    pub name: String,
    pub publisher: String,
    pub category: String,
    pub blurb: String,
    pub winget: Option<String>,
    pub choco: Option<String>,
    /// Microsoft Store product id, when the Store has the same app.
    #[serde(default)]
    pub msstore: Option<String>,
    #[serde(default)]
    pub featured: bool,
}

#[derive(Debug, Clone, Deserialize, Serialize, TS)]
#[ts(export)]
pub struct Bundle {
    pub id: String,
    pub name: String,
    pub blurb: String,
    pub apps: Vec<String>,
}

/// One app as the UI shows it, whichever source(s) it comes from. When it comes back from the
/// UI (details panel), its ids are re-validated before use.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct AppEntry {
    pub key: String,
    pub name: String,
    pub publisher: Option<String>,
    pub blurb: Option<String>,
    pub category: Option<String>,
    pub winget: Option<String>,
    pub choco: Option<String>,
    pub msstore: Option<String>,
    pub version: Option<String>,
    pub homepage: Option<String>,
    /// Which source an install uses: winget, else Chocolatey, else the Store.
    pub best: SourceKind,
    pub curated: bool,
    /// `<source>:<id>` for the icon service.
    pub icon_key: String,
}

#[derive(Debug, Deserialize)]
pub struct Catalog {
    pub categories: Vec<Category>,
    pub apps: Vec<CatalogApp>,
    pub bundles: Vec<Bundle>,
    #[serde(skip)]
    by_winget: HashMap<String, usize>,
    #[serde(skip)]
    by_choco: HashMap<String, usize>,
    #[serde(skip)]
    by_msstore: HashMap<String, usize>,
    #[serde(skip)]
    by_key: HashMap<String, usize>,
}

static EMBEDDED: LazyLock<Catalog> = LazyLock::new(|| {
    let mut c: Catalog = serde_json::from_str(include_str!("../../catalog/apps.json"))
        .expect("catalog/apps.json is valid");
    for (i, a) in c.apps.iter().enumerate() {
        c.by_key.insert(a.key.clone(), i);
        if let Some(w) = &a.winget {
            c.by_winget.insert(w.to_ascii_lowercase(), i);
        }
        if let Some(ch) = &a.choco {
            c.by_choco.insert(ch.to_ascii_lowercase(), i);
        }
        if let Some(s) = &a.msstore {
            c.by_msstore.insert(s.to_ascii_uppercase(), i);
        }
    }
    c
});

impl Catalog {
    pub fn embedded() -> &'static Catalog {
        &EMBEDDED
    }

    pub fn by_key(&self, key: &str) -> Option<&CatalogApp> {
        self.by_key.get(key).map(|&i| &self.apps[i])
    }

    pub fn by_winget(&self, id: &str) -> Option<&CatalogApp> {
        self.by_winget
            .get(&id.to_ascii_lowercase())
            .map(|&i| &self.apps[i])
    }

    pub fn by_choco(&self, id: &str) -> Option<&CatalogApp> {
        self.by_choco
            .get(&id.to_ascii_lowercase())
            .map(|&i| &self.apps[i])
    }

    pub fn by_msstore(&self, id: &str) -> Option<&CatalogApp> {
        self.by_msstore
            .get(&id.to_ascii_uppercase())
            .map(|&i| &self.apps[i])
    }

    /// The catalog app a package in a given source belongs to.
    pub fn by_source(&self, source: SourceKind, id: &str) -> Option<&CatalogApp> {
        match source {
            SourceKind::Winget => self.by_winget(id),
            SourceKind::Choco => self.by_choco(id),
            SourceKind::Msstore => self.by_msstore(id),
        }
    }

    pub fn entry(&self, a: &CatalogApp) -> AppEntry {
        let best = if a.winget.is_some() {
            SourceKind::Winget
        } else if a.choco.is_some() {
            SourceKind::Choco
        } else {
            SourceKind::Msstore
        };
        let icon_key = match best {
            SourceKind::Winget => format!("winget:{}", a.winget.as_deref().unwrap_or_default()),
            SourceKind::Choco => format!("choco:{}", a.choco.as_deref().unwrap_or_default()),
            SourceKind::Msstore => {
                format!("msstore:{}", a.msstore.as_deref().unwrap_or_default())
            }
        };
        AppEntry {
            key: a.key.clone(),
            name: a.name.clone(),
            publisher: Some(a.publisher.clone()),
            blurb: Some(a.blurb.clone()),
            category: Some(a.category.clone()),
            winget: a.winget.clone(),
            choco: a.choco.clone(),
            msstore: a.msstore.clone(),
            version: None,
            homepage: None,
            best,
            curated: true,
            icon_key,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::{ChocoId, WingetId};
    use std::collections::HashSet;

    #[test]
    fn embedded_catalog_is_consistent() {
        let cat = Catalog::embedded();
        assert!(cat.apps.len() >= 90);
        let keys: HashSet<_> = cat.apps.iter().map(|a| a.key.as_str()).collect();
        assert_eq!(keys.len(), cat.apps.len(), "duplicate app keys");
        let cats: HashSet<_> = cat.categories.iter().map(|c| c.id.as_str()).collect();
        for a in &cat.apps {
            assert!(
                cats.contains(a.category.as_str()),
                "{}: unknown category {}",
                a.key,
                a.category
            );
            assert!(
                a.winget.is_some() || a.choco.is_some(),
                "{}: no source",
                a.key
            );
            if let Some(w) = &a.winget {
                assert!(
                    WingetId::try_from(w.clone()).is_ok(),
                    "{}: bad winget id",
                    a.key
                );
            }
            if let Some(c) = &a.choco {
                assert!(
                    ChocoId::try_from(c.clone()).is_ok(),
                    "{}: bad choco id",
                    a.key
                );
            }
        }
        for b in &cat.bundles {
            for k in &b.apps {
                assert!(
                    keys.contains(k.as_str()),
                    "bundle {} references unknown app {k}",
                    b.id
                );
            }
        }
    }

    #[test]
    fn looks_up_by_source_id_case_insensitively() {
        let cat = Catalog::embedded();
        assert_eq!(
            cat.by_winget("mozilla.firefox").map(|a| a.key.as_str()),
            Some("firefox")
        );
        assert_eq!(
            cat.by_choco("VSCODE").map(|a| a.key.as_str()),
            Some("vscode")
        );
        assert!(cat.by_winget("Nope.Nothing").is_none());
    }

    #[test]
    fn catalog_apps_become_entries_with_best_source() {
        let cat = Catalog::embedded();
        let ff = cat.entry(cat.by_key("firefox").unwrap());
        assert_eq!(ff.best, crate::ids::SourceKind::Winget);
        assert_eq!(ff.icon_key, "winget:Mozilla.Firefox");
        assert!(ff.curated);
        let fz = cat.entry(cat.by_key("filezilla").unwrap());
        assert_eq!(fz.best, crate::ids::SourceKind::Choco);
        assert_eq!(fz.icon_key, "choco:filezilla");
    }
}
