//! The only web addresses Quantum Updater keeps from package data: https, a real domain name
//! (no IP address, localhost or single-word host), the default port, no user name or password,
//! no control characters, and at most 2048 characters.

pub const MAX_LEN: usize = 2048;

pub fn safe_url(s: &str) -> Option<url::Url> {
    let s = s.trim();
    if s.is_empty() || s.len() > MAX_LEN || s.chars().any(|c| c.is_control() || c.is_whitespace()) {
        return None;
    }
    let u = url::Url::parse(s).ok()?;
    if u.scheme() != "https"
        || !u.username().is_empty()
        || u.password().is_some()
        || u.port().is_some()
    {
        return None;
    }
    let url::Host::Domain(host) = u.host()? else {
        return None;
    };
    let host = host.trim_end_matches('.');
    if !host.contains('.') || host.eq_ignore_ascii_case("localhost") || host.ends_with(".localhost")
    {
        return None;
    }
    Some(u)
}

/// The host to show next to a link ("www.mozilla.org").
pub fn host_of(u: &url::Url) -> Option<String> {
    u.host_str()
        .map(|h| h.trim_end_matches('.').to_ascii_lowercase())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn accepted() {
        for u in [
            "https://www.mozilla.org/en-US/firefox/131.0/releasenotes/",
            "https://github.com/videolan/vlc/releases",
            "https://example.co.uk/notes?v=1#top",
        ] {
            assert!(safe_url(u).is_some(), "{u}");
        }
    }

    #[test]
    fn refused() {
        let long = format!("https://example.com/{}", "a".repeat(2100));
        for u in [
            "http://example.com/",
            "file:///C:/Windows/notepad.exe",
            "javascript:alert(1)",
            "https://127.0.0.1/x",
            "https://[::1]/x",
            "https://localhost/x",
            "https://intranet/x",
            "https://a.localhost/x",
            "https://example.com:8443/x",
            "https://user:pw@example.com/x",
            "https://user@example.com/x",
            "https://exa mple.com/x",
            "https://example.com/a\nb",
            "ms-settings:windowsupdate",
            "",
            long.as_str(),
        ] {
            assert!(safe_url(u).is_none(), "{u}");
        }
    }

    #[test]
    fn hosts() {
        let u = safe_url("https://WWW.Mozilla.org./x").unwrap();
        assert_eq!(host_of(&u).as_deref(), Some("www.mozilla.org"));
    }
}
