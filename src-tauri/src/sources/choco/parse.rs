//! Chocolatey `-r` (limit-output) parsing. Non-elevated choco interleaves retry/warning noise
//! with data, so only lines shaped like `<valid-id>|...` are accepted.

use regex::Regex;
use std::sync::LazyLock;

static ROW: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"^[A-Za-z0-9][A-Za-z0-9._\-]*\|").unwrap());

pub fn pipe_rows(stdout: &str, min_fields: usize) -> Vec<Vec<String>> {
    stdout
        .lines()
        .map(str::trim)
        .filter(|l| ROW.is_match(l))
        .map(|l| {
            l.split('|')
                .map(|f| f.trim().to_string())
                .collect::<Vec<_>>()
        })
        .filter(|f| f.len() >= min_fields && f.iter().take(min_fields).all(|x| !x.is_empty()))
        .collect()
}

pub fn major(version_text: &str) -> Option<u32> {
    version_text.trim().split('.').next()?.parse().ok()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn keeps_only_pipe_rows_with_enough_fields() {
        let text = "This is try 1/3. Retrying\r\n Access to the path 'C:\\x' is denied.\r\nvscode|1.95.2\r\nbad row|1.0\r\n|missing\r\nonlyone\r\n";
        assert_eq!(
            pipe_rows(text, 2),
            vec![vec!["vscode".to_string(), "1.95.2".to_string()]]
        );
        assert!(pipe_rows("a|1", 3).is_empty());
    }

    #[test]
    fn major_version() {
        assert_eq!(major("2.7.4\r\n"), Some(2));
        assert_eq!(major("1.4.0"), Some(1));
        assert_eq!(major("garbage"), None);
    }
}
