//! winget argv construction. Pure functions so every flag is unit-tested.
//! Package operations always target one exact id in the winget source; `--force` and `--all`
//! are never emitted.

use crate::error::{AppError, AppResult};
use crate::ids::{SourceKind, WingetId};
use crate::sources::model::{OpKind, OpRequest};

const AGREE_SOURCE: &str = "--accept-source-agreements";
const NO_PROMPT: &str = "--disable-interactivity";

pub fn build_op(op: &OpRequest) -> AppResult<Vec<String>> {
    let source = match op.target.source() {
        SourceKind::Winget => "winget",
        SourceKind::Msstore => "msstore",
        SourceKind::Choco => return Err(AppError::new("invalid_input", "not a winget package")),
    };
    let id = op.target.id();
    let mut argv: Vec<&str> = match op.kind {
        OpKind::Install | OpKind::Upgrade => vec![
            if op.kind == OpKind::Install {
                "install"
            } else {
                "upgrade"
            },
            "--id",
            id,
            "--exact",
            "--source",
            source,
            "--silent",
            NO_PROMPT,
            "--accept-package-agreements",
            AGREE_SOURCE,
        ],
        OpKind::Uninstall => vec![
            "uninstall",
            "--id",
            id,
            "--exact",
            "--silent",
            NO_PROMPT,
            AGREE_SOURCE,
        ],
    };
    // The Store always installs its current version.
    let version = op
        .version
        .as_ref()
        .filter(|_| op.kind != OpKind::Uninstall && op.target.source() != SourceKind::Msstore);
    if let Some(v) = version {
        argv.extend(["--version", v.as_str()]);
    }
    Ok(argv.into_iter().map(String::from).collect())
}

pub fn version() -> Vec<String> {
    vec!["--version".into()]
}

pub fn list() -> Vec<String> {
    ["list", AGREE_SOURCE, NO_PROMPT].map(String::from).to_vec()
}

/// One app's installed row (for checking a job's result).
pub fn list_id(id: &WingetId) -> Vec<String> {
    [
        "list",
        "--id",
        id.as_str(),
        "--exact",
        AGREE_SOURCE,
        NO_PROMPT,
    ]
    .map(String::from)
    .to_vec()
}

pub fn upgrade(include_unknown: bool) -> Vec<String> {
    let mut a = vec!["upgrade"];
    if include_unknown {
        a.push("--include-unknown");
    }
    a.extend([AGREE_SOURCE, NO_PROMPT]);
    a.into_iter().map(String::from).collect()
}

pub fn search(query: &str) -> Vec<String> {
    [
        "search",
        "--query",
        query,
        "--source",
        "winget",
        "--count",
        "50",
        AGREE_SOURCE,
        NO_PROMPT,
    ]
    .map(String::from)
    .to_vec()
}

/// Store search: a short list, since Store matches get loose quickly.
pub fn search_store(query: &str) -> Vec<String> {
    [
        "search",
        "--query",
        query,
        "--source",
        "msstore",
        "--count",
        "20",
        AGREE_SOURCE,
        NO_PROMPT,
    ]
    .map(String::from)
    .to_vec()
}

pub fn export(path: &str) -> Vec<String> {
    [
        "export",
        "-o",
        path,
        "--include-versions",
        AGREE_SOURCE,
        NO_PROMPT,
    ]
    .map(String::from)
    .to_vec()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::{PkgVersion, SourceKind, SourceRef};
    use crate::sources::model::{OpKind, OpRequest};

    #[test]
    fn store_apps_install_from_the_store_source() {
        let o = OpRequest {
            kind: OpKind::Install,
            target: SourceRef::new(SourceKind::Msstore, "9NKSQGP7F2NH").unwrap(),
            version: Some(PkgVersion::try_from("1.0".to_string()).unwrap()),
        };
        let a = build_op(&o).unwrap();
        assert_eq!(&a[..4], ["install", "--id", "9NKSQGP7F2NH", "--exact"]);
        assert!(a.windows(2).any(|w| w == ["--source", "msstore"]), "{a:?}");
        assert!(a.contains(&"--accept-package-agreements".to_string()));
        assert!(
            !a.contains(&"--version".to_string()),
            "the Store has no versions"
        );
        let s = search_store("whats app");
        assert!(s.windows(2).any(|w| w == ["--source", "msstore"]), "{s:?}");
        assert!(s.windows(2).any(|w| w == ["--query", "whats app"]), "{s:?}");
    }

    fn op(kind: OpKind, id: &str, version: Option<&str>) -> OpRequest {
        OpRequest {
            kind,
            target: SourceRef::new(SourceKind::Winget, id).unwrap(),
            version: version.map(|v| PkgVersion::try_from(v.to_string()).unwrap()),
        }
    }

    #[test]
    fn install_with_version_exact_argv() {
        assert_eq!(
            build_op(&op(OpKind::Install, "Notepad++.Notepad++", Some("8.7"))).unwrap(),
            [
                "install",
                "--id",
                "Notepad++.Notepad++",
                "--exact",
                "--source",
                "winget",
                "--silent",
                "--disable-interactivity",
                "--accept-package-agreements",
                "--accept-source-agreements",
                "--version",
                "8.7"
            ]
        );
    }

    #[test]
    fn uninstall_argv() {
        assert_eq!(
            build_op(&op(OpKind::Uninstall, "Mozilla.Firefox", None)).unwrap(),
            [
                "uninstall",
                "--id",
                "Mozilla.Firefox",
                "--exact",
                "--silent",
                "--disable-interactivity",
                "--accept-source-agreements"
            ]
        );
    }

    #[test]
    fn never_force_or_all() {
        for kind in [OpKind::Install, OpKind::Upgrade, OpKind::Uninstall] {
            let argv = build_op(&op(kind, "Mozilla.Firefox", Some("1.0"))).unwrap();
            assert!(
                !argv.iter().any(|a| a == "--force" || a == "--all"),
                "{argv:?}"
            );
            assert!(argv.iter().any(|a| a == "Mozilla.Firefox"));
        }
    }

    #[test]
    fn rejects_choco_target() {
        let mut o = op(OpKind::Install, "Mozilla.Firefox", None);
        o.target = SourceRef::new(SourceKind::Choco, "firefox").unwrap();
        assert_eq!(build_op(&o).unwrap_err().code, "invalid_input");
    }
}
