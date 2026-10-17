//! Going back to the version an update replaced. History lives in a user-writable file, so
//! nothing from it is trusted: the offer is re-checked against the PC and the source, the
//! version comes from the source's own list, and a Windows dialog asks before anything runs.

use crate::catalog::Catalog;
use crate::ids::{SourceKind, SourceRef};
use crate::jobs::model::{Job, JobPhase};
use crate::jobs::verify::Verify;
use crate::rules::rule_key;
use crate::sources::model::OpKind;
use crate::versions::compare;
use std::cmp::Ordering;

fn usable(v: Option<&str>) -> Option<&str> {
    v.map(str::trim).filter(|v| {
        !v.is_empty()
            && !v.eq_ignore_ascii_case("unknown")
            && v.starts_with(|c: char| c.is_ascii_digit())
    })
}

/// Whether History offers "Go back" for this job: a confirmed update through its own source
/// (winget or Chocolatey), from a known older version, and the newest change to that app.
/// Returns the package, the version to go back to, and the version it would leave.
pub fn rollback_offer<'a>(
    job: &'a Job,
    history: &[Job],
    cat: &Catalog,
) -> Result<(SourceRef, &'a str, &'a str), &'static str> {
    if job.phase != JobPhase::Succeeded || job.op.kind != OpKind::Upgrade {
        return Err("Only finished updates can be undone.");
    }
    if job.via.is_some() {
        return Err("This update came from a backup source, so it can't be undone here.");
    }
    if job.verify != Some(Verify::Confirmed) {
        return Err("We couldn't confirm what this update changed.");
    }
    if !matches!(
        job.op.target.source(),
        SourceKind::Winget | SourceKind::Choco
    ) {
        return Err("Microsoft Store apps can't go back to an earlier version.");
    }
    let (Some(from), Some(to)) = (
        usable(job.from_version.as_deref()),
        usable(job.to_version.as_deref()),
    ) else {
        return Err("We don't know which version this update replaced.");
    };
    if compare(from, to) != Ordering::Less {
        return Err("We don't know which version this update replaced.");
    }
    let key = rule_key(&job.op.target, cat);
    let newest = history
        .iter()
        .filter(|j| j.phase == JobPhase::Succeeded)
        .find(|j| rule_key(j.via.as_ref().unwrap_or(&j.op.target), cat) == key);
    if newest.is_none_or(|n| n.id != job.id) {
        return Err("This app changed again after this update.");
    }
    Ok((job.op.target.clone(), from, to))
}

/// The listed version matching `wanted`: exact, else the only one that compares equal.
pub fn pick_listed(listed: &[String], wanted: &str) -> Option<String> {
    if let Some(v) = listed.iter().find(|v| v.as_str() == wanted) {
        return Some(v.clone());
    }
    let mut same = listed
        .iter()
        .filter(|v| compare(v, wanted) == Ordering::Equal);
    match (same.next(), same.next()) {
        (Some(v), None) => Some(v.clone()),
        _ => None,
    }
}

/// `winget show --versions`: the lines after the dashed line.
pub fn parse_versions(stdout: &str) -> Vec<String> {
    stdout
        .lines()
        .skip_while(|l| !l.trim_start().starts_with("---"))
        .skip(1)
        .map(str::trim)
        .filter(|l| !l.is_empty())
        .map(String::from)
        .collect()
}

