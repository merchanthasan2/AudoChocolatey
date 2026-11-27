using QuantumUpdater.Models;
using QuantumUpdater.Services.Scan;

namespace QuantumUpdater.Services.Updates;

/// <summary>
/// Holds available updates and today's completed updates.
/// Loads cached results first via <see cref="SnapshotCache"/>, refreshes via COM with CLI fallback,
/// and shares a single active refresh across concurrent callers. Pure C# (no WinUI types).
/// </summary>
public sealed class UpdatesCatalog
{
    private const string CacheKey = "upgrades";
    private const int CacheSchema = 1;

    private readonly IUpdatesSource _primary;
    private readonly IUpdatesSource _fallback;
    private readonly SnapshotCache _cache;
    private readonly Action<Action> _post;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();

    private IReadOnlyList<UpdateCandidate> _candidates = Array.Empty<UpdateCandidate>();
    private List<UpdatedRecord> _updatedToday = new();
    private IReadOnlyList<UpgradeEntry> _entries = Array.Empty<UpgradeEntry>();
    private Task<IReadOnlyList<UpgradeEntry>>? _inFlight;

    public UpdatesCatalog(
        IUpdatesSource primary,
        IUpdatesSource fallback,
        SnapshotCache cache,
        Action<Action> post,
        Func<DateTimeOffset>? now = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _post = post ?? throw new ArgumentNullException(nameof(post));
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public event Action? Changed;

    public IReadOnlyList<UpgradeEntry> Entries
    {
        get { lock (_gate) return _entries; }
    }

    public DateTimeOffset? LastChecked
    {
        get { lock (_gate) return field; }
        private set { lock (_gate) field = value; }
    }

    public DateTimeOffset? SavedAt
    {
        get { lock (_gate) return field; }
        private set { lock (_gate) field = value; }
    }

    public bool IsChecking
    {
        get { lock (_gate) return field; }
        private set { lock (_gate) field = value; }
    }

    public string? Error
    {
        get { lock (_gate) return field; }
        private set { lock (_gate) field = value; }
    }

    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count(e => e.Group is UpgradeGroup.Ready or UpgradeGroup.VersionNotKnown);
            }
        }
    }

    /// <summary>Loads the last saved snapshot from disk without starting a scan.</summary>
    public bool LoadSaved()
    {
        var snap = _cache.Load<SavedUpdates>(CacheKey, CacheSchema);
        if (snap is null)
        {
            return false;
        }

        var now = _now();
        var today = now.ToLocalTime().Date;
        lock (_gate)
        {
            _candidates = snap.Value.Candidates ?? new List<UpdateCandidate>();
            _updatedToday = (snap.Value.UpdatedToday ?? new List<UpdatedRecord>())
                .Where(u => u.UpdatedAt.ToLocalTime().Date == today)
                .ToList();
            _entries = UpdatesGrouping.Build(_candidates, _updatedToday, now);
            SavedAt = snap.SavedAt;
            LastChecked ??= snap.SavedAt;
        }

        RaiseChanged();
        return true;
    }

    /// <summary>Refreshes updates via COM (falling back to CLI if COM throws). Concurrent calls join the active run.</summary>
    public Task<IReadOnlyList<UpgradeEntry>> RefreshAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<UpgradeEntry>>? running;
        lock (_gate)
        {
            if (_inFlight is not null && !_inFlight.IsCompleted)
            {
                running = _inFlight;
            }
            else
            {
                IsChecking = true;
                Error = null;
                running = RunRefreshAsync(progress, cancellationToken);
                _inFlight = running;
            }
        }

        if (running.IsCompleted)
        {
            return running;
        }

        RaiseChanged();
        return running;
    }

    /// <summary>Moves an updated package to the UpdatedToday group and persists the snapshot.</summary>
    public void MarkUpdated(string catalogId, string? version, DateTimeOffset when)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            return;
        }

        lock (_gate)
        {
            var existing = _entries.FirstOrDefault(e =>
                string.Equals(e.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase));
            var candidate = _candidates.FirstOrDefault(c =>
                string.Equals(c.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase));

            var name = existing?.Name ?? candidate?.Name ?? catalogId;
            var publisher = existing?.Publisher ?? candidate?.Publisher;
            var resolvedVersion = !string.IsNullOrWhiteSpace(version)
                ? version!
                : existing?.AvailableVersion ?? candidate?.AvailableVersion ?? "";

            var today = when.ToLocalTime().Date;
            _updatedToday.RemoveAll(r =>
                r.UpdatedAt.ToLocalTime().Date != today
                || string.Equals(r.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase));
            _updatedToday.Add(new UpdatedRecord(catalogId, name, publisher, resolvedVersion, when));

            _entries = UpdatesGrouping.Build(_candidates, _updatedToday, _now());
            SaveLocked();
        }

        RaiseChanged();
    }

    private async Task<IReadOnlyList<UpgradeEntry>> RunRefreshAsync(
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stepSink = new SyncProgress<int>(step => progress?.Report(new ScanProgress(step, 3, 0, 0)));
        try
        {
            IReadOnlyList<UpdateCandidate> candidates;
            try
            {
                candidates = await _primary.ReadAsync(stepSink, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                candidates = await _fallback.ReadAsync(stepSink, cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(new ScanProgress(3, 3, 0, 0));
            var now = _now();
            var today = now.ToLocalTime().Date;

            IReadOnlyList<UpgradeEntry> built;
            lock (_gate)
            {
                _candidates = candidates;
                _updatedToday = _updatedToday
                    .Where(u => u.UpdatedAt.ToLocalTime().Date == today)
                    .ToList();
                built = UpdatesGrouping.Build(_candidates, _updatedToday, now);
                _entries = built;
                LastChecked = now;
                SavedAt = now;
                Error = null;
                IsChecking = false;
                SaveLocked();
            }

            RaiseChanged();
            return built;
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                IsChecking = false;
            }
            RaiseChanged();
            return Entries;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                IsChecking = false;
                Error = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Could not check for updates."
                    : ex.Message;
            }
            RaiseChanged();
            return Entries;
        }
    }

    private void SaveLocked()
    {
        _cache.Save(CacheKey, CacheSchema, new SavedUpdates(_candidates.ToList(), _updatedToday.ToList()));
    }

    private void RaiseChanged()
    {
        try
        {
            _post(() => Changed?.Invoke());
        }
        catch
        {
            // Screen listener failures never break the catalog.
        }
    }

    private sealed record SavedUpdates(List<UpdateCandidate> Candidates, List<UpdatedRecord> UpdatedToday);

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;
        public SyncProgress(Action<T> report) => _report = report;
        public void Report(T value) => _report(value);
    }
}
