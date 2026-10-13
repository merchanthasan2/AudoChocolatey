//! How the app was started. Only exact, known arguments count; anything else (including what a
//! second launch forwards through single-instance) is ignored.

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum LaunchMode {
    Normal,
    /// Started at sign-in by the Scheduled Task: may stay in the tray.
    Background,
    /// Run by the uninstaller: remove the sign-in task and exit.
    UnregisterAutostart,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Launch {
    pub mode: LaunchMode,
    /// Wait for this process (the copy that relaunched us as admin) to exit first.
    pub after: Option<u32>,
}

pub fn parse_args(args: &[String]) -> Launch {
    let has = |flag: &str| args.iter().any(|a| a == flag);
    let mode = if has("--unregister-autostart") {
        LaunchMode::UnregisterAutostart
    } else if has("--background") {
        LaunchMode::Background
    } else {
        LaunchMode::Normal
    };
    let after = args
        .iter()
        .position(|a| a == "--after")
        .and_then(|i| args.get(i + 1))
        .and_then(|v| v.parse::<u32>().ok())
        .filter(|&pid| pid != 0);
    Launch { mode, after }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn p(a: &[&str]) -> Launch {
        parse_args(&a.iter().map(|s| s.to_string()).collect::<Vec<_>>())
    }

    #[test]
    fn known_arguments_only() {
        assert_eq!(
            p(&[]),
            Launch {
                mode: LaunchMode::Normal,
                after: None
            }
        );
        assert_eq!(p(&["--background"]).mode, LaunchMode::Background);
        assert_eq!(
            p(&["--background", "--background"]).mode,
            LaunchMode::Background
        );
        assert_eq!(
            p(&["--unregister-autostart"]).mode,
            LaunchMode::UnregisterAutostart
        );
        assert_eq!(
            p(&["--background", "--unregister-autostart"]).mode,
            LaunchMode::UnregisterAutostart,
            "uninstall cleanup wins"
        );
        assert_eq!(
            p(&["--BACKGROUND", "x", "--background=1"]).mode,
            LaunchMode::Normal
        );
    }

    #[test]
    fn after_takes_a_process_id() {
        assert_eq!(p(&["--after", "4321"]).after, Some(4321));
        assert_eq!(p(&["--after", "abc"]).after, None);
        assert_eq!(p(&["--after"]).after, None);
        assert_eq!(p(&["--after", "-5"]).after, None);
        assert_eq!(p(&["--after", "0"]).after, None);
    }
}
