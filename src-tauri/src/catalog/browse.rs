//! "More apps" for a category: winget packages with the category's tags, minus the catalog's
//! own apps, pre-release channels and duplicates. Clearly labelled as not checked by us.

use super::merge::normalize;
use super::Catalog;
use crate::sources::model::SourcePackage;
use std::collections::HashSet;

pub const MAX_MORE: usize = 200;

const CHANNELS: [&str; 11] = [
    "beta", "dev", "nightly", "canary", "preview", "insider", "insiders", "alpha", "rc", "daily",
    "unstable",
];

pub fn tags_for(category: &str, cat: &Catalog) -> Vec<String> {
    cat.categories
        .iter()
        .find(|c| c.id == category)
        .map(|c| c.tags.clone())
        .unwrap_or_default()
}

fn pre_release(p: &SourcePackage) -> bool {
    let last =
        p.id.rsplit('.')
            .next()
            .unwrap_or_default()
            .to_ascii_lowercase();
    let words: Vec<String> = p
        .name
        .split(|c: char| !c.is_alphanumeric())
        .map(str::to_lowercase)
        .collect();
    CHANNELS
        .iter()
        .any(|w| last == *w || words.iter().any(|x| x == w))
}

/// Drops catalog apps, pre-release channels and same-name duplicates; names that start with a
/// letter first, then alphabetical; at most `MAX_MORE`.
pub fn filter_more(raw: Vec<SourcePackage>, cat: &Catalog) -> Vec<SourcePackage> {
    let mut seen = HashSet::new();
    let mut out: Vec<SourcePackage> = raw
        .into_iter()
        .filter(|p| cat.by_winget(&p.id).is_none() && !pre_release(p))
        .filter(|p| {
            let n = normalize(&p.name);
            !n.is_empty() && seen.insert(n)
        })
        .collect();
    out.sort_by_key(|p| {
        (
            !p.name.starts_with(|c: char| c.is_ascii_alphabetic()),
            p.name.to_lowercase(),
        )
    });
    out.truncate(MAX_MORE);
    out
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sources::fake::FakeRunner;
    use crate::sources::winget::WingetSource;

    #[test]
    fn every_category_has_safe_tags() {
        let re = regex::Regex::new(r"^[a-z0-9][a-z0-9-]{0,39}$").unwrap();
        for c in &Catalog::embedded().categories {
            assert!(!c.tags.is_empty(), "{}", c.id);
            for t in &c.tags {
                assert!(re.is_match(t), "{t}");
            }
        }
    }

    #[tokio::test]
    async fn more_apps_skip_catalog_apps_previews_and_duplicates() {
        let text = include_str!("../../tests/fixtures/winget/search-tag-browser.en.txt");
        let s = WingetSource::new(
            FakeRunner::new().on("search", 0, text),
            Some("winget.exe".into()),
        );
        let raw = s.search_tag("browser").await.unwrap();
        assert!(raw.len() > 300, "{}", raw.len());
        assert!(raw
            .iter()
            .any(|p| p.id == "Mozilla.Firefox.Nightly" || p.id.contains("Nightly")));
        let more = filter_more(raw.clone(), Catalog::embedded());
        assert!(more.len() <= MAX_MORE);
        assert!(!more.is_empty());
        let cat = Catalog::embedded();
        for p in &more {
            assert!(cat.by_winget(&p.id).is_none(), "catalog app {}", p.id);
            for w in ["beta", "nightly", "canary", "preview", "insider", "dev"] {
                let last = p.id.rsplit('.').next().unwrap().to_lowercase();
                assert_ne!(last, w, "{}", p.id);
            }
            assert!(!p.name.to_lowercase().contains("nightly"), "{}", p.name);
        }
        let names: Vec<String> = more
            .iter()
            .map(|p| crate::catalog::merge::normalize(&p.name))
            .collect();
        let mut dedup = names.clone();
        dedup.sort();
        dedup.dedup();
        assert_eq!(dedup.len(), names.len(), "no duplicate names");
        // Names starting with a letter come first, alphabetically.
        let first = &more[0].name;
        assert!(
            first.starts_with(|c: char| c.is_ascii_alphabetic()),
            "{first}"
        );
    }

    #[test]
    fn unknown_categories_have_no_tags() {
        assert!(tags_for("nope", Catalog::embedded()).is_empty());
        assert_eq!(
            tags_for("browsers", Catalog::embedded()),
            ["browser", "web-browser"]
        );
    }
}
