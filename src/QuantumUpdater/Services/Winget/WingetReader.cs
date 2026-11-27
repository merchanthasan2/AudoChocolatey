using System.Text.RegularExpressions;
using Microsoft.Management.Deployment;

namespace QuantumUpdater.Services.Winget;

/// <summary>
/// Reads winget data through the COM API (in-proc factory).
/// Installed and update data use the composite catalog (remote winget catalog plus local installed apps).
/// Search, tag and details use the remote winget catalog only.
/// Only one COM call runs at a time. Each COM call has a 15 second limit.
/// </summary>
public sealed class WingetReader : IDisposable
{
    public const int MaxResultLimit = 500;

    internal sealed record ActionLookup(PackageManager Manager, CatalogPackage Package);

    private static readonly TimeSpan CallLimit = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CacheLimit = TimeSpan.FromMinutes(30);

    // Same rule as MarketplaceService.SearchQueryPattern. Copied, not moved.
    private static readonly Regex SearchTextPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9 ._+-]{0,79}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // One gate for all COM work.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Cancelled on Dispose. Not disposed on purpose: running calls still use it.
    private readonly CancellationTokenSource _life = new();

    private PackageManager? _manager;
    private PackageCatalog? _composite;
    private DateTime _compositeAt;
    private PackageCatalog? _remote;
    private DateTime _remoteAt;
    private CancellationTokenSource? _searchCts;
    private bool _disposed;

    /// <summary>Lists installed apps with update data. One FindPackages call.</summary>
    public async Task<IReadOnlyList<CatalogApp>> ListInstalledAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _life.Token);
        return await RunAsync(async token =>
        {
            var catalog = await GetCompositeAsync(token).ConfigureAwait(false);
            var result = await FindAsync(composite: true, catalog, new FindPackagesOptions(), "list", token).ConfigureAwait(false);
            return await Task.Run(() => MapMatches(result.Matches, includeAvailableVersion: true), token).ConfigureAwait(false);
        }, linked.Token).ConfigureAwait(false);
    }

    /// <summary>Searches all remote catalogs (winget + msstore). Name, Id, Moniker or Tag contains the text.</summary>
    public async Task<IReadOnlyList<CatalogApp>> SearchAsync(string text, int limit, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var query = (text ?? string.Empty).Trim();
        if (!SearchTextPattern.IsMatch(query))
        {
            throw new ArgumentException("Search text is not allowed.", nameof(text));
        }

        // A new search cancels the previous search that is still running.
        var search = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _life.Token);
        // Read the token now. A newer search can dispose this source at any time after the swap.
        var searchToken = search.Token;
        var previous = Interlocked.Exchange(ref _searchCts, search);
        previous?.Cancel();
        previous?.Dispose();

        var max = ClampLimit(limit);
        return await RunAsync(async token =>
        {
            var remote = await GetRemoteAsync(token).ConfigureAwait(false);
            var options = new FindPackagesOptions { ResultLimit = (uint)max };
            foreach (var field in new[] { PackageMatchField.Name, PackageMatchField.Id, PackageMatchField.Moniker, PackageMatchField.Tag })
            {
                options.Selectors.Add(new PackageMatchFilter
                {
                    Field = field,
                    Option = PackageFieldMatchOption.ContainsCaseInsensitive,
                    Value = query
                });
            }

            var result = await FindAsync(composite: false, remote, options, "search", token).ConfigureAwait(false);
            return await Task.Run(() => MapMatches(result.Matches, includeAvailableVersion: false), token).ConfigureAwait(false);
        }, searchToken).ConfigureAwait(false);
    }

    /// <summary>Lists remote packages with a tag. The tag must match exactly, ignoring case.</summary>
    public async Task<IReadOnlyList<CatalogApp>> ByTagAsync(string tag, int limit, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var value = (tag ?? string.Empty).Trim();
        if (!SearchTextPattern.IsMatch(value))
        {
            throw new ArgumentException("Tag is not allowed.", nameof(tag));
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _life.Token);
        var max = ClampLimit(limit);
        return await RunAsync(async token =>
        {
            var remote = await GetRemoteAsync(token).ConfigureAwait(false);
            var options = new FindPackagesOptions { ResultLimit = (uint)max };
            options.Selectors.Add(new PackageMatchFilter
            {
                Field = PackageMatchField.Tag,
                Option = PackageFieldMatchOption.EqualsCaseInsensitive,
                Value = value
            });

            var result = await FindAsync(composite: false, remote, options, "tag search", token).ConfigureAwait(false);
            return await Task.Run(() => MapMatches(result.Matches, includeAvailableVersion: false), token).ConfigureAwait(false);
        }, linked.Token).ConfigureAwait(false);
    }

    /// <summary>Finds one exact package for an install or upgrade COM action (46.7.93). Returns null unless 1 exact match.</summary>
    internal async Task<ActionLookup?> FindForActionAsync(string catalogId, bool isUpgrade, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!PackageId.IsCatalogId(catalogId))
        {
            return null;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _life.Token);
        return await RunAsync(async token =>
        {
            var catalog = isUpgrade
                ? await GetCompositeAsync(token).ConfigureAwait(false)
                : await GetRemoteAsync(token).ConfigureAwait(false);

            var options = new FindPackagesOptions();
            options.Selectors.Add(new PackageMatchFilter
            {
                Field = PackageMatchField.Id,
                Option = PackageFieldMatchOption.EqualsCaseInsensitive,
                Value = catalogId,
            });

            var result = await FindAsync(composite: isUpgrade, catalog, options, "exact lookup", token).ConfigureAwait(false);
            var matches = result.Matches;
            if (matches is null || matches.Count == 0)
            {
                return null;
            }

            CatalogPackage? chosen = null;
            int exactCount = 0;
            for (var i = 0; i < matches.Count; i++)
            {
                var candidate = matches[i]?.CatalogPackage;
                if (candidate is not null && string.Equals(candidate.Id, catalogId, StringComparison.OrdinalIgnoreCase))
                {
                    chosen = candidate;
                    exactCount++;
                }
            }

            if (exactCount != 1 || chosen is null)
            {
                return null;
            }

            return new ActionLookup(EnsureManager(), chosen);
        }, linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one package by its exact catalog id. Returns null when winget does not know the id.
    /// </summary>
    public async Task<CatalogDetails?> DetailsAsync(string catalogId, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!PackageId.IsCatalogId(catalogId))
        {
            throw new ArgumentException("Catalog id is not allowed.", nameof(catalogId));
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _life.Token);
        return await RunAsync(async token =>
        {
            var remote = await GetRemoteAsync(token).ConfigureAwait(false);
            var options = new FindPackagesOptions { ResultLimit = 1 };
            options.Selectors.Add(new PackageMatchFilter
            {
                Field = PackageMatchField.Id,
                Option = PackageFieldMatchOption.EqualsCaseInsensitive,
                Value = catalogId
            });

            var result = await FindAsync(composite: false, remote, options, "details", token).ConfigureAwait(false);
            return await Task.Run(() => MapDetails(result.Matches, catalogId), token).ConfigureAwait(false);
        }, linked.Token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // _gate and _life are not disposed on purpose. Running calls still use them.
        _life.Cancel();
        _composite = null;
        _remote = null;
        _manager = null;
        Interlocked.Exchange(ref _searchCts, null)?.Cancel();
    }

    private async Task<PackageCatalog> GetCompositeAsync(CancellationToken token)
    {
        if (_composite is not null && DateTime.UtcNow - _compositeAt < CacheLimit)
        {
            return _composite;
        }

        _composite = null;
        var reference = await Task.Run(() =>
        {
            var manager = EnsureManager();
            var remoteRef = manager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog);
            var options = new CreateCompositePackageCatalogOptions();
            options.Catalogs.Add(remoteRef);
            options.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;
            return manager.CreateCompositePackageCatalog(options);
        }, token).ConfigureAwait(false);

        var catalog = await ConnectAsync(reference, "composite catalog", token).ConfigureAwait(false);
        _composite = catalog;
        _compositeAt = DateTime.UtcNow;
        return catalog;
    }

    private async Task<PackageCatalog> GetRemoteAsync(CancellationToken token)
    {
        if (_remote is not null && DateTime.UtcNow - _remoteAt < CacheLimit)
        {
            return _remote;
        }

        _remote = null;
        var reference = await Task.Run(() =>
        {
            var manager = EnsureManager();
            try
            {
                // Include all configured non-explicit catalogs (winget + msstore) so Marketplace
                // exposes everything winget search can find. Index access avoids CsWinRT enumerator bugs.
                var catalogs = manager.GetPackageCatalogs();
                if (catalogs is not null && catalogs.Count > 0)
                {
                    var options = new CreateCompositePackageCatalogOptions
                    {
                        CompositeSearchBehavior = CompositeSearchBehavior.RemotePackagesFromRemoteCatalogs,
                    };
                    var added = 0;
                    for (var i = 0; i < catalogs.Count; i++)
                    {
                        var cat = catalogs[i];
                        if (cat is not null && !cat.Info.Explicit)
                        {
                            options.Catalogs.Add(cat);
                            added++;
                        }
                    }

                    if (added > 0)
                    {
                        return manager.CreateCompositePackageCatalog(options);
                    }
                }
            }
            catch
            {
                // Fall back to OpenWindowsCatalog if composite remote catalog setup fails.
            }

            return manager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog);
        }, token).ConfigureAwait(false);

        var catalog = await ConnectAsync(reference, "remote catalog", token).ConfigureAwait(false);
        _remote = catalog;
        _remoteAt = DateTime.UtcNow;
        return catalog;
    }

    private PackageManager EnsureManager()
    {
        _manager ??= new PackageManager();
        return _manager;
    }

    private static async Task<PackageCatalog> ConnectAsync(PackageCatalogReference reference, string name, CancellationToken token)
    {
        ConnectResult connect = await WithLimitAsync(t => reference.ConnectAsync().AsTask(t), token).ConfigureAwait(false);
        if (connect.PackageCatalog is null || connect.Status != ConnectResultStatus.Ok)
        {
            throw new WingetReaderException($"Connect to the {name} failed: {connect.Status}");
        }

        return connect.PackageCatalog;
    }

    // Cancels the COM call itself after CallLimit, so winget stops the work (W20).
    // The extra wait is a backstop for a call that ignores the cancel.
    private static async Task<T> WithLimitAsync<T>(Func<CancellationToken, Task<T>> start, CancellationToken token)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(CallLimit);
        try
        {
            return await start(limit.Token).WaitAsync(CallLimit + CancelGrace, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("The COM call did not finish in time.", ex);
        }
    }

    // A failed or timed out find clears the catalog, so the next call connects again.
    private async Task<FindPackagesResult> FindAsync(
        bool composite,
        PackageCatalog catalog,
        FindPackagesOptions options,
        string operation,
        CancellationToken token)
    {
        try
        {
            FindPackagesResult result = await WithLimitAsync(t => catalog.FindPackagesAsync(options).AsTask(t), token).ConfigureAwait(false);
            if (result.Status != FindPackagesResultStatus.Ok)
            {
                throw new WingetReaderException($"The {operation} failed: {result.Status}");
            }

            return result;
        }
        catch (Exception ex) when (ex is WingetReaderException or TimeoutException)
        {
            ResetCatalog(composite);
            throw;
        }
    }

    private void ResetCatalog(bool composite)
    {
        if (composite)
        {
            _composite = null;
        }
        else
        {
            _remote = null;
        }
    }

    // One bad package is skipped. The rest of the list still returns.
    // Index access, not foreach. The COM collection enumerator fails in this process (CsWinRT 'No such interface').
    private static IReadOnlyList<CatalogApp> MapMatches(IReadOnlyList<MatchResult>? matches, bool includeAvailableVersion)
    {
        var rows = new List<CatalogApp>();
        if (matches is null)
        {
            return rows;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            try
            {
                var package = matches[i]?.CatalogPackage;
                if (package is null)
                {
                    continue;
                }

                rows.Add(MapPackage(package, includeAvailableVersion));
            }
            catch (Exception)
            {
                // Skip this package. Do not log package data.
            }
        }

        return rows;
    }

    // Index access, not foreach. Same reason as MapMatches.
    private static CatalogDetails? MapDetails(IReadOnlyList<MatchResult>? matches, string catalogId)
    {
        CatalogPackage? package = null;
        for (var i = 0; matches is not null && i < matches.Count && package is null; i++)
        {
            var candidate = matches[i]?.CatalogPackage;
            if (candidate is not null && string.Equals(candidate.Id, catalogId, StringComparison.OrdinalIgnoreCase))
            {
                package = candidate;
            }
        }

        if (package is null)
        {
            return null;
        }

        var version = package.DefaultInstallVersion;
        var metadata = version?.GetCatalogPackageMetadata();
        return new CatalogDetails(
            CatalogId: package.Id,
            Name: package.Name ?? string.Empty,
            Publisher: metadata?.Publisher ?? version?.Publisher,
            Description: metadata?.Description,
            Homepage: metadata?.PackageUrl ?? metadata?.PublisherUrl,
            License: metadata?.License,
            LatestVersion: version?.Version,
            Icons: MapIcons(metadata?.Icons));
    }

    private static CatalogApp MapPackage(CatalogPackage package, bool includeAvailableVersion)
    {
        var installed = package.InstalledVersion;
        // On remote search (46.15), reading DefaultInstallVersion per row loads every manifest (~0.5-1.6 s each).
        // Only read DefaultInstallVersion for local composite list/upgrades; search/tag rows read details lazily on select.
        PackageVersionInfo? available = includeAvailableVersion ? package.DefaultInstallVersion : null;
        string? sourceName = null;
        try
        {
            var catalogId = available?.PackageCatalog?.Info?.Id ?? installed?.PackageCatalog?.Info?.Id;
            if (!string.IsNullOrEmpty(catalogId))
            {
                sourceName = catalogId.Contains("Store", StringComparison.OrdinalIgnoreCase)
                    ? "Microsoft Store"
                    : "Microsoft catalog";
            }
        }
        catch
        {
            // Ignore catalog metadata errors on individual rows.
        }

        return new CatalogApp(
            LocalId: package.Id ?? string.Empty,
            CatalogId: includeAvailableVersion ? (available is null ? null : package.Id) : package.Id,
            Name: package.Name ?? string.Empty,
            Publisher: installed?.Publisher ?? available?.Publisher,
            InstalledVersion: installed?.Version,
            AvailableVersion: available?.Version,
            IsUpdateAvailable: package.IsUpdateAvailable,
            ProductCodes: Strings(installed?.ProductCodes),
            FamilyNames: Strings(installed?.PackageFamilyNames),
            SourceName: sourceName);
    }

    private static IReadOnlyList<CatalogIcon> MapIcons(IReadOnlyList<Icon>? icons)
    {
        var rows = new List<CatalogIcon>();
        if (icons is null)
        {
            return rows;
        }

        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            if (!string.IsNullOrWhiteSpace(icon?.Url))
            {
                var hash = icon.Sha256 is { Length: > 0 } bytes ? Convert.ToHexString(bytes) : null;
                rows.Add(new CatalogIcon(icon.Url, hash, icon.FileType.ToString()));
            }
        }

        return rows;
    }

    private static IReadOnlyList<string> Strings(IReadOnlyList<string>? values)
    {
        var rows = new List<string>();
        if (values is null)
        {
            return rows;
        }

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (!string.IsNullOrWhiteSpace(value))
            {
                rows.Add(value);
            }
        }

        return rows;
    }

    private static int ClampLimit(int limit) => Math.Clamp(limit, 1, MaxResultLimit);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    // Runs one COM call at a time, under the gate.
    // A COM failure, a timeout, or a bad status becomes a WingetReaderException.
    // A cancel by the caller (or by Dispose) rethrows OperationCanceledException.
    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await work(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            throw new WingetReaderException("WinGet COM read did not finish in 15 seconds.", ex);
        }
        catch (WingetReaderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new WingetReaderException("WinGet COM read failed.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }
}
