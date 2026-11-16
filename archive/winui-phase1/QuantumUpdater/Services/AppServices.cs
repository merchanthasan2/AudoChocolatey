namespace QuantumUpdater.Services;

/// <summary>Application service facade for the WinUI shell (Phase 44.1).</summary>
public sealed class AppServices
{
    public WingetService Winget { get; } = new();

    public InstalledService Installed { get; } = new();

    public MarketplaceService Marketplace { get; } = new();

    public UpdateService Update { get; } = new();

    // ScanService — catalog / inventory scans (Phase 44.2+).
    // ChocoService — Chocolatey integration (Phase 44.2+).
}