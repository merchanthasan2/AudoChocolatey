using System.Security.Cryptography;
using System.Text;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Chocolatey;
using QuantumUpdater.Services.Inventory;
using QuantumUpdater.Services.Scoop;
using QuantumUpdater.Services.Updates;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Catalog;

public enum MarketplaceItemState
{
    Install,
    Installed,
    UpdateAvailable,
}

public enum CatalogSourceKind
{
    Winget,
    Store,
    Scoop,
    Chocolatey,
}

public sealed record MarketplaceItem(
    string CatalogId,
    string Name,
    string Publisher,
    string Version,
    string SourceLabel,
    string? Summary,
    string? Category,
    bool IsPopular,
    MarketplaceItemState State,
    string? InstalledVersion,
    string? UpdateVersion,
    CatalogSourceKind SourceKind = CatalogSourceKind.Winget,
    string? BucketName = null,
    string? BucketUrl = null,
    string? Homepage = null,
    string? License = null);

/// <summary>
/// Full Marketplace catalog engine (46.6.4, ADR 050, ADR 051):
/// - Curated 10-category home picks (including NetSpeedBoss via Scoop default bucket)
/// - Full multi-engine live search across Winget COM, Microsoft Store, Winget CLI, Scoop, and Chocolatey
/// - Live category "See all" browsing by tag + query across the remote winget catalog
/// - 7-day cached package details (<see cref="CatalogDetails"/>)
/// - 1-click queued installs via <see cref="ActionQueue"/>
/// </summary>
public sealed class MarketplaceCatalog
{
    private const int DetailsSchema = 1;
    private static readonly TimeSpan DetailsTtl = TimeSpan.FromDays(7);

    private readonly WingetReader _reader;
    private readonly WingetInstaller _installer;
    private readonly MarketplaceService _cli;
    private readonly InstalledCatalog _installed;
    private readonly UpdatesCatalog _updates;
    private readonly ActionQueue _queue;
    private readonly SnapshotCache _cache;
    private readonly Func<string, bool, Task>? _reportActivity;
    private readonly PicksCatalog _picks;
    private readonly ScoopService? _scoop;
    private readonly ChocolateyService? _choco;

    public MarketplaceCatalog(
        WingetReader reader,
        WingetInstaller installer,
        MarketplaceService cli,
        InstalledCatalog installed,
        UpdatesCatalog updates,
        ActionQueue queue,
        SnapshotCache cache,
        Func<string, bool, Task>? reportActivity = null,
        PicksCatalog? picks = null,
        ScoopService? scoop = null,
        ChocolateyService? choco = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _cli = cli ?? throw new ArgumentNullException(nameof(cli));
        _installed = installed ?? throw new ArgumentNullException(nameof(installed));
        _updates = updates ?? throw new ArgumentNullException(nameof(updates));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _reportActivity = reportActivity;
        _picks = picks ?? CatalogPicks.LoadDefault();
        _scoop = scoop;
        _choco = choco;
    }

    public IReadOnlyList<CatalogCategory> Categories => _picks.Categories;

    public IReadOnlyList<MarketplaceItem> GetPopularPicks()
    {
        return _picks.Picks
            .Where(p => p.Popular)
            .Select(ToItem)
            .ToList();
    }

    public IReadOnlyList<MarketplaceItem> GetCategoryPicks(string categoryId)
    {
        return _picks.Picks
            .Where(p => string.Equals(p.Category, categoryId, StringComparison.OrdinalIgnoreCase))
            .Select(ToItem)
            .ToList();
    }

    public IReadOnlyList<MarketplaceItem> GetAllPicks()
    {
        return _picks.Picks.Select(ToItem).ToList();
    }