/// The name to show: the catalog's, else the package id (never the stored label).
pub fn display_name(target: &SourceRef, cat: &Catalog) -> String {
    cat.by_source(target.source(), target.id())
        .map(|a| a.name.clone())
        .unwrap_or_else(|| target.id().to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sources::model::OpRequest;

    fn upd(id: &str, n: &str, source: SourceKind, from: &str, to: &str) -> Job {
        let mut j = Job::new(
            n.into(),
            OpRequest {
                kind: OpKind::Upgrade,
                target: SourceRef::new(source, id).unwrap(),
                version: None,
            },
            id.into(),
        );
        j.set_phase(JobPhase::Preparing);
        j.set_phase(JobPhase::Succeeded);
        j.verify = Some(Verify::Confirmed);
        j.from_version = Some(from.into());
        j.to_version = Some(to.into());
        j
    }

    #[test]
    fn offered_for_a_confirmed_newest_update() {
        let cat = Catalog::embedded();
        let j = upd("Some.App", "j1", SourceKind::Winget, "1.0", "1.1");
        let (t, from, to) = rollback_offer(&j, std::slice::from_ref(&j), cat).unwrap();
        assert_eq!((t.id(), from, to), ("Some.App", "1.0", "1.1"));
        let c = upd("some-app", "j1", SourceKind::Choco, "1.0", "1.1");
        assert!(rollback_offer(&c, std::slice::from_ref(&c), cat).is_ok());
    }

    #[test]
    fn not_offered_otherwise() {
        let cat = Catalog::embedded();
        let base = upd("Some.App", "j1", SourceKind::Winget, "1.0", "1.1");
        let mut cases: Vec<Job> = Vec::new();
        let mut j = base.clone();
        j.phase = JobPhase::Failed;
        cases.push(j);
        for k in [OpKind::Install, OpKind::Uninstall, OpKind::Rollback] {
            let mut j = base.clone();
            j.op.kind = k;
            cases.push(j);
        }
        for from in [
            None,
            Some("Unknown"),
            Some("< 2.0"),
            Some("1.1"),
            Some("2.0"),
        ] {
            let mut j = base.clone();
            j.from_version = from.map(String::from);
            cases.push(j);
        }
        for v in [None, Some(Verify::Unchanged), Some(Verify::Unknown)] {
            let mut j = base.clone();
            j.verify = v;
            cases.push(j);
        }
        let mut j = base.clone();
        j.via = Some(SourceRef::new(SourceKind::Choco, "some-app").unwrap());
        cases.push(j);
        cases.push(upd("9NBLGGH4NNS1", "j1", SourceKind::Msstore, "1.0", "1.1"));
        for j in &cases {
            assert!(
                rollback_offer(j, std::slice::from_ref(j), cat).is_err(),
                "{j:?}"
            );
        }
        // A newer change to the same app, even through another source.
        let ff = upd("Mozilla.Firefox", "j1", SourceKind::Winget, "1.0", "1.1");
        let mut later = upd("firefox", "j2", SourceKind::Choco, "1.1", "1.2");
        later.via = None;
        assert!(rollback_offer(&ff, &[later, ff.clone()], cat).is_err());
        // An older change to it doesn't matter.
        let older = upd("Mozilla.Firefox", "j0", SourceKind::Winget, "0.9", "1.0");
        assert!(rollback_offer(&ff, &[ff.clone(), older], cat).is_ok());
    }

    #[test]
    fn versions_come_from_the_source_list() {
        let l: Vec<String> = ["1.1", "1.0", "0.9.0"]
            .iter()
            .map(|s| s.to_string())
            .collect();
        assert_eq!(pick_listed(&l, "1.0").as_deref(), Some("1.0"));
        assert_eq!(pick_listed(&l, "1.0.0").as_deref(), Some("1.0"));
        assert_eq!(pick_listed(&l, "0.9").as_deref(), Some("0.9.0"));
        assert_eq!(pick_listed(&l, "2.0"), None);
        assert_eq!(pick_listed(&l, "1.0 --force"), None);
        let twins: Vec<String> = ["1.0", "1.0.0"].iter().map(|s| s.to_string()).collect();
        assert_eq!(pick_listed(&twins, "1.0.0.0"), None);
    }

    #[test]
    fn winget_version_list() {
        let out = "Found Mozilla Firefox [Mozilla.Firefox]\nVersion\n-------\n131.0\n130.0.1\n\n";
        assert_eq!(parse_versions(out), vec!["131.0", "130.0.1"]);
        assert!(parse_versions("No package found").is_empty());
    }
}
