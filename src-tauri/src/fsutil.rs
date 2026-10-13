//! Small file helpers shared by settings, the job history and rules.

use std::io::Write;
use std::path::Path;

/// Write via a temp file and rename, so a crash never leaves a half-written file.
pub fn write_atomic(path: &Path, bytes: &[u8]) -> std::io::Result<()> {
    if let Some(dir) = path.parent() {
        std::fs::create_dir_all(dir)?;
    }
    let mut tmp = path.as_os_str().to_owned();
    tmp.push(".tmp");
    let tmp = std::path::PathBuf::from(tmp);
    {
        let mut f = std::fs::File::create(&tmp)?;
        f.write_all(bytes)?;
        f.sync_all()?;
    }
    std::fs::rename(&tmp, path)
}

/// What reading a small JSON state file produced.
#[derive(Debug, PartialEq)]
pub enum Read {
    Missing,
    /// Couldn't be read (locked, permissions): leave it alone.
    Unreadable,
    /// Larger than the cap or not JSON: the caller backs it up and starts fresh.
    Corrupt,
    Json(serde_json::Value),
}

pub fn read_json(path: &Path, max_bytes: u64) -> Read {
    match std::fs::metadata(path) {
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Read::Missing,
        Err(_) => return Read::Unreadable,
        Ok(m) if m.len() > max_bytes => return Read::Corrupt,
        Ok(_) => {}
    }
    let Ok(text) = std::fs::read_to_string(path) else {
        return Read::Unreadable;
    };
    match serde_json::from_str(&text) {
        Ok(v) => Read::Json(v),
        Err(_) => Read::Corrupt,
    }
}

/// Moves an unusable file aside as `<name>.bad`; true if it's out of the way.
pub fn set_aside(path: &Path) -> bool {
    let mut bad = path.as_os_str().to_owned();
    bad.push(".bad");
    std::fs::rename(path, std::path::PathBuf::from(bad)).is_ok()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn atomic_write_then_read() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("sub").join("x.json");
        write_atomic(&p, br#"{"a":1}"#).unwrap();
        assert_eq!(read_json(&p, 100), Read::Json(serde_json::json!({"a":1})));
        assert!(!dir.path().join("sub").join("x.json.tmp").exists());
    }

    #[test]
    fn missing_oversized_and_garbage() {
        let dir = tempfile::tempdir().unwrap();
        let p = dir.path().join("x.json");
        assert_eq!(read_json(&p, 100), Read::Missing);
        std::fs::write(&p, "not json").unwrap();
        assert_eq!(read_json(&p, 100), Read::Corrupt);
        std::fs::write(&p, vec![b' '; 200]).unwrap();
        assert_eq!(read_json(&p, 100), Read::Corrupt);
        assert!(set_aside(&p));
        assert!(dir.path().join("x.json.bad").exists());
        assert!(!p.exists());
    }
}
