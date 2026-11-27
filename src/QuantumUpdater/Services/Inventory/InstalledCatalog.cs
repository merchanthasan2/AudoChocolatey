using QuantumUpdater.Models;
using QuantumUpdater.Services.Chocolatey;
using QuantumUpdater.Services.Scan;
using QuantumUpdater.Services.Scoop;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Inventory;

/// <summary>
/// Holds the merged installed applications list (Registry + Store + Winget + Scoop + Chocolatey),
/// backed by <see cref="SnapshotCache"/> so Installed opens immediately (46.5.17, 46.21).
/// </summary>
public sealed class InstalledCatalog
{
    private const string CacheKey = "installed";
    private const int CacheSchema = 2;

    private readonly WingetReader _reader;
    private readonly InstalledService _cliFallback;
    private readonly SnapshotCache _cache;
    private readonly Action<Action> _post;
    private readonly ScoopService? _scoop;
    private readonly ChocolateyService? _choco;
    private readonly object _gate = new();

    private IReadOnlyList<AppEntry> _apps = Array.Empty<AppEntry>();
    private Task<IReadOnlyList<AppEntry>>? _inFlight;

    public InstalledCatalog(
        WingetReader reader,
        InstalledService cliFallback,
        SnapshotCache cache,
        Action<Action> post,
        ScoopService? scoop = null,
        ChocolateyService? choco = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _cliFallback = cliFallback ?? throw new ArgumentNullException(nameof(cliFallback));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _post = post ?? throw new ArgumentNullException(nameof(post));
        _scoop = scoop;
        _choco = choco;
    }

    public event Action? Changed;

    public IReadOnlyList<AppEntry> Apps
    {
        get { lock (_gate) return _apps; }
    }

    public DateTimeOffset? SavedAt
    {
        get { lock (_gate) return field; }
        private set { lock (_gate) field = value; }
    }

    public string? Error
    {
        get { lock (_gate) return field; }
        private set { lock (_gate) field = value; }
    }

    public bool LoadSaved()
    {
        var snap = _cache.Load<List<AppEntry>>(CacheKey, CacheSchema);
        if (snap is null || snap.Value is null)
        {
            return false;
        }

        lock (_gate)
        {
            _apps = snap.Value;
            SavedAt = snap.SavedAt;
        }

        RaiseChanged();
        return true;
    }

    public Task<IReadOnlyList<AppEntry>> RefreshAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<AppEntry>>? running;
        lock (_gate)
        {
            if (_inFlight is not null && !_inFlight.IsCompleted)
            {
                running = _inFlight;
            }
            else
            {
                Error = null;
                running = RunRefreshAsync(progress, cancellationToken);
                _inFlight = running;
            }
        }

