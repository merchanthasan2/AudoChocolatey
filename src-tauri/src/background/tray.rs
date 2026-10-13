//! The tray icon's menu and tooltip.

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum TrayAction {
    Open,
    CheckNow,
    PauseResume,
    UpdateAll,
    Quit,
}

impl TrayAction {
    pub const ALL: [TrayAction; 5] = [
        Self::Open,
        Self::CheckNow,
        Self::PauseResume,
        Self::UpdateAll,
        Self::Quit,
    ];

    pub fn id(self) -> &'static str {
        match self {
            Self::Open => "open",
            Self::CheckNow => "check",
            Self::PauseResume => "pause",
            Self::UpdateAll => "update-all",
            Self::Quit => "quit",
        }
    }

    pub fn from_id(id: &str) -> Option<Self> {
        Self::ALL.into_iter().find(|a| a.id() == id)
    }

    pub fn label(self, paused: bool, updates: usize) -> String {
        match self {
            Self::Open => "Open Quantum Updater".into(),
            Self::CheckNow => "Check for updates now".into(),
            Self::PauseResume if paused => "Resume".into(),
            Self::PauseResume => "Pause".into(),
            Self::UpdateAll if updates > 0 => format!("Update all ({updates})"),
            Self::UpdateAll => "Update all".into(),
            Self::Quit => "Quit".into(),
        }
    }
}

/// `None` before the first check.
pub fn tooltip(updates: Option<usize>) -> String {
    match updates {
        None => "Quantum Updater".into(),
        Some(0) => "Quantum Updater: everything is up to date".into(),
        Some(1) => "Quantum Updater: 1 update ready".into(),
        Some(n) => format!("Quantum Updater: {n} updates ready"),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn menu_ids_round_trip_and_unknown_ids_are_ignored() {
        for a in TrayAction::ALL {
            assert_eq!(TrayAction::from_id(a.id()), Some(a));
        }
        assert_eq!(TrayAction::from_id("open; rm -rf"), None);
        assert_eq!(TrayAction::from_id(""), None);
    }

    #[test]
    fn tooltip_counts_updates() {
        assert_eq!(tooltip(None), "Quantum Updater");
        assert_eq!(
            tooltip(Some(0)),
            "Quantum Updater: everything is up to date"
        );
        assert_eq!(tooltip(Some(1)), "Quantum Updater: 1 update ready");
        assert_eq!(tooltip(Some(3)), "Quantum Updater: 3 updates ready");
    }

    #[test]
    fn labels_never_name_package_tools() {
        let tool = regex::Regex::new(r"(?i)\b(winget|chocolatey|choco|scoop|msstore)\b").unwrap();
        for a in TrayAction::ALL {
            for paused in [false, true] {
                assert!(!tool.is_match(&a.label(paused, 2)), "{a:?}");
            }
        }
        assert!(!tool.is_match(&tooltip(Some(2))));
    }
}
