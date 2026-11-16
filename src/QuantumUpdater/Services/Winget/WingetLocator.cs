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

    private static readonly Lazy<string?> Cached = new(FindCore, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Full path to winget.exe inside the App Installer package, or null when it is not found or not trusted.
    /// The result is cached for the life of the process.
    /// </summary>
    public static string? Find()
    {
        return Cached.Value;
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
            var package = HighestPackage(QueryPackagesForCurrentUser())
                ?? HighestPackage(QueryPackagesAnyWay());
            var installedPath = package?.InstalledLocation?.Path;
            if (string.IsNullOrEmpty(installedPath))
            {
                return null;
            }

            var candidate = Path.Combine(installedPath, "winget.exe");
            return IsTrustedPath(candidate) ? candidate : null;
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

    private static Package? HighestPackage(List<Package> packages)
    {
        return packages
            .OrderByDescending(p => new Version(
                p.Id.Version.Major,
                p.Id.Version.Minor,
                p.Id.Version.Build,
                p.Id.Version.Revision))
            .FirstOrDefault();
    }
}
