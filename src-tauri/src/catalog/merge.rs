//! Search results from winget and Chocolatey become one list of apps. A pair is merged only
//! when it's certain: a catalog mapping, or identical normalized names plus the winget
//! publisher appearing in the Chocolatey package plus a Chocolatey id that names the same app
//! (package titles alone can mislead: FirefoxESR is titled "Mozilla Firefox"). Installing the wrong thing as admin is worse
//! than showing a duplicate.

use super::{AppEntry, Catalog};
use crate::ids::SourceKind;
use crate::sources::model::SourcePackage;
use regex::Regex;
use std::collections::HashMap;
use std::sync::LazyLock;

static NOISE: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"(?i)\b(x64|x86|64-bit|32-bit|desktop)\b").unwrap());

/// Comparable form of an app name: no marks or architecture words; `+` -> `plus`. Bracketed
/// qualifiers stay, because "(MSIX)" or "(ESR)" make it a different package.
pub fn normalize(name: &str) -> String {
    let s = NOISE.replace_all(name, " ");
    s.replace('+', "plus")
        .chars()
        .filter(|c| c.is_alphanumeric())
        .flat_map(char::to_lowercase)
        .collect()
}

fn publisher_token(winget_id: &str) -> Option<String> {
    let t = normalize(winget_id.split('.').next()?);
    (t.len() >= 3).then_some(t)
}

fn same_app(w: &SourcePackage, c: &SourcePackage) -> bool {
    let (wn, cn) = (normalize(&w.name), normalize(&c.name));
    if wn.is_empty() || wn != cn {
        return false;
    }
    let cid = normalize(&c.id);
    let id_names_app = !cid.is_empty() && (wn.contains(&cid) || cid.contains(&wn));
    id_names_app && publisher_token(&w.id).is_some_and(|t| cid.contains(&t) || cn.contains(&t))
}

fn uncurated_winget(p: &SourcePackage) -> AppEntry {
    AppEntry {
        key: format!("w:{}", p.id),
        name: p.name.clone(),
        publisher: None,
        blurb: p.description.clone(),
        category: None,
        winget: Some(p.id.clone()),
        choco: None,
        version: p.version.clone(),
        homepage: p.homepage.clone(),
        best: SourceKind::Winget,
        curated: false,
        icon_key: format!("winget:{}", p.id),
    }
}

fn uncurated_choco(p: &SourcePackage) -> AppEntry {
    AppEntry {
        key: format!("c:{}", p.id),
        name: p.name.clone(),
        publisher: None,
        blurb: p.description.clone(),
        category: None,
        winget: None,
        choco: Some(p.id.clone()),
        version: p.version.clone(),
        homepage: p.homepage.clone(),
        best: SourceKind::Choco,
        curated: false,
        icon_key: format!("choco:{}", p.id),
    }
}

