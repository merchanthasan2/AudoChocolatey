//! What closing or starting the window does. Pure decisions; `lib.rs` applies them.

use crate::launch::LaunchMode;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CloseAction {
    /// Hide to the tray; jobs keep running.
    Hide,
    /// Ask first: closing would stop a running job.
    Guard,
    Close,
}

pub fn close_action(close_to_tray: bool, job_running: bool) -> CloseAction {
    if close_to_tray {
        CloseAction::Hide
    } else if job_running {
        CloseAction::Guard
    } else {
        CloseAction::Close
    }
}

/// A sign-in start stays in the tray when the person chose that; every other start shows.
pub fn show_at_start(mode: LaunchMode, start_in_tray: bool) -> bool {
    !(mode == LaunchMode::Background && start_in_tray)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::launch::LaunchMode;

    #[test]
    fn closing() {
        assert_eq!(
            close_action(true, false),
            CloseAction::Hide,
            "jobs keep running in the tray"
        );
        assert_eq!(close_action(true, true), CloseAction::Hide);
        assert_eq!(close_action(false, true), CloseAction::Guard);
        assert_eq!(close_action(false, false), CloseAction::Close);
    }

    #[test]
    fn showing_at_start() {
        assert!(show_at_start(LaunchMode::Normal, true));
        assert!(show_at_start(LaunchMode::Normal, false));
        assert!(!show_at_start(LaunchMode::Background, true));
        assert!(show_at_start(LaunchMode::Background, false));
    }
}
