namespace QuantumUpdater.Services;

/// <summary>
/// The only door from screens to the PC. Screens do not start processes themselves.
/// </summary>
public sealed class AppServices
{
    /// <param name="post">Runs an action on the screen thread. Tests pass <c>a =&gt; a()</c>.</param>
    public AppServices(Action<Action> post)
    {
        Queue = new ActionQueue(post);
    }

    /// <summary>The one action queue for the whole app.</summary>
    public ActionQueue Queue { get; }

    public WingetService Winget { get; } = new();

    public InstalledService Installed { get; } = new();

    public MarketplaceService Marketplace { get; } = new();

    public UpdateService Update { get; } = new();

    public AccountClient Account { get; } = new();

    public SessionStore Session { get; } = new();

    public CatalogSnapshot? Catalog { get; set; }
}
