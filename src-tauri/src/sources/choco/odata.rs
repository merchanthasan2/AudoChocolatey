//! Chocolatey community feed search over HTTPS (OData v2 Atom). Works without choco installed,
//! and returns descriptions and icon URLs the CLI doesn't. Text is plain; URLs must be https.

use crate::error::{AppError, AppResult};
use crate::ids::{ChocoId, SearchQuery, SourceKind};
use crate::sources::model::SourcePackage;
use quick_xml::events::Event;
use quick_xml::Reader;
use regex::Regex;
use std::collections::HashMap;
use std::sync::LazyLock;

const FEED: &str = "https://community.chocolatey.org/api/v2/Search()";
const MAX_DESCRIPTION: usize = 400;

pub fn search_url(q: &SearchQuery) -> String {
    let term = format!("'{}'", q.as_str().replace('\'', "''"));
    format!(
        "{FEED}?searchTerm={}&targetFramework=%27%27&includePrerelease=false&$filter=IsLatestVersion&$top=30",
        encode(&term)
    )
}

/// One package's latest version (for its IconUrl when it isn't in recent search results).
pub fn by_id_url(id: &ChocoId) -> String {
    let filter = format!(
        "tolower(Id) eq '{}' and IsLatestVersion",
        id.as_str().to_ascii_lowercase()
    );
    format!(
        "https://community.chocolatey.org/api/v2/Packages()?$filter={}&$top=1",
        encode(&filter)
    )
}

/// Every published version of one package (for going back to an earlier one).
pub fn versions_url(id: &ChocoId) -> String {
    let filter = format!("tolower(Id) eq '{}'", id.as_str().to_ascii_lowercase());
    format!(
        "https://community.chocolatey.org/api/v2/Packages()?$filter={}&$top=200",
        encode(&filter)
    )
}

/// RFC 3986 percent-encoding of everything outside the unreserved set.
fn encode(s: &str) -> String {
    s.bytes()
        .map(|b| match b {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'.' | b'_' | b'~' => {
                (b as char).to_string()
            }
            _ => format!("%{b:02X}"),
        })
        .collect()
}

static TAGS: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"<[^>]*>").unwrap());
static SPACE: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"\s+").unwrap());

static MD_IMAGE: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"!\[[^\]]*\]\([^)]*\)").unwrap());
static MD_LINK: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"\[([^\]]*)\]\([^)]*\)").unwrap());
static MD_BLOCK: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"(?m)^\s*(#{1,6}|[-*+]|>|\d+\.)\s+").unwrap());
static MD_INLINE: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"\*\*|__|[*`~]").unwrap());

/// Package descriptions are Markdown/HTML; the app shows them as one line of plain text.
fn plain(text: &str) -> String {
    let no_html = TAGS.replace_all(text, " ");
    let no_images = MD_IMAGE.replace_all(&no_html, " ");
    let links_as_text = MD_LINK.replace_all(&no_images, "$1");
    let unblocked = MD_BLOCK.replace_all(&links_as_text, " ");
    let unmarked = MD_INLINE.replace_all(&unblocked, "");
    let t = SPACE.replace_all(&unmarked, " ").trim().to_string();
    if t.chars().count() <= MAX_DESCRIPTION {
        return t;
    }
    let mut cut: String = t.chars().take(MAX_DESCRIPTION - 1).collect();
    cut.push('\u{2026}');
    cut
}

fn https(url: Option<&String>) -> Option<String> {
    url.filter(|u| u.starts_with("https://")).cloned()
}

fn named_entity(name: &str) -> Option<char> {
    Some(match name {
        "amp" => '&',
        "lt" => '<',
        "gt" => '>',
        "quot" => '"',
        "apos" => '\'',
        _ => return None,
    })
}

