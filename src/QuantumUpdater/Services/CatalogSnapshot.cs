namespace QuantumUpdater.Services;

/// <summary>Last Getting ready scan. Failed jobs stay visible and do not block the app.</summary>
public sealed class CatalogSnapshot
{
    public IReadOnlyList<WingetPackage> Upgrades { get; init; } = Array.Empty<WingetPackage>();

    public string? UpgradesError { get; init; }

    public IReadOnlyList<InstalledPackage> Installed { get; init; } = Array.Empty<InstalledPackage>();

    public string? InstalledError { get; init; }
}