    /// <summary>
    /// Searches all configured package managers (Winget COM + Store, Winget CLI, Scoop, and Chocolatey)
    /// in parallel and merges everything cleanly.
    /// </summary>
    public async Task<IReadOnlyList<MarketplaceItem>> SearchFullCatalogAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return GetAllPicks();
        }

        var merged = new Dictionary<string, MarketplaceItem>(StringComparer.OrdinalIgnoreCase);

        // 1. Curated picks matching locally give immediate coverage even if offline.
        foreach (var pick in _picks.Picks)
        {
            if (pick.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || pick.Id.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || pick.Publisher.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || pick.Summary.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                var item = ToItem(pick);
                merged[$"{item.SourceKind}:{item.CatalogId}"] = item;
            }
        }

        // 2. Run Winget COM, Winget CLI, Scoop, and Chocolatey searches in parallel.
        var comTask = TryComSearchAsync(trimmed, cancellationToken);
        var cliTask = _cli.SearchAsync(trimmed, cancellationToken);
        var scoopTask = _scoop is not null ? _scoop.SearchAsync(trimmed, cancellationToken) : Task.FromResult<IReadOnlyList<ScoopPackage>>(Array.Empty<ScoopPackage>());
        var chocoTask = _choco is not null ? _choco.SearchAsync(trimmed, cancellationToken) : Task.FromResult<IReadOnlyList<ChocoPackage>>(Array.Empty<ChocoPackage>());
        await Task.WhenAll(comTask, cliTask, scoopTask, chocoTask).ConfigureAwait(false);

        var comResults = await comTask.ConfigureAwait(false);
        var cliResults = await cliTask.ConfigureAwait(false);
        var scoopResults = await scoopTask.ConfigureAwait(false);
        var chocoResults = await chocoTask.ConfigureAwait(false);

        foreach (var app in comResults)
        {
            var id = app.CatalogId ?? app.LocalId;
            if (!PackageId.IsCatalogId(id)) continue;

            var kind = string.Equals(app.SourceName, "Microsoft Store", StringComparison.OrdinalIgnoreCase)
                ? CatalogSourceKind.Store
                : CatalogSourceKind.Winget;
            var key = $"{CatalogSourceKind.Winget}:{id}";
            if (merged.TryGetValue(key, out var existing))
            {
                merged[key] = existing with
                {
                    Publisher = string.IsNullOrWhiteSpace(app.Publisher) ? existing.Publisher : app.Publisher,
                    SourceLabel = app.SourceName ?? existing.SourceLabel,
                    SourceKind = kind,
                };
            }
            else
            {
                merged[$"{kind}:{id}"] = ToItem(
                    id,
                    string.IsNullOrWhiteSpace(app.Name) ? id : app.Name,
                    app.Publisher ?? "Microsoft catalog",
                    app.AvailableVersion ?? app.InstalledVersion ?? "",
                    app.SourceName ?? "Microsoft catalog",
                    summary: null,
                    category: null,
                    isPopular: false,
                    sourceKind: kind);
            }
        }

        foreach (var pkg in cliResults)
        {
            if (!PackageId.IsCatalogId(pkg.Id)) continue;
            var kind = string.Equals(pkg.Source, "Microsoft Store", StringComparison.OrdinalIgnoreCase)
                ? CatalogSourceKind.Store
                : CatalogSourceKind.Winget;
            var key = $"{kind}:{pkg.Id}";
            if (merged.ContainsKey($"{CatalogSourceKind.Winget}:{pkg.Id}") || merged.ContainsKey(key))
            {
                continue;
            }

            merged[key] = ToItem(
                pkg.Id,
                pkg.Name,
                pkg.Source ?? "Microsoft catalog",
                pkg.Version,
                pkg.Source ?? "Microsoft catalog",
                summary: null,
                category: null,
                isPopular: false,
                sourceKind: kind);
        }

        foreach (var s in scoopResults)
        {
            var key = $"{CatalogSourceKind.Scoop}:{s.Name}";
            if (merged.ContainsKey(key)) continue;

            merged[key] = ToItem(
                s.Name,
                s.Name,
                string.IsNullOrWhiteSpace(s.Bucket) ? "Scoop" : $"Scoop ({s.Bucket})",
                s.Version,
                string.IsNullOrWhiteSpace(s.Bucket) ? "Scoop" : $"Scoop · {s.Bucket}",
                summary: null,
                category: null,
                isPopular: false,
                sourceKind: CatalogSourceKind.Scoop,
                bucketName: s.Bucket);
        }

        foreach (var c in chocoResults)
        {
            var key = $"{CatalogSourceKind.Chocolatey}:{c.Id}";
            if (merged.ContainsKey(key)) continue;

            merged[key] = ToItem(
                c.Id,
                c.Id,
                "Chocolatey",
                c.Version,
                "Chocolatey",
                summary: null,
                category: null,
                isPopular: false,
                sourceKind: CatalogSourceKind.Chocolatey);
        }

        return merged.Values
            .OrderByDescending(m => string.Equals(m.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.IsPopular)
            .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<MarketplaceItem>> BrowseCategoryAsync(
        string categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = _picks.Categories.FirstOrDefault(c =>
            string.Equals(c.Id, categoryId, StringComparison.OrdinalIgnoreCase));

        var merged = new Dictionary<string, MarketplaceItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var pick in _picks.Picks.Where(p => string.Equals(p.Category, categoryId, StringComparison.OrdinalIgnoreCase)))
        {
            merged[pick.Id] = ToItem(pick);
        }

        if (category is null)
        {
            return merged.Values.ToList();
        }

        try
        {
            var tagged = await _reader.ByTagAsync(category.Tag, 200, cancellationToken).ConfigureAwait(false);
            foreach (var app in tagged)
            {
                var id = app.CatalogId ?? app.LocalId;
                if (!PackageId.IsCatalogId(id) || merged.ContainsKey(id)) continue;

                merged[id] = ToItem(
                    id,
                    string.IsNullOrWhiteSpace(app.Name) ? id : app.Name,
                    app.Publisher ?? "Microsoft catalog",
                    app.AvailableVersion ?? app.InstalledVersion ?? "",
                    app.SourceName ?? "Microsoft catalog",
                    summary: null,
                    category: categoryId,
                    isPopular: false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Continue with curated picks + CLI fallback.
        }

        if (merged.Count < 25)
        {
            var cliTagged = await _cli.SearchAsync(category.Query, cancellationToken).ConfigureAwait(false);
            foreach (var pkg in cliTagged)
            {
                if (!PackageId.IsCatalogId(pkg.Id) || merged.ContainsKey(pkg.Id)) continue;

                merged[pkg.Id] = ToItem(
                    pkg.Id,
                    pkg.Name,
                    pkg.Source ?? "Microsoft catalog",
                    pkg.Version,
                    pkg.Source ?? "Microsoft catalog",
                    summary: null,
                    category: categoryId,
                    isPopular: false);
            }
        }

        return merged.Values
            .OrderByDescending(m => m.IsPopular)
            .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<CatalogDetails?> GetDetailsAsync(string catalogId, CancellationToken cancellationToken = default)
    {
        if (!PackageId.IsCatalogId(catalogId))
        {
            return null;
        }

        var pick = _picks.Picks.FirstOrDefault(p => string.Equals(p.Id, catalogId, StringComparison.OrdinalIgnoreCase));
        if (pick is not null && !string.Equals(pick.Source, "winget", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogDetails(
                pick.Id,
                pick.Name,
                pick.Publisher,
                pick.Summary,
                pick.Homepage,
                pick.License,
                pick.Version,
                Array.Empty<CatalogIcon>());
        }

        var cacheKey = "details-" + HashId(catalogId);
        var cached = _cache.Load<CatalogDetails>(cacheKey, DetailsSchema);
        if (cached is not null && DateTimeOffset.UtcNow - cached.SavedAt < DetailsTtl)
        {
            return cached.Value;
        }

        try
        {
            var details = await _reader.DetailsAsync(catalogId, cancellationToken).ConfigureAwait(false);
            if (details is not null)
            {
                _cache.Save(cacheKey, DetailsSchema, details);
                return details;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Fallback to pick info if offline/COM unavailable.
        }

        if (pick is not null)
        {
            return new CatalogDetails(
                pick.Id,
                pick.Name,
                pick.Publisher,
                pick.Summary,
                pick.Homepage,
                pick.License,
                pick.Version,
                Array.Empty<CatalogIcon>());
        }

        return null;
    }

    /// <summary>Queues a 1-click install or update through the global sequential <see cref="ActionQueue"/>.</summary>
    public QueuedAction EnqueueInstall(MarketplaceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool isUpdate = item.State == MarketplaceItemState.UpdateAvailable;
        var kind = isUpdate ? ActionKind.Update : ActionKind.Install;

        return _queue.Enqueue(
            $"{item.SourceKind}:{item.CatalogId}",
            kind,
            item.Name,
            async (progress, token) =>
            {
                ActionResult result;
                switch (item.SourceKind)
                {
                    case CatalogSourceKind.Scoop when _scoop is not null:
                        result = isUpdate
                            ? await _scoop.UpgradeAsync(item.CatalogId, progress, token).ConfigureAwait(false)
                            : await _scoop.InstallAsync(item.CatalogId, item.BucketName, item.BucketUrl, progress, token).ConfigureAwait(false);
                        break;

                    case CatalogSourceKind.Chocolatey when _choco is not null:
                        result = isUpdate
                            ? await _choco.UpgradeAsync(item.CatalogId, progress, token).ConfigureAwait(false)
                            : await _choco.InstallAsync(item.CatalogId, progress, token).ConfigureAwait(false);
                        break;

                    default:
                        result = isUpdate
                            ? await _installer.UpgradeAsync(item.CatalogId, progress, token).ConfigureAwait(false)
                            : await _installer.InstallAsync(item.CatalogId, progress, token).ConfigureAwait(false);

                        if (result.NotStarted && !token.IsCancellationRequested)
                        {
                            progress.Report(new ActionProgress(isUpdate ? "Updating" : "Installing", null));
                            result = await _cli.InstallAsync(item.CatalogId, token).ConfigureAwait(false);
                        }
                        break;
                }

                if (result.Success)
                {
                    if (isUpdate)
                    {
                        _updates.MarkUpdated(item.CatalogId, item.UpdateVersion, DateTimeOffset.Now);
                    }

                    _ = _installed.RefreshAsync(cancellationToken: CancellationToken.None);
                }

                if (_reportActivity is not null && !token.IsCancellationRequested)
                {
                    try
                    {
                        await _reportActivity(item.CatalogId, result.Success).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore activity reporting error.
                    }
                }

                return new ActionOutcome(result.Success, result.Text, result.StillRunning);
            });
    }

    public MarketplaceItem RefreshItemState(MarketplaceItem item)
    {
        var (state, installedVer, updateVer) = ResolveState(item.CatalogId);
        return item with
        {
            State = state,
            InstalledVersion = installedVer,
            UpdateVersion = updateVer,
        };
    }

    private MarketplaceItem ToItem(CatalogPick pick)
    {
        var kind = pick.Source switch
        {
            "scoop" => CatalogSourceKind.Scoop,
            "choco" => CatalogSourceKind.Chocolatey,
            "msstore" => CatalogSourceKind.Store,
            _ => CatalogSourceKind.Winget,
        };
        var label = kind switch
        {
            CatalogSourceKind.Scoop => string.IsNullOrWhiteSpace(pick.Bucket) ? "Scoop" : $"Scoop · {pick.Bucket}",
            CatalogSourceKind.Chocolatey => "Chocolatey",
            CatalogSourceKind.Store => "Microsoft Store",
            _ => "Microsoft catalog",
        };

        return ToItem(
            pick.Id,
            pick.Name,
            pick.Publisher,
            pick.Version ?? "",
            label,
            pick.Summary,
            pick.Category,
            pick.Popular,
            kind,
            pick.Bucket,
            pick.BucketUrl,
            pick.Homepage,
            pick.License);
    }

    private MarketplaceItem ToItem(
        string catalogId,
        string name,
        string publisher,
        string version,
        string sourceLabel,
        string? summary,
        string? category,
        bool isPopular,
        CatalogSourceKind sourceKind = CatalogSourceKind.Winget,
        string? bucketName = null,
        string? bucketUrl = null,
        string? homepage = null,
        string? license = null)
    {
        var (state, installedVer, updateVer) = ResolveState(catalogId);
        return new MarketplaceItem(
            CatalogId: catalogId,
            Name: name,
            Publisher: publisher,
            Version: version,
            SourceLabel: sourceLabel,
            Summary: summary,
            Category: category,
            IsPopular: isPopular,
            State: state,
            InstalledVersion: installedVer,
            UpdateVersion: updateVer,
            SourceKind: sourceKind,
            BucketName: bucketName,
            BucketUrl: bucketUrl,
            Homepage: homepage,
            License: license);
    }

    private (MarketplaceItemState State, string? InstalledVersion, string? UpdateVersion) ResolveState(string catalogId)
    {
        var upgrade = _updates.Entries.FirstOrDefault(e =>
            e.Group is UpgradeGroup.Ready or UpgradeGroup.VersionNotKnown
            && string.Equals(e.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase));
        if (upgrade is not null)
        {
            return (MarketplaceItemState.UpdateAvailable, upgrade.InstalledVersion, upgrade.AvailableVersion);
        }

        var installed = _installed.FindByCatalogId(catalogId);
        if (installed is not null)
        {
            return (MarketplaceItemState.Installed, installed.Version, installed.UpdateVersion);
        }

        return (MarketplaceItemState.Install, null, null);
    }

    private async Task<IReadOnlyList<CatalogApp>> TryComSearchAsync(string query, CancellationToken token)
    {
        try
        {
            return await _reader.SearchAsync(query, WingetReader.MaxResultLimit, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Array.Empty<CatalogApp>();
        }
    }

    private static string HashId(string id) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToLowerInvariant())))[..16].ToLowerInvariant();
}