        return running;
    }

    /// <summary>Checks whether a catalog id (winget, scoop, or choco) is currently installed on this PC.</summary>
    public AppEntry? FindByCatalogId(string? catalogId)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            return null;
        }

        lock (_gate)
        {
            return _apps.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.CatalogId)
                && string.Equals(a.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void RemoveEntry(string key)
    {
        lock (_gate)
        {
            _apps = _apps.Where(a => !string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase)).ToList();
            _cache.Save(CacheKey, CacheSchema, _apps.ToList());
        }

        RaiseChanged();
    }

    private async Task<IReadOnlyList<AppEntry>> RunRefreshAsync(
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report(new ScanProgress(1, 3, 0, 0));
            var regTask = Task.Run(RegistryInventory.Read, cancellationToken);
            var storeTask = Task.Run(StoreAppInventory.Read, cancellationToken);
            var scoopTask = _scoop is not null ? _scoop.ListInstalledAsync(cancellationToken) : Task.FromResult<IReadOnlyList<ScoopPackage>>(Array.Empty<ScoopPackage>());
            var chocoTask = _choco is not null ? _choco.ListInstalledAsync(cancellationToken) : Task.FromResult<IReadOnlyList<ChocoPackage>>(Array.Empty<ChocoPackage>());
            await Task.WhenAll(regTask, storeTask, scoopTask, chocoTask).ConfigureAwait(false);

            var reg = await regTask.ConfigureAwait(false);
            var store = await storeTask.ConfigureAwait(false);
            var scoopApps = await scoopTask.ConfigureAwait(false);
            var chocoApps = await chocoTask.ConfigureAwait(false);

            // Publish Windows-only rows immediately if we had nothing yet so UI doesn't wait for COM.
            lock (_gate)
            {
                if (_apps.Count == 0)
                {
                    _apps = AppendExtraManagers(InstalledMerger.Merge(reg, store, Array.Empty<CatalogApp>()), scoopApps, chocoApps);
                }
            }
            RaiseChanged();

            progress?.Report(new ScanProgress(2, 3, 0, 0));
            IReadOnlyList<CatalogApp> wingetApps;
            try
            {
                wingetApps = await _reader.ListInstalledAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                var cli = await _cliFallback.ListInstalledAsync(cancellationToken).ConfigureAwait(false);
                wingetApps = cli.Select(c => new CatalogApp(
                    LocalId: c.Id,
                    CatalogId: c.CanAct ? c.Id : null,
                    Name: c.Name,
                    Publisher: null,
                    InstalledVersion: c.Version,
                    AvailableVersion: null,
                    IsUpdateAvailable: false,
                    ProductCodes: Array.Empty<string>(),
                    FamilyNames: Array.Empty<string>())).ToList();
            }

            var merged = AppendExtraManagers(InstalledMerger.Merge(reg, store, wingetApps), scoopApps, chocoApps);
            progress?.Report(new ScanProgress(3, 3, 0, 0));

            lock (_gate)
            {
                _apps = merged;
                SavedAt = DateTimeOffset.UtcNow;
                Error = null;
                _cache.Save(CacheKey, CacheSchema, merged.ToList());
            }

            RaiseChanged();
            return merged;
        }
        catch (OperationCanceledException)
        {
            return Apps;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                Error = string.IsNullOrWhiteSpace(ex.Message) ? "Installed scan failed." : ex.Message;
            }
            RaiseChanged();
            return Apps;
        }
    }

    private static IReadOnlyList<AppEntry> AppendExtraManagers(
        IReadOnlyList<AppEntry> baseList,
        IReadOnlyList<ScoopPackage> scoopApps,
        IReadOnlyList<ChocoPackage> chocoApps)
    {
        if (scoopApps.Count == 0 && chocoApps.Count == 0)
        {
            return baseList;
        }

        var list = baseList.ToList();
        foreach (var s in scoopApps)
        {
            list.Add(new AppEntry(
                Key: "scoop:" + s.Name,
                Name: s.Name,
                Publisher: string.IsNullOrWhiteSpace(s.Bucket) ? "Scoop" : $"Scoop ({s.Bucket})",
                Version: s.Version,
                Source: AppSource.Scoop,
                CatalogId: s.Name,
                RegistryRef: null,
                StoreFullName: null,
                Match: MatchKind.Exact,
                Route: UninstallRoute.Scoop,
                IsProtected: false,
                SizeBytes: null,
                InstallDate: null,
                UpdateVersion: null,
                DisplayIcon: null,
                InstallLocation: null,
                ProductCode: null,
                UninstallString: null,
                QuietUninstallString: null));
        }

        foreach (var c in chocoApps)
        {
            list.Add(new AppEntry(
                Key: "choco:" + c.Id,
                Name: c.Id,
                Publisher: "Chocolatey",
                Version: c.Version,
                Source: AppSource.Chocolatey,
                CatalogId: c.Id,
                RegistryRef: null,
                StoreFullName: null,
                Match: MatchKind.Exact,
                Route: UninstallRoute.Chocolatey,
                IsProtected: false,
                SizeBytes: null,
                InstallDate: null,
                UpdateVersion: null,
                DisplayIcon: null,
                InstallLocation: null,
                ProductCode: null,
                UninstallString: null,
                QuietUninstallString: null));
        }

        return list
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RaiseChanged()
    {
        try
        {
            _post(() => Changed?.Invoke());
        }
        catch
        {
            // Ignore listener errors.
        }
    }
}
