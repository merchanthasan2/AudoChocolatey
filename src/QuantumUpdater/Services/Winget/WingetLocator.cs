using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace QuantumUpdater.Services.Winget;

/// <summary>
/// Finds the App Installer winget.exe. It asks Windows for the package and never reads PATH (F3).
/// DeskVerve runs as admin, so a fake winget.exe in a user folder must never be used.
/// </summary>
public static class WingetLocator
{
    private const string AppInstallerFamilyName = "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe";

    // W5: App Installer updates move winget.exe to a new versioned folder and delete the old one.
    // The cached path is checked on every read and looked up again when it is gone.
    private static readonly RecheckedPath Cached = new(FindCore, IsTrustedPath);

    /// <summary>
    /// Full path to winget.exe inside the App Installer package, or null when it is not found or not trusted.
    /// The path is cached, and found again when the cached file no longer exists.
    /// </summary>
    public static string? Find()
    {
        return Cached.Get();
    }

    /// <summary>The folder that holds the installed Windows packages. Only files here are trusted.</summary>
    internal static string WindowsAppsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");

    /// <summary>True only when the file exists and its full path is under the WindowsApps folder.</summary>
    internal static bool IsTrustedPath(string? path)
    {
        return File.Exists(path) && WingetPathRules.IsUnderRoot(path, WindowsAppsRoot);
    }

    private static string? FindCore()
    {
        try
        {
            var package = HighestTrustedPackage(QueryPackagesForCurrentUser())
                ?? HighestTrustedPackage(QueryPackagesAnyWay());
            return package is null ? null : WingetPathOf(package);
        }
        catch (Exception)
        {
            // Lookup failed. Treat winget as not found.
            return null;
        }
    }

    private static List<Package> QueryPackagesForCurrentUser()
    {
        try
        {
            return new PackageManager()
                .FindPackagesForUser(string.Empty, AppInstallerFamilyName)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static List<Package> QueryPackagesAnyWay()
    {
        try
        {
            // Works when DeskVerve runs elevated. Used only when the first query finds nothing.
            return new PackageManager()
                .FindPackages(AppInstallerFamilyName)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    // W6: the trust check runs before the version pick, so a newer untrusted package cannot hide a trusted one.
    private static Package? HighestTrustedPackage(List<Package> packages)
    {
        return WingetPathRules.HighestTrusted(
            packages,
            p => new Version(
                p.Id.Version.Major,
                p.Id.Version.Minor,
                p.Id.Version.Build,
                p.Id.Version.Revision),
            IsTrustedPackage);
    }

    // Trusted: signed by the Store or by Windows, and winget.exe is under the WindowsApps folder.
    private static bool IsTrustedPackage(Package package)
    {
        var signature = package.SignatureKind;
        if (signature != PackageSignatureKind.Store && signature != PackageSignatureKind.System)
        {
            return false;
        }

        return WingetPathOf(package) is not null;
    }

    // The winget.exe path of the package, or null when it is missing or not under WindowsApps.
    private static string? WingetPathOf(Package package)
    {
        var installedPath = package.InstalledLocation?.Path;
        if (string.IsNullOrEmpty(installedPath))
        {
            return null;
        }

        var candidate = Path.Combine(installedPath, "winget.exe");
        return IsTrustedPath(candidate) ? candidate : null;
    }
}
