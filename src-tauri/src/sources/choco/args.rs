//! Chocolatey argv construction. Progress output is kept (no `--no-progress`) so the job queue
//! can show download percentages; `--force` is never emitted.

use crate::error::{AppError, AppResult};
use crate::ids::{ChocoId, SourceKind};
use crate::sources::model::{OpKind, OpRequest};

pub fn build_op(op: &OpRequest) -> AppResult<Vec<String>> {
    if op.target.source() != SourceKind::Choco {
        return Err(AppError::new("invalid_input", "not a Chocolatey package"));
    }
    if op.kind == OpKind::Rollback {
        // Chocolatey's documented downgrade: upgrade to an exact older version.
        let Some(v) = &op.version else {
            return Err(AppError::new("invalid_input", "rollback needs a version"));
        };
        return Ok(vec![
            "upgrade".into(),
            op.target.id().to_string(),
            "-y".into(),
            "--no-color".into(),
            "--version".into(),
            v.to_string(),
            "--allow-downgrade".into(),
        ]);
    }
    let verb = match op.kind {
        OpKind::Install => "install",
        OpKind::Upgrade => "upgrade",
        OpKind::Uninstall => "uninstall",
        OpKind::Rollback => unreachable!("handled above"),
    };
    let mut argv = vec![
        verb.to_string(),
        op.target.id().to_string(),
        "-y".into(),
        "--no-color".into(),
    ];
    if let (Some(v), false) = (&op.version, op.kind == OpKind::Uninstall) {
        argv.extend(["--version".to_string(), v.to_string()]);
    }
    Ok(argv)
}

pub fn version() -> Vec<String> {
    vec!["--version".into()]
}

/// `--local-only` was removed in Chocolatey v2 (list is local by default there).
pub fn list(major: u32) -> Vec<String> {
    if major >= 2 {
        vec!["list".into(), "-r".into()]
    } else {
        vec!["list".into(), "--local-only".into(), "-r".into()]
    }
}

/// One package's installed row.
pub fn list_id(major: u32, id: &ChocoId) -> Vec<String> {
    let mut a = list(major);
    a.insert(1, id.as_str().to_string());
    a.insert(2, "--exact".into());
    a
}

pub fn outdated() -> Vec<String> {
    vec!["outdated".into(), "-r".into()]
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::{PkgVersion, SourceKind, SourceRef};
    use crate::sources::model::{OpKind, OpRequest};

    fn op(kind: OpKind, v: Option<&str>) -> OpRequest {
        OpRequest {
            kind,
            target: SourceRef::new(SourceKind::Choco, "chocolatey-core.extension").unwrap(),
            version: v.map(|v| PkgVersion::try_from(v.to_string()).unwrap()),
        }
    }

    #[test]
    fn argv_shapes() {
        assert_eq!(
            build_op(&op(OpKind::Install, Some("1.4.0"))).unwrap(),
            [
                "install",
                "chocolatey-core.extension",
                "-y",
                "--no-color",
                "--version",
                "1.4.0"
            ]
        );
        assert_eq!(
            build_op(&op(OpKind::Upgrade, None)).unwrap(),
            ["upgrade", "chocolatey-core.extension", "-y", "--no-color"]
        );
        assert_eq!(
            build_op(&op(OpKind::Uninstall, Some("9"))).unwrap(),
            ["uninstall", "chocolatey-core.extension", "-y", "--no-color"]
        );
    }

    #[test]
    fn going_back_is_an_exact_downgrade() {
        assert_eq!(
            build_op(&op(OpKind::Rollback, Some("1.4.0"))).unwrap(),
            [
                "upgrade",
                "chocolatey-core.extension",
                "-y",
                "--no-color",
                "--version",
                "1.4.0",
                "--allow-downgrade"
            ]
        );
        assert!(build_op(&op(OpKind::Rollback, None)).is_err());
        for k in [OpKind::Install, OpKind::Upgrade] {
            let a = build_op(&op(k, Some("1.0"))).unwrap();
            assert!(!a.iter().any(|x| x == "--allow-downgrade"), "{a:?}");
        }
    }

    #[test]
    fn never_force_and_rejects_winget_target() {
        for k in [OpKind::Install, OpKind::Upgrade, OpKind::Uninstall] {
            assert!(!build_op(&op(k, None))
                .unwrap()
                .iter()
                .any(|a| a.starts_with("--force")));
        }
        let mut o = op(OpKind::Install, None);
        o.target = SourceRef::new(SourceKind::Winget, "Mozilla.Firefox").unwrap();
        assert_eq!(build_op(&o).unwrap_err().code, "invalid_input");
    }

    #[test]
    fn list_args_depend_on_major() {
        assert_eq!(list(2), ["list", "-r"]);
        assert_eq!(list(1), ["list", "--local-only", "-r"]);
    }
}
