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
    format!(
        "https://community.chocolatey.org/api/v2/FindPackagesById()?id={}&$filter=IsLatestVersion",
        encode(&format!("'{}'", id.as_str()))
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

fn plain(text: &str) -> String {
    let t = SPACE
        .replace_all(&TAGS.replace_all(text, " "), " ")
        .trim()
        .to_string();
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

pub fn parse_feed(xml: &str) -> AppResult<Vec<SourcePackage>> {
    let mut reader = Reader::from_str(xml);
    let mut in_entry = false;
    let mut current: Option<String> = None;
    let mut fields: HashMap<String, String> = HashMap::new();
    let mut found: Vec<(SourcePackage, bool)> = Vec::new();
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
                    if let Some(entry) = to_package(&fields) {
                        found.push(entry);
                    }
                }
                current = None;
            }
            Event::Eof => break,
            _ => {}
        }
    }
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
mod tests {
    use super::*;
    use crate::ids::SearchQuery;

    const FEED: &str = include_str!("../../../tests/fixtures/choco/odata-search-firefox.xml");

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
