//! Locale-agnostic parser for winget's tabular output.
//!
//! winget pads columns to the widest value when piped, and header words may be separated by a
//! single space, so column starts are the display-width offsets of the header tokens. Columns are
//! assigned by position (Name, Id, Version, [Available|Match], Source), never by header text, so
//! any Windows display language works. Slicing is by display width, so wide (CJK) names are safe.

use unicode_width::UnicodeWidthChar;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Row {
    pub name: String,
    pub id: String,
    pub version: String,
    /// "Available" (list/upgrade) or "Match" (search) when the table has 5 columns.
    pub extra: Option<String>,
    pub source: Option<String>,
}

/// A line as (display offset, char, width) triples.
fn cells_of(line: &str) -> Vec<(usize, char, usize)> {
    let mut off = 0;
    line.chars()
        .map(|c| {
            let w = c.width().unwrap_or(0);
            let item = (off, c, w);
            off += w;
            item
        })
        .collect()
}

fn display_width(line: &str) -> usize {
    line.chars().map(|c| c.width().unwrap_or(0)).sum()
}

/// Keep only what a terminal would show: text after the last carriage return, no trailing space.
fn normalize(raw: &str) -> String {
    let line = raw.strip_suffix('\r').unwrap_or(raw);
    let visible = line.rsplit('\r').next().unwrap_or(line);
    crate::sources::process::strip_ansi(visible)
        .trim_end()
        .to_string()
}

fn is_separator(line: &str) -> bool {
    let t = line.trim();
    t.len() >= 10 && t.chars().all(|c| c == '-')
}

/// Display offsets where each whitespace-separated header token starts.
fn column_starts(header: &str) -> Vec<usize> {
    let mut starts = Vec::new();
    let mut prev_space = true;
    for (off, c, _) in cells_of(header) {
        if !c.is_whitespace() && prev_space {
            starts.push(off);
        }
        prev_space = c.is_whitespace();
    }
    starts
}

/// A data row must have a space just before every column start it reaches.
fn aligned(cells: &[(usize, char, usize)], starts: &[usize]) -> bool {
    let width = cells.last().map(|(o, _, w)| o + w).unwrap_or(0);
    starts.iter().skip(1).filter(|&&s| s < width).all(|&s| {
        cells
            .iter()
            .find(|(o, _, w)| *o <= s - 1 && s - 1 < o + (*w).max(1))
            .is_some_and(|(_, c, _)| *c == ' ')
    })
}

fn slice(cells: &[(usize, char, usize)], from: usize, to: Option<usize>) -> String {
    cells
        .iter()
        .filter(|(o, _, _)| *o >= from && to.is_none_or(|t| *o < t))
        .map(|(_, c, _)| *c)
        .collect::<String>()
        .trim()
        .to_string()
}

fn to_row(cols: Vec<String>) -> Option<Row> {
    let get = |i: usize| cols.get(i).cloned().unwrap_or_default();
    let opt = |s: String| (!s.is_empty()).then_some(s);
    let (name, id, version) = (get(0), get(1), get(2));
    if name.is_empty() || id.is_empty() {
        return None;
    }
    let (extra, source) = match cols.len() {
        n if n >= 5 => (opt(get(3)), opt(get(n - 1))),
        4 => (None, opt(get(3))),
        _ => (None, None),
    };
    Some(Row {
        name,
        id,
        version,
        extra,
        source,
    })
}

/// Every table in the output (winget can print a second "explicit targeting" table).
pub fn parse_tables(stdout: &str) -> Vec<Vec<Row>> {
    let lines: Vec<String> = stdout.split('\n').map(normalize).collect();
    let mut tables = Vec::new();
    let mut i = 0;
    while i < lines.len() {
        if !is_separator(&lines[i]) {
            i += 1;
            continue;
        }
        let Some(header) = lines[..i].iter().rev().find(|l| !l.trim().is_empty()) else {
            i += 1;
            continue;
        };
        let starts = column_starts(header);
        let mut rows = Vec::new();
        i += 1;
        while i < lines.len() && !lines[i].trim().is_empty() && !is_separator(&lines[i]) {
            let cells = cells_of(&lines[i]);
            if !aligned(&cells, &starts)
                || display_width(&lines[i]) <= starts.get(1).copied().unwrap_or(0)
            {
                break;
            }
            let cols = (0..starts.len())
                .map(|k| slice(&cells, starts[k], starts.get(k + 1).copied()))
                .collect();
            match to_row(cols) {
                Some(r) => rows.push(r),
                None => break,
            }
            i += 1;
        }
        if !rows.is_empty() {
            tables.push(rows);
        }
    }
    tables
}

