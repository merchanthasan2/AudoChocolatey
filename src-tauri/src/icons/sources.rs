//! Pure helpers for finding an app's original icon: registry values, install folders, MSIX
//! manifests and publisher web pages.

use regex::Regex;
use std::path::PathBuf;
use std::sync::LazyLock;

/// `"C:\path\app.exe",0` / `C:\x.ico` / `C:\y.exe,-101` -> (path, resource index).
pub fn parse_display_icon(value: &str) -> Option<(PathBuf, i32)> {
    let v = value.trim();
    if v.is_empty() {
        return None;
    }
    let (path, index) = match v.rsplit_once(',') {
        Some((p, i)) if i.trim().parse::<i32>().is_ok() => (p, i.trim().parse().unwrap_or(0)),
        _ => (v, 0),
    };
    let path = path.trim().trim_matches('"').trim();
    (!path.is_empty()).then(|| (PathBuf::from(path), index))
}

static HELPER: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"(?i)^(unins\d*|uninstall|update|updater|setup|install|crash|crashpad|helper|elevate|notification|squirrel|maintenance|repair)").unwrap()
});
static VERSION_TAIL: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"\s+v?\d+(\.\d+)+\s*$").unwrap());

fn alnum(s: &str) -> String {
    s.chars()
        .filter(|c| c.is_alphanumeric())
        .flat_map(char::to_lowercase)
        .collect()
}

/// The app's main program among `.exe` file names, or None if nothing plausibly matches.
pub fn choose_exe(names: &[String], app_name: &str) -> Option<String> {
    let tokens: Vec<String> = app_name
        .split(|c: char| !c.is_alphanumeric())
        .map(alnum)
        .filter(|t| t.len() >= 3)
        .collect();
    let joined = alnum(app_name);
    names
        .iter()
        .filter(|n| n.to_ascii_lowercase().ends_with(".exe"))
        .filter(|n| !HELPER.is_match(n))
        .filter_map(|n| {
            let stem = alnum(n.trim_end_matches(|c| c != '.').trim_end_matches('.'));
            let score = if tokens.contains(&stem) {
                3
            } else if stem.len() >= 3 && joined.starts_with(&stem) {
                2
            } else if stem.len() >= 4 && joined.contains(&stem) {
                1
            } else {
                0
            };
            (score > 0).then_some((score, stem.len(), n.clone()))
        })
        .max_by_key(|(score, len, _)| (*score, *len))
        .map(|(_, _, n)| n)
}

/// Same app name, ignoring case and a trailing version number.
pub fn name_matches(a: &str, b: &str) -> bool {
    let norm = |s: &str| VERSION_TAIL.replace(s.trim(), "").to_lowercase();
    norm(a) == norm(b)
}

static LINK: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"(?is)<link\b[^>]*>").unwrap());
static ATTR: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r#"(?is)([a-z-]+)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))"#).unwrap()
});

/// Best publisher icon from a homepage: apple-touch-icon, then the largest sized icon, then any icon.
pub fn pick_icon_link(html: &str, base: &str) -> Option<String> {
    let base = reqwest::Url::parse(base).ok()?;
    let mut best: Option<(u32, u32, String)> = None; // (priority, size, url); lower priority wins
    for tag in LINK.find_iter(html) {
        let mut rel = String::new();
        let mut href = String::new();
        let mut sizes = String::new();
        for c in ATTR.captures_iter(tag.as_str()) {
            let val = c
                .get(2)
                .or(c.get(3))
                .or(c.get(4))
                .map(|m| m.as_str())
                .unwrap_or("");
            match c[1].to_ascii_lowercase().as_str() {
                "rel" => rel = val.to_ascii_lowercase(),
                "href" => href = val.trim().to_string(),
                "sizes" => sizes = val.to_ascii_lowercase(),
                _ => {}
            }
        }
        let priority = if rel.contains("apple-touch-icon") {
            0
        } else if rel.split_whitespace().any(|r| r == "icon") {
            1
        } else {
            continue;
        };
        let Ok(url) = base.join(&href) else { continue };
        if url.scheme() != "https" {
            continue;
        }
        let size = sizes
            .split_whitespace()
            .filter_map(|s| s.split_once('x').and_then(|(w, _)| w.parse::<u32>().ok()))
            .max()
            .unwrap_or(0);
        let better = match &best {
            None => true,
            Some((p, s, _)) => priority < *p || (priority == *p && size > *s),
        };
        if better {
            best = Some((priority, size, url.to_string()));
        }
    }
    best.map(|(_, _, u)| u)
}

static LOGO: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"(?s)<Logo>\s*([^<]+?)\s*</Logo>").unwrap());

/// The package logo path declared in an MSIX `AppxManifest.xml`.
pub fn msix_logo(manifest: &str) -> Option<String> {
    LOGO.captures(manifest).map(|c| c[1].to_string())
}

