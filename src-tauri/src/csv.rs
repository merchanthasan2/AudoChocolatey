//! "Save as spreadsheet": the shown list as CSV. Cells are written so a spreadsheet never runs
//! them as formulas (a package name like `=HYPERLINK(...)` stays text).

pub const MAX_ROWS: usize = 5000;
const MAX_CELL: usize = 512;

/// One cell: control characters become spaces, a leading formula character gets a `'`, and the
/// cell is always quoted.
pub fn csv_cell(s: &str) -> String {
    let flat: String = s
        .chars()
        .map(|c| if c.is_control() { ' ' } else { c })
        .take(MAX_CELL)
        .collect();
    let first = flat.trim_start().chars().next();
    let formula = matches!(
        first,
        Some('=' | '+' | '-' | '@' | '\u{FF1D}' | '\u{FF0B}' | '\u{FF0D}' | '\u{FF20}')
    );
    let body = if formula { format!("'{flat}") } else { flat };
    format!("\"{}\"", body.replace('"', "\"\""))
}

/// UTF-8 with a byte-order mark (so Excel reads accents) and Windows line endings.
pub fn to_csv(header: &[&str], rows: &[Vec<String>]) -> String {
    let mut out = String::from("\u{FEFF}");
    let line = |cells: Vec<String>| cells.join(",") + "\r\n";
    out.push_str(&line(header.iter().map(|h| csv_cell(h)).collect()));
    for r in rows {
        out.push_str(&line(r.iter().map(|c| csv_cell(c)).collect()));
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn cells() {
        let cases = [
            ("Firefox", "\"Firefox\""),
            ("Say \"hi\"", "\"Say \"\"hi\"\"\""),
            ("a,b", "\"a,b\""),
            ("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\""),
            ("+1", "\"'+1\""),
            ("-1", "\"'-1\""),
            ("@SUM(A1)", "\"'@SUM(A1)\""),
            ("\t=1", "\"' =1\""),
            ("\u{FF1D}1", "\"'\u{FF1D}1\""),
            ("a\r\nb", "\"a  b\""),
            ("", "\"\""),
        ];
        for (input, want) in cases {
            assert_eq!(csv_cell(input), want, "{input:?}");
        }
    }

    #[test]
    fn file_shape() {
        let s = to_csv(&["App", "Version"], &[vec!["Firefox".into(), "131.0".into()]]);
        assert!(s.starts_with('\u{FEFF}'));
        assert_eq!(
            s.trim_start_matches('\u{FEFF}'),
            "\"App\",\"Version\"\r\n\"Firefox\",\"131.0\"\r\n"
        );
    }
}
