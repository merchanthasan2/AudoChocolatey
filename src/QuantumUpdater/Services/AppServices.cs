namespace QuantumUpdater.Services;

/// <summary>
/// The only door from screens to the PC. Screens do not start processes themselves.
/// </summary>
public sealed class AppServices
{
    public WingetService Winget { get; } = new();

    public InstalledService Installed { get; } = new();

    public MarketplaceService Marketplace { get; } = new();

    public UpdateService Update { get; } = new();

    public AccountClient Account { get; } = new();

    public SessionStore Session { get; } = new();

    public CatalogSnapshot? Catalog { get; set; }
}
