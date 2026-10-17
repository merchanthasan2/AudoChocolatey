//! A Windows restore point before a run of changes. Asked for once each time the queue goes
//! from idle to busy (when the setting is on). It never stops the queue: if it fails or takes
//! too long, the changes go ahead and the activity panel says so.

use std::time::Duration;

/// How long the queue waits for a restore point before going ahead without one.
pub const TIMEOUT: Duration = Duration::from_secs(3 * 60);

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Outcome {
    /// Windows accepted the request. It may skip it if it made one in the last day.
    Requested,
    /// System Restore is turned off for this PC.
    Off,
    Failed,
    TimedOut,
}

impl Outcome {
    pub fn note(&self) -> &'static str {
        match self {
            Self::Requested => "Restore point requested before these changes.",
            Self::Off => "System Restore is off on this PC, so no restore point was made.",
            Self::Failed => "Couldn't make a restore point. The changes went ahead.",
            Self::TimedOut => "Making a restore point took too long. The changes went ahead.",
        }
    }
}

pub const MAKING: &str = "Making a restore point before changing apps…";

pub trait RestorePoint: Send + Sync {
    /// Blocking; called off the async threads.
    fn create(&self, description: &str) -> Outcome;
}

/// `SRSetRestorePointW`, begin then end (an "application install" point).
pub struct SystemRestore;

impl RestorePoint for SystemRestore {
    fn create(&self, description: &str) -> Outcome {
        use windows_sys::Win32::System::Restore::{
            SRSetRestorePointW, APPLICATION_INSTALL, BEGIN_SYSTEM_CHANGE, END_SYSTEM_CHANGE,
            RESTOREPOINTINFOW, STATEMGRSTATUS,
        };
        // What SRSetRestorePoint reports when System Restore is turned off.
        const ERROR_SERVICE_DISABLED: u32 = 1058;

        let mut desc = [0u16; 256];
        for (d, c) in desc.iter_mut().zip(description.encode_utf16().take(255)) {
            *d = c;
        }
        let mut info = RESTOREPOINTINFOW {
            dwEventType: BEGIN_SYSTEM_CHANGE,
            dwRestorePtType: APPLICATION_INSTALL,
            llSequenceNumber: 0,
            szDescription: desc,
        };
        // SAFETY: plain out-parameter struct.
        let mut status: STATEMGRSTATUS = unsafe { std::mem::zeroed() };
        // SAFETY: both pointers refer to valid, initialised structs that outlive the call.
        let ok = unsafe { SRSetRestorePointW(&info, &mut status) } != 0;
        if !ok {
            let code = status.nStatus;
            return if code == ERROR_SERVICE_DISABLED {
                Outcome::Off
            } else {
                Outcome::Failed
            };
        }
        info.dwEventType = END_SYSTEM_CHANGE;
        info.llSequenceNumber = status.llSequenceNumber;
        // SAFETY: as above.
        unsafe { SRSetRestorePointW(&info, &mut status) };
        Outcome::Requested
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn notes_never_promise_more_than_windows_does() {
        for o in [
            Outcome::Requested,
            Outcome::Off,
            Outcome::Failed,
            Outcome::TimedOut,
        ] {
            let n = o.note().to_lowercase();
            assert!(!n.contains("created") && !n.contains("safe"), "{n}");
        }
    }
}