#[cfg(test)]
mod tests {
    use super::*;

    const LIST: &str = include_str!("../../../tests/fixtures/winget/list.en.txt");
    const LIST_SPINNER: &str = include_str!("../../../tests/fixtures/winget/list-spinner.en.txt");
    const UPGRADE: &str = include_str!("../../../tests/fixtures/winget/upgrade.en.txt");
    const UPGRADE_DE: &str = include_str!("../../../tests/fixtures/winget/upgrade.de.txt");
    const UPGRADE_JA: &str = include_str!("../../../tests/fixtures/winget/upgrade.ja.txt");
    const EXPLICIT: &str = include_str!("../../../tests/fixtures/winget/upgrade-explicit.en.txt");
    const SEARCH: &str = include_str!("../../../tests/fixtures/winget/search-firefox.en.txt");
    const NO_RESULTS: &str = include_str!("../../../tests/fixtures/winget/no-results.en.txt");

    fn rows(s: &str) -> Vec<Row> {
        parse_tables(s).into_iter().flatten().collect()
    }

    #[test]
    fn single_space_headers_parse() {
        let r = rows(UPGRADE);
        assert_eq!(r.len(), 1);
        assert_eq!(r[0].name, "CrystalDiskInfo 9.9.2");
        assert_eq!(r[0].id, "CrystalDewWorld.CrystalDiskInfo");
        assert_eq!(r[0].version, "9.9.2");
        assert_eq!(r[0].extra.as_deref(), Some("9.9.3"));
        assert_eq!(r[0].source.as_deref(), Some("winget"));
    }

    #[test]
    fn second_explicit_targeting_table_is_included() {
        let ids: Vec<_> = rows(EXPLICIT).into_iter().map(|r| r.id).collect();
        assert_eq!(
            ids,
            [
                "Microsoft.VisualStudioCode",
                "Discord.Discord",
                "Microsoft.WSL"
            ]
        );
    }

    #[test]
    fn german_headers_and_umlauts() {
        let r = rows(UPGRADE_DE);
        assert_eq!(r.len(), 2);
        assert_eq!(r[1].name, "Bildschirmfoto Größe");
        assert_eq!(r[1].id, "Contoso.Screenshot");
        assert_eq!(r[1].extra.as_deref(), Some("1.1"));
    }

    #[test]
    fn japanese_wide_characters_slice_by_display_width() {
        let r = rows(UPGRADE_JA);
        assert_eq!(r.len(), 2, "{r:?}");
        assert_eq!(r[0].id, "CrystalDewWorld.CrystalDiskInfo");
        assert_eq!(r[0].extra.as_deref(), Some("9.9.3"));
        assert_eq!(r[1].name, "メモ帳++");
        assert_eq!(r[1].id, "Notepad++.Notepad++");
        assert_eq!(r[1].version, "8.6");
        assert_eq!(r[1].source.as_deref(), Some("winget"));
    }

    #[test]
    fn real_installed_list_has_every_row_and_no_footer() {
        let r = rows(LIST);
        assert_eq!(r.len(), 245);
        assert_eq!(
            r.iter()
                .filter(|r| r.source.as_deref() == Some("winget"))
                .count(),
            96
        );
        let acrobat = r.iter().find(|r| r.id == "Adobe.Acrobat.Pro").unwrap();
        assert_eq!(acrobat.name, "Adobe Acrobat (64-bit)");
        let studio = r.iter().find(|r| r.name == "Android Studio").unwrap();
        assert_eq!(studio.id, r"ARP\Machine\X64\Android Studio");
        assert_eq!(studio.source, None);
    }

    #[test]
    fn spinner_prefix_is_ignored() {
        assert_eq!(rows(LIST_SPINNER), rows(LIST));
    }

    #[test]
    fn search_match_column() {
        let r = rows(SEARCH);
        assert_eq!(r.len(), 338);
        assert_eq!(r[0].id, "Mozilla.Firefox");
        assert_eq!(r[0].version, "157.0.1");
        assert_eq!(r[0].extra.as_deref(), Some("Moniker: firefox"));
    }

    #[test]
    fn no_table_means_no_rows() {
        assert!(parse_tables(NO_RESULTS).is_empty());
        assert!(parse_tables("").is_empty());
    }
}
