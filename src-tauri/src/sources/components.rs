//! "Windows components": runtimes, codecs, language packs and other framework packages that
//! other apps need and nobody opens. Installed hides them by default (with a count and "Show").
//! The rules are anchored on purpose; a real app hidden here would be worse than a stray
//! runtime shown. Catalog apps are never components.

use crate::sources::model::InstalledPackage;
use regex::Regex;
use std::collections::HashSet;
use std::sync::LazyLock;

const MSIX_PREFIXES: &[&str] = &[
    "microsoft.vclibs.",
    "microsoft.net.native.",
    "microsoft.ui.xaml.",
    "microsoft.windowsappruntime.",
    "microsoft.winappruntime.",
    "microsoftcorporationii.winappruntime.",
    "microsoft.services.store.engagement",
    "microsoft.directxruntime",
    "microsoft.languageexperiencepack",
    "microsoftwindows.speech.",
    "microsoft.winget.source",
    "microsoft.widgetsplatformruntime",
    "microsoft.applicationcompatibilityenhancements",
    "microsoft.xboxidentityprovider",
    "microsoft.xbox.tcui",
    "microsoft.storepurchaseapp",
    "microsoft.edge.gameassist",
    "microsoft.office.actionsserver",
    "microsoft.officepushnotificationutility",
    "microsoft.office.onenotevirtualprinter",
    "microsoftwindows.client.webexperience",
    "microsoft.gamingservices",
    "microsoft.startexperiencesapp",
    "notepadplusplus",
];

const ID_PREFIXES: &[&str] = &[
    "microsoft.vcredist.",
    "microsoft.vclibs.",
    "microsoft.ui.xaml.",
    "microsoft.windowsappruntime.",
    "microsoft.dotnet.desktopruntime.",
    "microsoft.dotnet.runtime.",
    "microsoft.dotnet.aspnetcore.",
    "microsoft.dotnet.hostingbundle.",
    "microsoft.dotnet.native.",
    "vcredist",
    "netfx-",
];

const ID_EXACT: &[&str] = &[
    "microsoft.directx",
    "microsoft.edgewebview2runtime",
    "microsoft.gameinput",
    "nvidia.physx",
    "dotnetfx",
];

static MSIX_CODEC: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"(?i)^Microsoft\.[A-Za-z0-9]+(Video|Image|Media)Extensions?$").unwrap()
});
static MSIX_SHELL: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"(?i)(ShellExtension|ShellEx\d*)$").unwrap());
static CHOCO_ID: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"(?i)^((dotnet|netfx)[-\w.]*runtime|kb\d{6,7}|[\w.-]+\.extension)$").unwrap()
});
static NAMES: LazyLock<Vec<Regex>> = LazyLock::new(|| {
    [
        r"^Microsoft Visual C\+\+ (\d{4}|v\d+)\b.*\b(Redistributable|Runtime)\b",
        r"^Microsoft (Windows Desktop Runtime|\.NET (Runtime|Host|Host FX Resolver|AppHost Pack|Windows Desktop Runtime)|ASP\.NET Core \S+ (Shared Framework|Runtime))\b",
        r"^Microsoft \.Net Native (Framework|Runtime) Package\b",
        r"^(Windows App Runtime|WindowsAppRuntime|WinAppRuntime|Microsoft\.WindowsAppRuntime|Microsoft\.UI\.Xaml)\b",
        r"^Microsoft Edge WebView2 Runtime$",
        r"\bLocal Experience Pack$",
        r"^Speech Pack - ",
        r"^(AV1|AVC Encoder|HEIF|HEVC|MPEG-2|Raw|VP9|WebP|Web Media) (Video |Image |Media )?Extensions?\b",
        r"^vs_",
    ]
    .iter()
    .map(|p| Regex::new(&format!("(?i){p}")).unwrap())
    .collect()
});

