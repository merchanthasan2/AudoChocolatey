using Windows.ApplicationModel;
using Windows.Management.Deployment;
using QuantumUpdater.Models;

namespace QuantumUpdater.Services.Inventory;

/// <summary>
/// Reads Microsoft Store and MSIX apps for the current user (section 5.2).
/// It only reads. It never removes or changes a package.
/// </summary>
public static class StoreAppInventory
{
    /// <summary>
    /// Reads the packages for the current user. The cheap flags are read first,
    /// so the slow fields are read only for packages that the filter keeps.
    /// One bad package is skipped. The rest still load.
    /// </summary>
    public static IReadOnlyList<AppEntry> Read()
    {
        var rows = new List<AppEntry>();
        List<Package> packages;
        try
        {
            packages = new PackageManager().FindPackagesForUser(string.Empty).ToList();
        }
        catch (Exception)
        {
            // The package list cannot be read. The Store part is empty.
            return rows;
        }

        foreach (var package in packages)
        {
            try
            {
                var entry = ReadRow(package);
                if (entry is not null)
                {
                    rows.Add(entry);
                }
            }
            catch (Exception)
            {
                // This package cannot be read. The scan goes on with the next package.
            }
        }

        return rows;
    }

    private static AppEntry? ReadRow(Package package)
    {
        var id = package.Id;
        var flags = new StorePackageFlags(
            IdName: id.Name,
            IsFramework: package.IsFramework,
            IsResourcePackage: package.IsResourcePackage,
            IsBundle: package.IsBundle,
            Signature: MapSignature(package.SignatureKind));

        if (StoreAppRules.IsSkipped(flags))
        {
            return null;
        }

        var version = id.Version;
        var fields = new StorePackageFields(
            Flags: flags,
            FamilyName: id.FamilyName,
            FullName: id.FullName,
            DisplayName: package.DisplayName,
            PublisherDisplayName: package.PublisherDisplayName,
            Version: $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}",
            // InstalledPath needs Windows 10 build 19041. The app can run on older builds, so check first.
            InstallLocation: OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) ? package.InstalledPath : null,
            // InstalledDate is slow on this PC (about 4 ms per package). It stays empty to keep the read under 0.8 s.
            InstallDate: null,
            // The logo adds about 200 ms on a cold start. It stays empty until the icon task reads it.
            LogoUri: null);

        return StoreAppRules.MapPackage(fields);
    }

    private static StoreSignature MapSignature(PackageSignatureKind kind)
    {
        return kind switch
        {
            PackageSignatureKind.None => StoreSignature.None,
            PackageSignatureKind.Developer => StoreSignature.Developer,
            PackageSignatureKind.Enterprise => StoreSignature.Enterprise,
            PackageSignatureKind.System => StoreSignature.System,
            PackageSignatureKind.Store => StoreSignature.Store,
            _ => StoreSignature.Unknown,
        };
    }
}
