//! Windows notifications when new updates appear. Text comes from package names, so the toast
//! XML is built with the DOM API, never by string formatting.

const NAME_MAX: usize = 60;

/// Shown once, the first time closing hides the window to the tray.
pub const TRAY_HINT: (&str, &str) = (
    "Still running in the tray",
    "Quantum Updater keeps checking for updates. Right-click its tray icon to quit.",
);

fn short(name: &str) -> String {
    if name.chars().count() <= NAME_MAX {
        name.to_string()
    } else {
        format!("{}…", name.chars().take(NAME_MAX - 1).collect::<String>())
    }
}

/// Title and body for "new updates appeared".
pub fn update_lines(names: &[String]) -> (String, String) {
    let first = names.first().map(|n| short(n)).unwrap_or_default();
    match names.len() {
        0 | 1 => ("Update ready".into(), format!("{first} can be updated.")),
        2 => (
            "2 updates ready".into(),
            format!("{first} and {} can be updated.", short(&names[1])),
        ),
        n => (
            format!("{n} updates ready"),
            format!("{first} and {} more can be updated.", n - 1),
        ),
    }
}

/// Shows a notification as Quantum Updater. Clicking it starts the app through its Start menu
/// shortcut, which single-instance turns into "show the window". Debug builds have no
/// shortcut, so they skip it.
pub fn show(title: &str, body: &str) {
    if cfg!(debug_assertions) {
        eprintln!("notification (debug, not shown): {title} / {body}");
        return;
    }
    let (title, body) = (title.to_string(), body.to_string());
    // WinRT needs an initialised apartment; use a short-lived thread of our own.
    std::thread::spawn(move || {
        // SAFETY: balanced with CoUninitialize below on this thread.
        let _ = unsafe {
            windows::Win32::System::Com::CoInitializeEx(
                None,
                windows::Win32::System::Com::COINIT_MULTITHREADED,
            )
        };
        if let Err(e) = post(&title, &body) {
            eprintln!("notification failed: {e}");
        }
        // SAFETY: matches the CoInitializeEx above.
        unsafe { windows::Win32::System::Com::CoUninitialize() };
    });
}

fn post(title: &str, body: &str) -> windows::core::Result<()> {
    use windows::core::HSTRING;
    use windows::Data::Xml::Dom::XmlDocument;
    use windows::UI::Notifications::{ToastNotification, ToastNotificationManager};
    let xml = XmlDocument::new()?;
    xml.LoadXml(&HSTRING::from(
        "<toast launch=\"open:updates\"><visual><binding template=\"ToastGeneric\"><text></text><text></text></binding></visual></toast>",
    ))?;
    let texts = xml.GetElementsByTagName(&HSTRING::from("text"))?;
    texts
        .Item(0)?
        .AppendChild(&xml.CreateTextNode(&HSTRING::from(title))?)?;
    texts
        .Item(1)?
        .AppendChild(&xml.CreateTextNode(&HSTRING::from(body))?)?;
    let toast = ToastNotification::CreateToastNotification(&xml)?;
    ToastNotificationManager::CreateToastNotifierWithId(&HSTRING::from(crate::APP_ID))?.Show(&toast)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn names(v: &[&str]) -> Vec<String> {
        v.iter().map(|s| s.to_string()).collect()
    }

    #[test]
    fn lines_for_one_two_and_many() {
        assert_eq!(
            update_lines(&names(&["Firefox"])),
            ("Update ready".into(), "Firefox can be updated.".into())
        );
        assert_eq!(
            update_lines(&names(&["Firefox", "Zoom"])).1,
            "Firefox and Zoom can be updated."
        );
        assert_eq!(
            update_lines(&names(&["Firefox", "Zoom", "Git"])),
            (
                "3 updates ready".into(),
                "Firefox and 2 more can be updated.".into()
            )
        );
    }

    #[test]
    fn long_names_are_cut() {
        let long = "A".repeat(200);
        let (_, body) = update_lines(&[long]);
        assert!(body.chars().count() < 80, "{body}");
        assert!(body.contains('…'));
    }

    #[test]
    fn never_names_package_tools() {
        let tool = regex::Regex::new(r"(?i)\b(winget|chocolatey|choco|scoop|msstore)\b").unwrap();
        let (t, b) = update_lines(&names(&["Firefox", "Zoom", "Git"]));
        assert!(!tool.is_match(&t) && !tool.is_match(&b));
        assert!(!tool.is_match(TRAY_HINT.0) && !tool.is_match(TRAY_HINT.1));
    }
}
