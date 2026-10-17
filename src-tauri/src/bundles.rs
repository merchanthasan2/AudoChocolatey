//! "My bundles": app lists the person made from their own selection or an imported file.
//! Kept in `my-bundles.json`; every app is re-validated on load, so a forged file can only
//! list apps (shown with their ids when the catalog doesn't know them), never run anything.

use crate::apps_file::{clean_name, dedupe, validate, FileApp};
use crate::catalog::Catalog;
use crate::error::{AppError, AppResult};
use crate::fsutil::{read_json, set_aside, write_atomic, Read};
use serde::{Deserialize, Serialize};
use std::path::PathBuf;

pub const SCHEMA_VERSION: u32 = 1;
pub const MAX_BUNDLES: usize = 50;
pub const MAX_APPS: usize = 500;
const MAX_BYTES: u64 = 2 * 1024 * 1024;
const MAX_NAME: usize = 60;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MyBundle {
    pub id: String,
    pub name: String,
    pub apps: Vec<FileApp>,
    pub created_at: u64,
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct File {
    schema_version: u32,
    bundles: Vec<MyBundle>,
}

pub struct BundleStore {
    path: PathBuf,
    bundles: Vec<MyBundle>,
    writable: bool,
}

pub fn bundle_name(s: &str) -> AppResult<String> {
    let n: String = clean_name(s).chars().take(MAX_NAME).collect();
    if n.is_empty() {
        return Err(AppError::new("invalid_input", "Give the bundle a name."));
    }
    Ok(n)
}

/// Keeps only apps that pass validation, without duplicates.
fn checked(apps: Vec<FileApp>, cat: &Catalog) -> Vec<FileApp> {
    let apps = apps
        .into_iter()
        .filter_map(|a| validate(a.source, &a.id, &a.name, cat))
        .collect();
    let (mut apps, _) = dedupe(apps);
    apps.truncate(MAX_APPS);
    apps
}

impl BundleStore {
    pub fn open(path: PathBuf) -> Self {
        let cat = Catalog::embedded();
        let mut writable = true;
        let bundles = match read_json(&path, MAX_BYTES) {
            Read::Missing => Vec::new(),
            Read::Unreadable => {
                writable = false;
                Vec::new()
            }
            Read::Corrupt => {
                set_aside(&path);
                Vec::new()
            }
            Read::Json(v) => match serde_json::from_value::<File>(v) {
                Ok(f) if f.schema_version > SCHEMA_VERSION => {
                    writable = false;
                    Vec::new()
                }
                Ok(f) => f
                    .bundles
                    .into_iter()
                    .take(MAX_BUNDLES)
                    .filter_map(|b| {
                        Some(MyBundle {
                            name: bundle_name(&b.name).ok()?,
                            apps: checked(b.apps, cat),
                            ..b
                        })
                    })
                    .collect(),
                Err(_) => {
                    set_aside(&path);
                    Vec::new()
                }
            },
        };
        Self {
            path,
            bundles,
            writable,
        }
    }

    pub fn list(&self) -> &[MyBundle] {
        &self.bundles
    }

    pub fn get(&self, id: &str) -> Option<&MyBundle> {
        self.bundles.iter().find(|b| b.id == id)
    }

    /// Adds apps to an existing bundle (`to = Some(id)`) or a new one named `name`. Returns the
    /// bundle id.
    pub fn add(
        &mut self,
        to: Option<&str>,
        name: Option<&str>,
        apps: Vec<FileApp>,
        now: u64,
    ) -> AppResult<String> {
        let cat = Catalog::embedded();
        let apps = checked(apps, cat);
        if apps.is_empty() {
            return Err(AppError::new(
                "invalid_input",
                "None of those apps can be saved.",
            ));
        }
        let id = match to {
            Some(id) => {
                let b = self
                    .bundles
                    .iter_mut()
                    .find(|b| b.id == id)
                    .ok_or_else(|| {
                        AppError::new("invalid_input", "That bundle no longer exists.")
                    })?;
                let mut all = std::mem::take(&mut b.apps);
                all.extend(apps);
                b.apps = checked(all, cat);
                b.id.clone()
            }
            None => {
                if self.bundles.len() >= MAX_BUNDLES {
                    return Err(AppError::new(
                        "invalid_input",
                        "You have 50 bundles already. Delete one first.",
                    ));
                }
                let name = bundle_name(name.unwrap_or(""))?;
                let id = format!("my-{now}-{}", self.bundles.len());
                self.bundles.push(MyBundle {
                    id: id.clone(),
                    name,
                    apps,
                    created_at: now,
                });
                id
            }
        };
        self.save()?;
        Ok(id)
    }

    pub fn remove_app(&mut self, bundle: &str, identity: &str) -> AppResult<()> {
        if let Some(b) = self.bundles.iter_mut().find(|b| b.id == bundle) {
            b.apps.retain(|a| crate::apps_file::identity(a) != identity);
        }
        self.save()
    }

    pub fn delete(&mut self, bundle: &str) -> AppResult<()> {
        self.bundles.retain(|b| b.id != bundle);
        self.save()
    }

    fn save(&self) -> AppResult<()> {
        if !self.writable {
            return Err(AppError::new(
                "settings",
                "Your bundles were saved by a newer version of Quantum Updater and are read-only.",
            ));
        }
        let json = serde_json::to_vec_pretty(&File {
            schema_version: SCHEMA_VERSION,
            bundles: self.bundles.clone(),
        })
        .expect("bundles serialize");
        write_atomic(&self.path, &json)
            .map_err(|e| AppError::new("settings", format!("Couldn't save your bundles: {e}")))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind;

    fn app(id: &str) -> FileApp {
        FileApp {
            key: None,
            source: SourceKind::Winget,
            id: id.into(),
            name: id.into(),
        }
    }

    #[test]
    fn make_add_remove_and_delete() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("my-bundles.json");
        let mut s = BundleStore::open(p.clone());
        let id = s
            .add(
                None,
                Some("  Work PC "),
                vec![app("A.A"), app("a.a"), app("B.B")],
                5,
            )
            .unwrap();
        assert_eq!(s.get(&id).unwrap().name, "Work PC");
        assert_eq!(s.get(&id).unwrap().apps.len(), 2, "duplicates dropped");
        s.add(Some(&id), None, vec![app("C.C"), app("B.B")], 6)
            .unwrap();
        assert_eq!(s.get(&id).unwrap().apps.len(), 3);
        s.remove_app(&id, "winget:b.b").unwrap();
        assert_eq!(s.get(&id).unwrap().apps.len(), 2);

        let again = BundleStore::open(p.clone());
        assert_eq!(again.list(), s.list(), "survives a restart");
        s.delete(&id).unwrap();
        assert!(s.list().is_empty());
    }

    #[test]
    fn names_and_apps_are_required() {
        let dir = tempfile::tempdir().unwrap();
        let mut s = BundleStore::open(dir.path().join("b.json"));
        assert!(s.add(None, Some("  "), vec![app("A.A")], 1).is_err());
        assert!(s.add(None, Some("X"), vec![app("bad id\"")], 1).is_err());
        assert!(s.add(Some("missing"), None, vec![app("A.A")], 1).is_err());
    }

    #[test]
    fn forged_files_are_revalidated() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("b.json");
        std::fs::write(
            &p,
            r#"{"schemaVersion":1,"bundles":[{"id":"x","name":"Mine","createdAt":1,"apps":[
                {"source":"winget","id":"Ok.App","name":"Ok"},
                {"source":"winget","id":"--override evil","name":"Evil"},
                {"source":"winget","id":"Mozilla.Firefox","name":"Not Firefox"}]}]}"#,
        )
        .unwrap();
        let s = BundleStore::open(p);
        let apps = &s.list()[0].apps;
        assert_eq!(apps.len(), 2);
        assert_ne!(apps[1].name, "Not Firefox");
    }

    #[test]
    fn a_newer_file_is_read_only() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("b.json");
        std::fs::write(&p, r#"{"schemaVersion":9,"bundles":[]}"#).unwrap();
        let mut s = BundleStore::open(p.clone());
        assert!(s.add(None, Some("X"), vec![app("A.A")], 1).is_err());
        assert_eq!(
            std::fs::read_to_string(&p).unwrap(),
            r#"{"schemaVersion":9,"bundles":[]}"#
        );
    }
}
