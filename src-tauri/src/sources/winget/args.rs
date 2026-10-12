//! winget argv construction. Pure functions so every flag is unit-tested.
//! Package operations always target one exact id in the winget source; `--force` and `--all`
//! are never emitted.

use crate::error::{AppError, AppResult};
use crate::ids::SourceKind;
use crate::sources::model::{OpKind, OpRequest};

const AGREE_SOURCE: &str = "--accept-source-agreements";
const NO_PROMPT: &str = "--disable-interactivity";

pub fn build_op(op: &OpRequest) -> AppResult<Vec<String>> {
    if op.target.source != SourceKind::Winget {
        return Err(AppError::new("invalid_input", "not a winget package"));
    }
    let id = op.target.id.as_str();
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
            "winget",
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
    let version = op.version.as_ref().filter(|_| op.kind != OpKind::Uninstall);
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