pub fn merge(
    winget: Vec<SourcePackage>,
    choco: Vec<SourcePackage>,
    cat: &Catalog,
) -> Vec<AppEntry> {
    let mut out: Vec<AppEntry> = Vec::new();
    let mut by_key: HashMap<String, usize> = HashMap::new();
    let mut winget_src: Vec<Option<SourcePackage>> = Vec::new();

    for p in winget {
        let entry = match cat.by_winget(&p.id) {
            Some(app) => AppEntry {
                version: p.version.clone(),
                ..cat.entry(app)
            },
            None => uncurated_winget(&p),
        };
        if by_key.contains_key(&entry.key) {
            continue;
        }
        by_key.insert(entry.key.clone(), out.len());
        winget_src.push((!entry.curated).then(|| p.clone()));
        out.push(entry);
    }

    for p in choco {
        if let Some(app) = cat.by_choco(&p.id) {
            match by_key.get(&app.key) {
                Some(&i) => out[i].choco = Some(p.id.clone()),
                None => {
                    let mut e = cat.entry(app);
                    e.choco = Some(p.id.clone());
                    if e.winget.is_none() {
                        e.version = p.version.clone();
                    }
                    by_key.insert(e.key.clone(), out.len());
                    winget_src.push(None);
                    out.push(e);
                }
            }
            continue;
        }
        let pair = winget_src
            .iter()
            .position(|w| w.as_ref().is_some_and(|w| same_app(w, &p)));
        match pair {
            Some(i) if out[i].choco.is_none() => {
                let e = &mut out[i];
                e.choco = Some(p.id.clone());
                if e.blurb.is_none() {
                    e.blurb = p.description.clone();
                }
                if e.homepage.is_none() {
                    e.homepage = p.homepage.clone();
                }
            }
            _ => {
                let e = uncurated_choco(&p);
                if !by_key.contains_key(&e.key) {
                    by_key.insert(e.key.clone(), out.len());
                    winget_src.push(None);
                    out.push(e);
                }
            }
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind;
    use crate::sources::model::SourcePackage;

    fn w(id: &str, name: &str) -> SourcePackage {
        SourcePackage {
            name: name.into(),
            id: id.into(),
            version: Some("1.0".into()),
            source: SourceKind::Winget,
            matched_on: None,
            description: None,
            homepage: None,
            icon_url: None,
        }
    }

    fn c(id: &str, title: &str) -> SourcePackage {
        SourcePackage {
            name: title.into(),
            id: id.into(),
            version: Some("1.0".into()),
            source: SourceKind::Choco,
            matched_on: None,
            description: Some(format!("{title} description")),
            homepage: Some("https://example.com".into()),
            icon_url: None,
        }
    }

    #[test]
    fn normalizes_names() {
        assert_eq!(normalize("Mozilla Firefox (MSIX)"), "mozillafirefoxmsix");
        assert_eq!(normalize("Notepad++ (64-bit x64)"), "notepadplusplus");
        assert_eq!(normalize("Contoso™ Widget Desktop"), "contosowidget");
        assert_eq!(normalize("Python 3.12"), "python312");
    }

    #[test]
    fn catalog_mapping_merges_both_sources() {
        let out = merge(
            vec![w("Mozilla.Firefox", "Mozilla Firefox (en-US)")],
            vec![c("Firefox", "Mozilla Firefox")],
            Catalog::embedded(),
        );
        assert_eq!(out.len(), 1);
        let e = &out[0];
        assert_eq!(e.key, "firefox");
        assert_eq!(e.name, "Firefox");
        assert_eq!(e.winget.as_deref(), Some("Mozilla.Firefox"));
        assert_eq!(e.choco.as_deref(), Some("Firefox"));
        assert_eq!(e.best, SourceKind::Winget);
        assert!(e.curated);
    }

    #[test]
    fn heuristic_merges_an_unmapped_pair() {
        let out = merge(
            vec![w("Contoso.Widget", "Contoso Widget")],
            vec![c("contoso-widget", "Contoso Widget")],
            Catalog::embedded(),
        );
        assert_eq!(out.len(), 1, "{out:?}");
        assert_eq!(out[0].winget.as_deref(), Some("Contoso.Widget"));
        assert_eq!(out[0].choco.as_deref(), Some("contoso-widget"));
        assert_eq!(out[0].blurb.as_deref(), Some("Contoso Widget description"));
        assert!(!out[0].curated);
    }

    #[test]
    fn near_misses_stay_separate() {
        let cases = [
            (
                w("Example.Python.3.11", "Python 3.11"),
                c("python312-x", "Python 3.12"),
            ),
            (
                w("Example.VSCode", "Visual Studio Code"),
                c("vscode-insiders-x", "Visual Studio Code Insiders"),
            ),
            (
                w("Example.Firefox", "Firefox"),
                c("firefox-dev-x", "Firefox Developer Edition"),
            ),
            (
                w("Notepad++.Plugins", "Notepad++"),
                c("notepad2", "Notepad2"),
            ),
            (w("Acme.Tool", "Tool"), c("tool", "Tool")),
            // Real case: the ESR package is titled plain "Mozilla Firefox".
            (
                w("Mozilla.Firefox.MSIX", "Mozilla Firefox (MSIX)"),
                c("FirefoxESR", "Mozilla Firefox"),
            ),
            (
                w("Mozilla.Firefox.ach", "Mozilla Firefox"),
                c("FirefoxESR", "Mozilla Firefox"),
            ),
        ];
        for (a, b) in cases {
            let out = merge(vec![a.clone()], vec![b.clone()], Catalog::embedded());
            assert_eq!(out.len(), 2, "{} must not merge with {}", a.name, b.name);
        }
    }

    #[test]
    fn choco_only_results_use_choco() {
        let out = merge(
            vec![],
            vec![c("some-tool", "Some Tool")],
            Catalog::embedded(),
        );
        assert_eq!(out[0].best, SourceKind::Choco);
        assert_eq!(out[0].key, "c:some-tool");
        assert_eq!(out[0].icon_key, "choco:some-tool");
        assert_eq!(out[0].homepage.as_deref(), Some("https://example.com"));
    }

    #[test]
    fn winget_order_first_then_choco_only() {
        let out = merge(
            vec![w("B.B", "Bee"), w("A.A", "Ay")],
            vec![c("zed", "Zed")],
            Catalog::embedded(),
        );
        let keys: Vec<_> = out.iter().map(|e| e.key.as_str()).collect();
        assert_eq!(keys, ["w:B.B", "w:A.A", "c:zed"]);
    }
}
