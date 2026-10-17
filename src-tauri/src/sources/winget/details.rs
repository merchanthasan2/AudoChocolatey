//! `winget show` parsing: `Key: value` lines (installer details are indented). URLs must be https.

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct ShowInfo {
    pub installer_url: Option<String>,
    pub homepage: Option<String>,
    pub publisher: Option<String>,
    pub description: Option<String>,
    pub version: Option<String>,
    pub license: Option<String>,
    pub release_notes_url: Option<String>,
    /// `YYYY-MM-DD`, only when it looks like one.
    pub release_date: Option<String>,
}

/// A plain `YYYY-MM-DD` date.
pub fn valid_date(s: &str) -> bool {
    let b = s.as_bytes();
    b.len() == 10
        && b[4] == b'-'
        && b[7] == b'-'
        && b.iter()
            .enumerate()
            .all(|(i, c)| i == 4 || i == 7 || c.is_ascii_digit())
}

pub fn parse_show(stdout: &str) -> ShowInfo {
    let mut s = ShowInfo::default();
    let https = |v: &str| crate::safe_url::safe_url(v).map(|u| u.to_string());
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
            "Release Notes Url" => (&mut s.release_notes_url, https(value)),
            "Release Date" => (
                &mut s.release_date,
                valid_date(value).then(|| value.to_string()),
            ),
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
        assert_eq!(
            s.release_notes_url.as_deref(),
            Some("https://www.firefox.com/en-US/firefox/157.0.1/releasenotes/")
        );
        assert_eq!(s.release_date.as_deref(), Some("2026-10-06"));
        assert!(s
            .description
            .as_deref()
            .unwrap()
            .starts_with("Firefox Browser"));
    }

    #[test]
    fn odd_dates_and_unsafe_links_are_dropped() {
        let s = parse_show("Release Date: 6 Oct 2026\nRelease Notes Url: https://127.0.0.1/x\n");
        assert_eq!(s.release_date, None);
        assert_eq!(s.release_notes_url, None);
    }

    #[test]
    fn non_https_urls_are_dropped() {
        let s = parse_show("Homepage: http://example.com\n  Installer Url: file:///C:/evil.exe\n");
        assert_eq!(s.homepage, None);
        assert_eq!(s.installer_url, None);
    }
}
