using QuantumUpdater.Services.Catalog;
using QuantumUpdater.Services.Cleanup;
using QuantumUpdater.Services.Chocolatey;
using QuantumUpdater.Services.Inventory;
using QuantumUpdater.Services.Scoop;
using QuantumUpdater.Services.Updates;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services;

/// <summary>
/// The only door from screens to the PC. Screens do not start processes themselves.
/// </summary>
public sealed class AppServices
{
    /// <param name="post">Runs an action on the screen thread. Tests pass <c>a =&gt; a()</c>.</param>
    public AppServices(Action<Action> post)
    {
        Post = post ?? throw new ArgumentNullException(nameof(post));
        Queue = new ActionQueue(post);
        Reader = new WingetReader();
        Installer = new WingetInstaller(Reader);
        Icons = new IconService(Reader);
        Scoop = new ScoopService();
        Chocolatey = new ChocolateyService();

        var comUpdates = new ComUpdatesSource(Reader);
        var cliUpdates = new CliUpdatesSource(Winget);
        Updates = new UpdatesCatalog(comUpdates, cliUpdates, SnapshotCache.Default, post);
        Updates.LoadSaved();

        UpdateActions = new UpdateActions(
            Queue,
            Updates,
            Installer.UpgradeAsync,
            Winget.UpgradeAsync,
            (id, ok) => Account.ReportActivityAsync(Session, "upgrade", id, ok));

        InstalledApps = new InstalledCatalog(Reader, Installed, SnapshotCache.Default, post, Scoop, Chocolatey);
        InstalledApps.LoadSaved();

        Uninstaller = new UninstallRunner(
            Queue,
            Installed,
            InstalledApps,
            (id, ok) => Account.ReportActivityAsync(Session, "remove", id, ok),
            Scoop,
            Chocolatey);

        Catalog = new MarketplaceCatalog(
            Reader,
            Installer,
            Marketplace,
            InstalledApps,
            Updates,
            Queue,
            SnapshotCache.Default,
            (id, ok) => Account.ReportActivityAsync(Session, "install", id, ok),
            scoop: Scoop,
            choco: Chocolatey);
    }

    public Action<Action> Post { get; }

    /// <summary>The one action queue for the whole app.</summary>
    public ActionQueue Queue { get; }

    public WingetReader Reader { get; }

    public WingetInstaller Installer { get; }

    public IconService Icons { get; }

    public ScoopService Scoop { get; }

    public ChocolateyService Chocolatey { get; }

    public WingetService Winget { get; } = new();

    public InstalledService Installed { get; } = new();

    public MarketplaceService Marketplace { get; } = new();

    public UpdateService Update { get; } = new();

    public AccountClient Account { get; } = new();

    public SessionStore Session { get; } = new();

    public DiskService Disk { get; } = new();

    public CleanupService Cleanup { get; } = new();

    public StartWithWindowsService StartWithWindows { get; } = new();

    public UpdatesCatalog Updates { get; }

    public UpdateActions UpdateActions { get; }

    public InstalledCatalog InstalledApps { get; }

    public UninstallRunner Uninstaller { get; }

    public MarketplaceCatalog Catalog { get; }

    public CatalogSnapshot? CatalogSnapshot { get; set; }
}
