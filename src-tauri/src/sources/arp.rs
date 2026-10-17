//! Reading one Programs-list ("ARP") entry, for linking apps installed directly. Machine-wide
//! entries only: per-user ones can be written without admin rights, so they never drive a link.

use super::label::ArpId;
use crate::catalog::local_match::LocalEntry;

/// Blocking registry read of `HKLM\…\Uninstall\<key>` in the entry's own 32/64-bit view.
pub fn read_entry(id: &ArpId) -> Option<LocalEntry> {
    use winreg::enums::{HKEY_LOCAL_MACHINE, KEY_READ, KEY_WOW64_32KEY, KEY_WOW64_64KEY};
    use winreg::RegKey;
    if !id.machine || id.key.contains('\\') || id.key.len() > 200 {
        return None;
    }
    let view = if id.x64 {
        KEY_WOW64_64KEY
    } else {
        KEY_WOW64_32KEY
    };
    let k = RegKey::predef(HKEY_LOCAL_MACHINE)
        .open_subkey_with_flags(
            format!(
                r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{}",
                id.key
            ),
            KEY_READ | view,
        )
        .ok()?;
    let get = |v: &str| {
        k.get_value::<String, _>(v)
            .ok()
            .map(|s| s.trim().to_string())
            .filter(|s| !s.is_empty())
    };
    Some(LocalEntry {
        key: id.key.to_string(),
        x64: id.x64,
        display_name: get("DisplayName")?,
        publisher: get("Publisher"),
        version: get("DisplayVersion"),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sources::label::parse_arp_id;

    /// Read-only look at this PC's FileZilla / WinSCP entries (run by hand).
    #[test]
    #[ignore]
    fn print_local_entries() {
        for id in [
            r"ARP\Machine\X64\FileZilla Client",
            r"ARP\Machine\X86\FileZilla Client",
            r"ARP\Machine\X86\winscp3_is1",
            r"ARP\Machine\X64\winscp3_is1",
        ] {
            let e = read_entry(&parse_arp_id(id).unwrap());
            let links = e.as_ref().map(|e| {
                crate::catalog::local_match::link_all(
                    &[(id.to_string(), e.clone())],
                    &Default::default(),
                    crate::catalog::Catalog::embedded(),
                    |_| true,
                )
            });
            println!("{id}: {e:?} -> {links:?}");
        }
    }

    #[test]
    fn per_user_and_odd_keys_are_never_read() {
        assert!(read_entry(&parse_arp_id(r"ARP\User\X64\FileZilla Client").unwrap()).is_none());
        assert!(read_entry(&parse_arp_id(r"ARP\Machine\X64\..\..\Run").unwrap()).is_none());
    }
}
