use std::cmp::Ordering;

/// Loose version ordering for package versions: numeric segments compare numerically,
/// text segments case-insensitively, missing segments count as zero, and a pre-release
/// text segment sorts below "nothing" (2.0.0-beta < 2.0.0).
pub fn compare(a: &str, b: &str) -> Ordering {
    let split = |s: &str| -> Vec<String> {
        s.split(['.', '-', '+', '_'])
            .filter(|p| !p.is_empty())
            .map(|p| p.to_ascii_lowercase())
            .collect()
    };
    let (pa, pb) = (split(a), split(b));
    for i in 0..pa.len().max(pb.len()) {
        let ord = match (pa.get(i), pb.get(i)) {
            (Some(x), Some(y)) => segment(x, y),
            (Some(x), None) => against_missing(x),
            (None, Some(y)) => against_missing(y).reverse(),
            (None, None) => Ordering::Equal,
        };
        if ord != Ordering::Equal {
            return ord;
        }
    }
    Ordering::Equal
}

fn is_num(s: &str) -> bool {
    !s.is_empty() && s.bytes().all(|b| b.is_ascii_digit())
}

fn segment(x: &str, y: &str) -> Ordering {
    match (is_num(x), is_num(y)) {
        (true, true) => {
            let (x, y) = (x.trim_start_matches('0'), y.trim_start_matches('0'));
            x.len().cmp(&y.len()).then_with(|| x.cmp(y))
        }
        (true, false) => Ordering::Greater,
        (false, true) => Ordering::Less,
        (false, false) => x.cmp(y),
    }
}

/// A present segment versus an absent one: zeros are equal to absent, other numbers are
/// greater, text (pre-release tags) is less.
fn against_missing(x: &str) -> Ordering {
    if is_num(x) {
        if x.trim_start_matches('0').is_empty() {
            Ordering::Equal
        } else {
            Ordering::Greater
        }
    } else {
        Ordering::Less
    }
}

#[cfg(test)]
mod tests {
    use super::compare;
    use std::cmp::Ordering::*;

    #[test]
    fn compares_like_people_expect() {
        assert_eq!(compare("1.95.2", "1.95.10"), Less);
        assert_eq!(compare("9.9.2", "9.9.3"), Less);
        assert_eq!(compare("1.0", "1.0.0"), Equal);
        assert_eq!(compare("2.0.0-beta", "2.0.0"), Less);
        assert_eq!(compare("117.14.19", "117.14.41"), Less);
        assert_eq!(compare("26.002.21931", "26.1.0"), Greater);
        assert_eq!(compare("0.0.309", "0.0.311"), Less);
        assert_eq!(compare("14.51.36247", "14.44.35211"), Greater);
    }
}
