//! `winget show` parsing: `Key: value` lines (installer details are indented). URLs must be https.

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct ShowInfo {
    pub installer_url: Option<String>,
    pub homepage: Option<String>,
    pub publisher: Option<String>,
    pub description: Option<String>,
    pub version: Option<String>,
    pub license: Option<String>,
}

pub fn parse_show(stdout: &str) -> ShowInfo {
    let mut s = ShowInfo::default();
    let https = |v: &str| v.starts_with("https://").then(|| v.to_string());
    for line in stdout.lines() {
        let Some((key, value)) = line.trim().split_once(": ") else {
            continue;
        };
        let value = value.trim();
        if value.is_empty() {
            continue;
        }
        let slot = match key {
            "Installer Url" => (&mut s.installer_url, https(value)),
            "Homepage" => (&mut s.homepage, https(value)),
            "Publisher" => (&mut s.publisher, Some(value.to_string())),
            "Description" => (&mut s.description, Some(value.to_string())),
            "Version" => (&mut s.version, Some(value.to_string())),
            "License" => (&mut s.license, Some(value.to_string())),
            _ => continue,
        };
        if slot.0.is_none() {
            *slot.0 = slot.1;
        }
    }
    s
}

#[cfg(test)]
mod tests {
    use super::*;

    const SHOW: &str = include_str!("../../../tests/fixtures/winget/show-firefox.en.txt");

    #[test]
    fn parses_show_output() {
        let s = parse_show(SHOW);
        assert!(s
            .installer_url
            .as_deref()
            .unwrap()
            .starts_with("https://download-installer.cdn.mozilla.net/"));
        assert_eq!(
            s.homepage.as_deref(),
            Some("https://www.mozilla.org/firefox/")
        );
        assert_eq!(s.publisher.as_deref(), Some("Mozilla"));
        assert_eq!(s.version.as_deref(), Some("157.0.1"));
        assert_eq!(s.license.as_deref(), Some("MPL-2.0"));
        assert!(s
            .description
            .as_deref()
            .unwrap()
            .starts_with("Firefox Browser"));
    }

    #[test]
    fn non_https_urls_are_dropped() {
        let s = parse_show("Homepage: http://example.com\n  Installer Url: file:///C:/evil.exe\n");
        assert_eq!(s.homepage, None);
        assert_eq!(s.installer_url, None);
    }
}