/// Each `<entry>`'s fields by local name.
fn entries(xml: &str) -> AppResult<Vec<HashMap<String, String>>> {
    let mut reader = Reader::from_str(xml);
    let mut in_entry = false;
    let mut current: Option<String> = None;
    let mut fields: HashMap<String, String> = HashMap::new();
    let mut out = Vec::new();
    loop {
        let event = reader
            .read_event()
            .map_err(|e| AppError::new("parse", format!("Chocolatey feed: {e}")))?;
        match event {
            Event::Start(e) => {
                let name = e.local_name().as_ref().to_string();
                if name == "entry" {
                    in_entry = true;
                    fields.clear();
                }
                current = in_entry.then_some(name);
            }
            Event::Text(t) => {
                if let Some(k) = &current {
                    fields
                        .entry(k.clone())
                        .or_default()
                        .push_str(&t.html_content());
                }
            }
            Event::GeneralRef(r) => {
                if let Some(k) = &current {
                    let c = r
                        .resolve_char_ref()
                        .ok()
                        .flatten()
                        .or_else(|| named_entity(&r.html_content()));
                    if let Some(c) = c {
                        fields.entry(k.clone()).or_default().push(c);
                    }
                }
            }
            Event::End(e) => {
                if e.local_name().as_ref() == "entry" {
                    in_entry = false;
                    out.push(std::mem::take(&mut fields));
                }
                current = None;
            }
            Event::Eof => break,
            _ => {}
        }
    }
    Ok(out)
}

/// What an update's "What's new" and release date come from.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FeedMeta {
    pub id: String,
    pub version: String,
    pub notes_url: Option<String>,
    pub released: Option<String>,
}

/// Every entry (callers match the exact version they need): release notes only when the whole field is one safe web
/// address, and the published date (`YYYY-MM-DD`).
pub fn feed_meta(xml: &str) -> AppResult<Vec<FeedMeta>> {
    Ok(entries(xml)?
        .iter()
        .filter_map(|f| {
            let id = f.get("title")?.trim().to_string();
            ChocoId::try_from(id.clone()).ok()?;
            let released = f
                .get("Published")
                .and_then(|p| p.trim().get(..10))
                .filter(|d| crate::sources::winget::details::valid_date(d))
                .map(String::from);
            Some(FeedMeta {
                id,
                version: f.get("Version")?.trim().to_string(),
                notes_url: f
                    .get("ReleaseNotes")
                    .and_then(|n| crate::safe_url::safe_url(n.trim()))
                    .map(|u| u.to_string()),
                released,
            })
        })
        .collect())
}

pub fn parse_feed(xml: &str) -> AppResult<Vec<SourcePackage>> {
    let found: Vec<(SourcePackage, bool)> = entries(xml)?.iter().filter_map(to_package).collect();
    let mut out: Vec<SourcePackage> = Vec::new();
    let mut latest: HashMap<String, bool> = HashMap::new();
    for (pkg, is_latest) in found {
        match latest.get(&pkg.id) {
            None => {
                latest.insert(pkg.id.clone(), is_latest);
                out.push(pkg);
            }
            Some(false) if is_latest => {
                latest.insert(pkg.id.clone(), true);
                if let Some(slot) = out.iter_mut().find(|p| p.id == pkg.id) {
                    *slot = pkg;
                }
            }
            _ => {}
        }
    }
    Ok(out)
}

fn to_package(f: &HashMap<String, String>) -> Option<(SourcePackage, bool)> {
    let id = f.get("title")?.trim().to_string();
    ChocoId::try_from(id.clone()).ok()?;
    let title = f
        .get("Title")
        .map(|t| t.trim().to_string())
        .filter(|t| !t.is_empty());
    let pkg = SourcePackage {
        name: title.unwrap_or_else(|| id.clone()),
        version: f
            .get("Version")
            .map(|v| v.trim().to_string())
            .filter(|v| !v.is_empty()),
        description: f
            .get("Description")
            .map(|d| plain(d))
            .filter(|d| !d.is_empty()),
        homepage: https(f.get("ProjectUrl")),
        icon_url: https(f.get("IconUrl")),
        matched_on: None,
        source: SourceKind::Choco,
        id,
    };
    let is_latest = f.get("IsLatestVersion").is_some_and(|v| v.trim() == "true");
    Some((pkg, is_latest))
}

#[cfg(test)]
mod meta_tests {
    use super::*;