pub fn is_component(name: &str, id: &str) -> bool {
    let lower = id.to_ascii_lowercase();
    if let Some(full) = lower.strip_prefix("msix\\") {
        let pkg = full.split('_').next().unwrap_or(full);
        let orig = &id[5..5 + pkg.len()];
        if MSIX_PREFIXES.iter().any(|p| pkg.starts_with(p))
            || MSIX_CODEC.is_match(orig)
            || MSIX_SHELL.is_match(orig)
        {
            return true;
        }
    } else if !lower.starts_with("arp\\")
        && (ID_PREFIXES.iter().any(|p| lower.starts_with(p))
            || ID_EXACT.contains(&lower.as_str())
            || CHOCO_ID.is_match(&lower))
    {
        return true;
    }
    NAMES.iter().any(|r| r.is_match(name.trim()))
}

/// Letters and digits only, lowercased: "64-bit" stays, so "Adobe Acrobat (64-bit)" and
/// "Adobe Acrobat" are different apps.
fn plain(name: &str) -> String {
    name.chars()
        .filter(|c| c.is_alphanumeric())
        .flat_map(char::to_lowercase)
        .collect()
}

/// A display-only row (no source) is dropped when a managed row has the same app name: winget
/// lists the Programs entry and the package separately for some apps.
pub fn fold_local_duplicates(items: Vec<InstalledPackage>) -> Vec<InstalledPackage> {
    let managed: HashSet<String> = items
        .iter()
        .filter(|p| p.source.is_some())
        .map(|p| plain(&p.name))
        .collect();
    items
        .into_iter()
        .filter(|p| p.source.is_some() || !managed.contains(&plain(&p.name)))
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::SourceKind;
    use crate::sources::label::classify;

    #[test]
    fn hidden() {
        let cases = [
            (
                "AV1 Video Extension",
                "MSIX\\Microsoft.AV1VideoExtension_1.1_x64__8wekyb3d8bbwe",
            ),
            (
                "Microsoft Visual C++ 2008 Redistributable - x86 9.0.30729",
                "ARP\\Machine\\X86\\{9BE518E6}",
            ),
            (
                "Microsoft Visual C++ v14 Redistributable (x64)",
                "Microsoft.VCRedist.2015+.x64",
            ),
            (
                "Microsoft Windows Desktop Runtime - 8.0.25 (x64)",
                "ARP\\Machine\\X64\\{abc}",
            ),
            (
                "WindowsAppRuntime.1.8",
                "MSIX\\Microsoft.WindowsAppRuntime.1.8_8000.616.304.0_x64__8wekyb3d8bbwe",
            ),
            (
                "Windows App Runtime DDLM 8000",
                "MSIX\\Microsoft.WinAppRuntime.DDLM.8000_x64__8wekyb3d8bbwe",
            ),
            (
                "Microsoft .Net Native Runtime Package 2.2",
                "MSIX\\Microsoft.NET.Native.Runtime.2.2_x64",
            ),
            (
                "English (United Kingdom) Local Experience Pack",
                "MSIX\\Microsoft.LanguageExperiencePacken-GB_x",
            ),
            (
                "Microsoft.UI.Xaml.2.8",
                "MSIX\\Microsoft.UI.Xaml.2.8_8.2310.30001.0_x64__8wekyb3d8bbwe",
            ),
            ("DirectX", "Microsoft.DirectX"),
            ("NVIDIA PhysX System Software", "Nvidia.PhysX"),
            ("WinRAR", "MSIX\\WinRAR.ShellExtension_7.1_x64__abc"),
            ("Notepad++", "MSIX\\NotepadPlusPlus_1.0_neutral__abc"),
            ("vcredist140", "vcredist140"),
            ("chocolatey-core.extension", "chocolatey-core.extension"),
            ("KB2919355", "KB2919355"),
            (
                "Microsoft Edge WebView2 Runtime",
                "Microsoft.EdgeWebView2Runtime",
            ),
        ];
        for (name, id) in cases {
            assert!(is_component(name, id), "{name} {id}");
        }
    }

    #[test]
    fn visible() {
        let cases = [
            ("Microsoft Teams", "Microsoft.Teams"),
            ("Windows Terminal", "Microsoft.WindowsTerminal"),
            ("Python 3.13.0 (64-bit)", "Python.Python.3.13"),
            ("Python Launcher", "ARP\\Machine\\X64\\{py}"),
            ("Node.js", "ARP\\Machine\\X64\\{node}"),
            ("Paint.NET", "dotPDN.PaintDotNet"),
            (
                "Microsoft .NET Framework 4.8.1 SDK",
                "ARP\\Machine\\X64\\{sdk}",
            ),
            ("Microsoft Visual Studio Code", "Microsoft.VisualStudioCode"),
            (
                "Visual Studio Build Tools 2022",
                "Microsoft.VisualStudio.2022.BuildTools",
            ),
            ("Windows Subsystem for Linux", "Microsoft.WSL"),
            (
                "Microsoft Store",
                "MSIX\\Microsoft.WindowsStore_22409.1401.5.0_x64__8wekyb3d8bbwe",
            ),
            (
                "Windows Calculator",
                "MSIX\\Microsoft.WindowsCalculator_11.2_x64__8wekyb3d8bbwe",
            ),
            (
                "Xbox",
                "MSIX\\Microsoft.GamingApp_2409.1001.21.0_x64__8wekyb3d8bbwe",
            ),
            ("WinRAR 7.10 (64-bit)", "RARLab.WinRAR"),
            ("Notepad++", "Notepad++.Notepad++"),
            ("Chocolatey", "chocolatey"),
            ("RawTherapee", "ARP\\Machine\\X64\\RawTherapee"),
            (
                "Visual C++ Runtime Installer (All-in-One)",
                "ARP\\Machine\\X64\\{vc}",
            ),
            (
                "Teams Meeting Add-in for Microsoft Office",
                "ARP\\Machine\\X64\\{tm}",
            ),
            ("NVIDIA Graphics Driver 566.14", "ARP\\Machine\\X64\\{nv}"),
            ("Counter-Strike 2", "ARP\\Machine\\X64\\Steam App 730"),
            ("Java(TM) SE Development Kit 25", "Oracle.JDK.25"),
        ];
        for (name, id) in cases {
            assert!(!is_component(name, id), "{name} {id}");
        }
    }

    #[test]
    fn owner_fixture_hidden_count() {
        let text = include_str!("../../tests/fixtures/winget/list.en.txt");
        let rows = crate::sources::winget::parse::parse_tables(text, true).concat();
        let hidden: Vec<&str> = rows
            .iter()
            .filter(|r| is_component(&r.name, &r.id))
            .map(|r| r.name.as_str())
            .collect();
        // Pinned: a change here means a rule now hides or shows something new.
        assert_eq!(hidden.len(), HIDDEN_IN_FIXTURE, "{hidden:#?}");
    }
    const HIDDEN_IN_FIXTURE: usize = 95;

    fn pkg(name: &str, id: &str, source: Option<SourceKind>) -> InstalledPackage {
        InstalledPackage {
            name: name.into(),
            id: id.into(),
            version: "1".into(),
            available: None,
            label: classify(source, id),
            component: false,
            linked: false,
            source,
        }
    }

    #[test]
    fn local_duplicates_fold_into_managed_rows() {
        let out = fold_local_duplicates(vec![
            pkg("Microsoft Edge", "Microsoft.Edge", Some(SourceKind::Winget)),
            pkg(
                "Microsoft Edge",
                "MSIX\\Microsoft.MicrosoftEdge.Stable_1",
                None,
            ),
            pkg("filezilla", "filezilla", Some(SourceKind::Choco)),
            pkg("FileZilla", "ARP\\Machine\\X86\\FileZilla Client", None),
            pkg("Adobe Acrobat", "MSIX\\Adobe.Acrobat_1", None),
            pkg(
                "Adobe Acrobat (64-bit)",
                "Adobe.Acrobat.Reader.64-bit",
                Some(SourceKind::Winget),
            ),
            pkg("Local One", "ARP\\Machine\\X64\\a", None),
            pkg("Local One", "ARP\\Machine\\X64\\b", None),
        ]);
        let ids: Vec<&str> = out.iter().map(|p| p.id.as_str()).collect();
        assert_eq!(
            ids,
            vec![
                "Microsoft.Edge",
                "filezilla",
                "MSIX\\Adobe.Acrobat_1",
                "Adobe.Acrobat.Reader.64-bit",
                "ARP\\Machine\\X64\\a",
                "ARP\\Machine\\X64\\b"
            ]
        );
    }
}
