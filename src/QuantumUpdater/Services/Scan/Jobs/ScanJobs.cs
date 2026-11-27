using QuantumUpdater.Models;
using QuantumUpdater.Services.Catalog;
using QuantumUpdater.Services.Inventory;
using QuantumUpdater.Services.Updates;

namespace QuantumUpdater.Services.Scan.Jobs;

public sealed class UpdatesScanJob : IScanJob
{
    private readonly UpdatesCatalog _updates;

    public UpdatesScanJob(UpdatesCatalog updates) => _updates = updates;

    public string Id => "updates";
    public string Title => "App updates";

    public async Task<ScanResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken token)
    {
        var entries = await _updates.RefreshAsync(progress, token).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(_updates.Error) && entries.Count == 0)
        {
            return ScanResult.Failed(_updates.Error);
        }

        int pending = entries.Count(e => e.Group is UpgradeGroup.Ready or UpgradeGroup.VersionNotKnown);
        string summary = pending == 0
            ? "All apps are up to date"
            : pending == 1 ? "1 update ready" : $"{pending} updates ready";
        return ScanResult.Done(summary);
    }
}

public sealed class InstalledScanJob : IScanJob
{
    private readonly InstalledCatalog _installed;

    public InstalledScanJob(InstalledCatalog installed) => _installed = installed;

    public string Id => "installed";
    public string Title => "Installed apps";

    public async Task<ScanResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken token)
    {
        var apps = await _installed.RefreshAsync(progress, token).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(_installed.Error) && apps.Count == 0)
        {
            return ScanResult.Failed(_installed.Error);
        }

        return ScanResult.Done($"{apps.Count} apps on this PC");
    }
}

public sealed class MarketplaceScanJob : IScanJob
{
    private readonly MarketplaceCatalog _catalog;

    public MarketplaceScanJob(MarketplaceCatalog catalog) => _catalog = catalog;

    public string Id => "marketplace";
    public string Title => "App Marketplace";

    public Task<ScanResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken token)
    {
        progress.Report(new ScanProgress(1, 2, 0, 0));
        var picks = _catalog.GetAllPicks();
        var catCount = _catalog.Categories.Count;
        progress.Report(new ScanProgress(2, 2, 0, 0));
        return Task.FromResult(ScanResult.Done($"{picks.Count} curated picks in {catCount} categories · full catalog ready"));
    }
}

public sealed class IconScanJob : IScanJob
{
    private static readonly string[] Deps = { "installed", "marketplace" };
    private static readonly TimeSpan WarmBudget = TimeSpan.FromSeconds(7);

    private readonly InstalledCatalog _installed;
    private readonly MarketplaceCatalog _catalog;
    private readonly IconService _icons;

    public IconScanJob(InstalledCatalog installed, MarketplaceCatalog catalog, IconService icons)
    {
        _installed = installed;
        _catalog = catalog;
        _icons = icons;
    }

    public string Id => "icons";
    public string Title => "App icons";
    public IReadOnlyList<string> DependsOn => Deps;

    public async Task<ScanResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken token)
    {
        var apps = _installed.Apps;
        var popular = _catalog.GetPopularPicks();
        int total = apps.Count + popular.Count;
        if (total == 0)
        {
            return ScanResult.Done("Ready");
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(WarmBudget);

        int done = 0;
        int original = 0;

        // Fast pass over local installed apps first (registry DisplayIcon / MSIX / EXE).
        foreach (var app in apps)
        {
            if (budget.IsCancellationRequested) break;
            try
            {
                if (_icons.TryGetCachedPath(app.Key) is not null
                    || await _icons.ResolveInstalledAsync(app, budget.Token).ConfigureAwait(false) is not null)
                {
                    original++;
                }
            }
            catch
            {
                // Keep scanning other icons.
            }

            done++;
            if (done % 8 == 0 || done == total)
            {
                progress.Report(new ScanProgress(0, 0, done, total));
            }
        }

        foreach (var pick in popular)
        {
            if (budget.IsCancellationRequested) break;
            try
            {
                if (_icons.TryGetCachedPath("catalog:" + pick.CatalogId) is not null
                    || await _icons.ResolveCatalogAsync(pick.CatalogId, budget.Token).ConfigureAwait(false) is not null)
                {
                    original++;
                }
            }
            catch
            {
                // Keep scanning.
            }

            done++;
            if (done % 4 == 0 || done == total)
            {
                progress.Report(new ScanProgress(0, 0, done, total));
            }
        }

        int fallbacks = Math.Max(0, done - original);
        return ScanResult.Done($"{original} of {done} original icons · {fallbacks} letter tiles");
    }
}

public sealed class DriveScanJob : IScanJob
{
    private readonly DiskService _disk;

    public DriveScanJob(DiskService disk) => _disk = disk;

    public string Id => "drives";
    public string Title => "Drives";

    public async Task<ScanResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken token)
    {
        progress.Report(new ScanProgress(1, 2, 0, 0));
        var volumes = _disk.Volumes();
        var disks = await _disk.PhysicalDisksAsync(token).ConfigureAwait(false);
        progress.Report(new ScanProgress(2, 2, 0, 0));

        var cDrive = volumes.FirstOrDefault(v => v.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase))
            ?? volumes.FirstOrDefault();
        string freeText = cDrive is null
            ? "No fixed drives found"
            : $"{cDrive.Free / (1024.0 * 1024 * 1024):0.#} GB free on {cDrive.Name.TrimEnd('\\')}";

        int healthy = disks.Count(d => string.Equals(d.Health, "Healthy", StringComparison.OrdinalIgnoreCase));
        if (disks.Count > 0)
        {
            return ScanResult.Done($"{healthy} of {disks.Count} drives healthy · {freeText}");
        }

        return ScanResult.Done($"{volumes.Count} drive(s) · {freeText}");
    }
}

public sealed class SelfUpdateScanJob : IScanJob
{
    private readonly UpdateService _update;

    public SelfUpdateScanJob(UpdateService update) => _update = update;

    public string Id => "self";
    public string Title => "DeskVerve version";

    public async Task<ScanResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken token)
    {
        var result = await _update.CheckAsync(token).ConfigureAwait(false);
        return result.HasUpdate
            ? ScanResult.Done($"DeskVerve {result.LatestTag} is ready")
            : ScanResult.Done($"DeskVerve {Models.AppVersion.Label} is up to date");
    }
}