    #[test]
    fn release_notes_and_dates_from_the_feed() {
        let xml = include_str!("../../../tests/fixtures/choco/odata-search-firefox.xml");
        let m = feed_meta(xml).unwrap();
        let ff = m
            .iter()
            .find(|m| m.id.eq_ignore_ascii_case("firefox") && m.version == "141.0.0")
            .expect("firefox 141");
        assert_eq!(
            ff.notes_url.as_deref(),
            Some("https://www.mozilla.org/en-US/firefox/141.0/releasenotes/")
        );
        assert_eq!(ff.released.as_deref(), Some("2025-07-22"));
        // Release notes written as text (not a single link) give no link.
        let vs = include_str!("../../../tests/fixtures/choco/odata-by-id-vsbuildtools.xml");
        assert!(feed_meta(vs).unwrap().iter().all(|m| m.notes_url.is_none()));
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SearchQuery;

    const FEED: &str = include_str!("../../../tests/fixtures/choco/odata-search-firefox.xml");

    #[test]
    fn by_id_feed_gives_the_package_title() {
        let xml = include_str!("../../../tests/fixtures/choco/odata-by-id-vsbuildtools.xml");
        let r = parse_feed(xml).unwrap();
        assert_eq!(r.len(), 1, "{r:?}");
        assert_eq!(r[0].name, "Visual Studio 2022 Build Tools");
    }

    #[test]
    fn parses_and_dedupes_feed() {
        let r = parse_feed(FEED).unwrap();
        assert_eq!(r.len(), 1, "{r:?}");
        let f = &r[0];
        assert_eq!(f.id, "Firefox");
        assert_eq!(f.name, "Mozilla Firefox");
        assert_eq!(f.version.as_deref(), Some("141.0.0"));
        assert_eq!(f.source, SourceKind::Choco);
        assert!(f
            .icon_url
            .as_deref()
            .unwrap()
            .starts_with("https://cdn.jsdelivr.net/"));
        let d = f.description.as_deref().unwrap();
        assert!(!d.is_empty() && !d.contains('<') && d.chars().count() <= 400);
    }

    #[test]
    fn descriptions_are_plain_text_without_markdown() {
        let md = "## Bitdefender TrafficLight ![Screenshot](https://x/y.png) **Safer** browsing. See [the site](https://bitdefender.com) for `details`.

- Blocks *bad* links";
        assert_eq!(
            plain(md),
            "Bitdefender TrafficLight Safer browsing. See the site for details. Blocks bad links"
        );
    }

    /// FindPackagesById + IsLatestVersion returns an empty feed (found live); ids are stored
    /// with capitals ("Firefox"), so the filter must compare case-insensitively.
    #[test]
    fn by_id_url_uses_a_case_insensitive_latest_filter() {
        let url = by_id_url(&ChocoId::try_from("vcredist140".to_string()).unwrap());
        assert!(
            url.starts_with("https://community.chocolatey.org/api/v2/Packages()?"),
            "{url}"
        );
        assert!(
            url.contains("tolower%28Id%29%20eq%20%27vcredist140%27"),
            "{url}"
        );
        assert!(url.contains("IsLatestVersion"), "{url}");
        assert!(!url.contains("FindPackagesById"), "{url}");
    }

    #[test]
    fn non_https_urls_are_dropped() {
        let xml = r#"<feed xmlns="http://www.w3.org/2005/Atom" xmlns:d="http://schemas.microsoft.com/ado/2007/08/dataservices" xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"><entry><title type="text">demo</title><m:properties><d:Version>1.0</d:Version><d:Title>Demo</d:Title><d:IconUrl>http://insecure/x.png</d:IconUrl><d:ProjectUrl>javascript:alert(1)</d:ProjectUrl><d:IsLatestVersion m:type="Edm.Boolean">true</d:IsLatestVersion></m:properties></entry></feed>"#;
        let r = parse_feed(xml).unwrap();
        assert_eq!(r[0].icon_url, None);
        assert_eq!(r[0].homepage, None);
    }

    #[test]
    fn invalid_ids_are_dropped() {
        let xml = r#"<feed xmlns="http://www.w3.org/2005/Atom" xmlns:d="x" xmlns:m="y"><entry><title type="text">-evil</title><m:properties><d:Version>1</d:Version></m:properties></entry></feed>"#;
        assert!(parse_feed(xml).unwrap().is_empty());
    }

    #[test]
    fn url_escapes_quotes_and_filters_latest() {
        let url = search_url(&SearchQuery::try_from("o'neil".to_string()).unwrap());
        assert!(url.starts_with("https://community.chocolatey.org/api/v2/Search()?"));
        assert!(url.contains("searchTerm=%27o%27%27neil%27"), "{url}");
        assert!(url.contains("$filter=IsLatestVersion"), "{url}");
        assert!(url.contains("includePrerelease=false"));
    }
}