/// The Store listing's own logo: the largest "logo" image, else the "tile" closest to 150 px.
/// Only https images on Microsoft's Store image host are accepted.
pub fn store_icon_url(json: &str) -> Option<String> {
    let v: serde_json::Value = serde_json::from_str(json).ok()?;
    let images = v.get("Payload")?.get("Images")?.as_array()?;
    let ok = |u: &str| u.starts_with("https://store-images.s-microsoft.com/");
    let pick = |kind: &str| {
        images
            .iter()
            .filter(|i| i.get("ImageType").and_then(|t| t.as_str()) == Some(kind))
            .filter_map(|i| {
                let url = i.get("Url")?.as_str()?;
                let w = i.get("Width").and_then(|w| w.as_u64()).unwrap_or(0);
                ok(url).then(|| (w, url.to_string()))
            })
            .collect::<Vec<_>>()
    };
    if let Some((_, u)) = pick("logo").into_iter().max_by_key(|(w, _)| *w) {
        return Some(u);
    }
    pick("tile")
        .into_iter()
        .min_by_key(|(w, _)| w.abs_diff(150))
        .map(|(_, u)| u)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn store_logo_is_the_largest_logo_image() {
        let json = include_str!("../../tests/fixtures/winget/store-product-whatsapp.json");
        let url = store_icon_url(json).unwrap();
        assert!(
            url.starts_with("https://store-images.s-microsoft.com/image/apps.8453."),
            "the 300 px logo: {url}"
        );
        assert_eq!(store_icon_url(r#"{"Payload":{"Images":[]}}"#), None);
        assert_eq!(store_icon_url("not json"), None);
        // Only https images on Microsoft's image host are accepted.
        let evil = r#"{"Payload":{"Images":[{"ImageType":"logo","Width":100,"Url":"http://evil.example/x.png"}]}}"#;
        assert_eq!(store_icon_url(evil), None);
        let tile = r#"{"Payload":{"Images":[{"ImageType":"tile","Width":150,"Url":"https://store-images.s-microsoft.com/image/t.png"},{"ImageType":"screenshot","Width":1920,"Url":"https://store-images.s-microsoft.com/image/s.png"}]}}"#;
        assert_eq!(
            store_icon_url(tile).as_deref(),
            Some("https://store-images.s-microsoft.com/image/t.png")
        );
    }
    use std::path::PathBuf;

    #[test]
    fn parses_display_icon_forms() {
        assert_eq!(
            parse_display_icon(r#""C:\a\b.exe",0"#),
            Some((PathBuf::from(r"C:\a\b.exe"), 0))
        );
        assert_eq!(
            parse_display_icon(r"C:\x.ico"),
            Some((PathBuf::from(r"C:\x.ico"), 0))
        );
        assert_eq!(
            parse_display_icon(r"C:\y.exe,-101"),
            Some((PathBuf::from(r"C:\y.exe"), -101))
        );
        assert_eq!(
            parse_display_icon(
                r"C:\Users\m\AppData\Local\Package Cache\{bdda}\python-3.13.15-amd64.exe,0"
            ),
            Some((
                PathBuf::from(
                    r"C:\Users\m\AppData\Local\Package Cache\{bdda}\python-3.13.15-amd64.exe"
                ),
                0
            ))
        );
        assert_eq!(parse_display_icon(""), None);
        assert_eq!(parse_display_icon("  "), None);
    }

    #[test]
    fn chooses_the_main_exe_not_helpers() {
        let pick = |names: &[&str], app: &str| {
            choose_exe(
                &names.iter().map(|s| s.to_string()).collect::<Vec<_>>(),
                app,
            )
        };
        assert_eq!(
            pick(
                &[
                    "unins000.exe",
                    "Update.exe",
                    "Discord.exe",
                    "crashpad_handler.exe"
                ],
                "Discord"
            )
            .as_deref(),
            Some("Discord.exe")
        );
        assert_eq!(
            pick(&["AcroCEF.exe", "Acrobat.exe"], "Adobe Acrobat (64-bit)").as_deref(),
            Some("Acrobat.exe")
        );
        assert_eq!(pick(&["unins000.exe", "Update.exe"], "Thing"), None);
        assert_eq!(pick(&["helper.exe"], "Totally Different"), None);
    }

    #[test]
    fn name_matching_ignores_versions_but_not_words() {
        assert!(name_matches(
            "CrystalDiskInfo 9.9.2",
            "CrystalDiskInfo 9.9.3"
        ));
        assert!(name_matches(
            "Microsoft Visual Studio Code (User)",
            "microsoft visual studio code (user)"
        ));
        assert!(!name_matches("Git", "GitHub CLI"));
        assert!(!name_matches(
            "Python 3.13.15 (64-bit)",
            "Python 3.13.15 Test Suite (64-bit)"
        ));
    }

    #[test]
    fn picks_the_best_icon_link() {
        let html = r#"<html><head>
            <link rel="icon" href="/favicon-16.png" sizes="16x16">
            <link rel="icon" type="image/png" href="/favicon-96.png" sizes="96x96">
            <link rel="apple-touch-icon" href="/apple-touch-icon.png">
            <link rel="icon" href="data:image/png;base64,AAAA">
        </head></html>"#;
        assert_eq!(
            pick_icon_link(html, "https://example.com/en/").as_deref(),
            Some("https://example.com/apple-touch-icon.png")
        );
        let no_apple = r#"<link rel="shortcut icon" href="img/fav.ico"><link rel="icon" href="//cdn.example.com/i-192.png" sizes="192x192">"#;
        assert_eq!(
            pick_icon_link(no_apple, "https://example.com/app/").as_deref(),
            Some("https://cdn.example.com/i-192.png")
        );
        assert_eq!(
            pick_icon_link(
                r#"<link rel="icon" href="http://insecure/x.png">"#,
                "https://example.com/"
            ),
            None
        );
        assert_eq!(
            pick_icon_link("<p>nothing</p>", "https://example.com/"),
            None
        );
    }

    #[test]
    fn reads_msix_logo() {
        let xml = r#"<Package><Properties><DisplayName>Xbox</DisplayName><Logo>Assets\StoreLogo.png</Logo></Properties></Package>"#;
        assert_eq!(msix_logo(xml).as_deref(), Some(r"Assets\StoreLogo.png"));
        assert_eq!(msix_logo("<Package/>"), None);
    }
}
