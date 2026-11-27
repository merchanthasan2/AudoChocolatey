using QuantumUpdater.Models;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Updates;

/// <summary>
/// Queues package updates sequentially via <see cref="ActionQueue"/> (D9, 46.7.70).
/// Runs the COM upgrade delegate first; falls back to the CLI delegate ONLY when <c>NotStarted</c> is true.
/// Pure C# (delegates injected for full unit testability).
/// </summary>
public sealed class UpdateActions
{
    public delegate Task<ActionResult> UpgradeDelegate(
        string catalogId,
        IProgress<ActionProgress>? progress,
        CancellationToken token);

    private readonly ActionQueue _queue;
    private readonly UpdatesCatalog _catalog;
    private readonly UpgradeDelegate _comUpgrade;
    private readonly UpgradeDelegate _cliUpgrade;
    private readonly Func<string, bool, Task>? _reportActivity;
    private readonly Func<DateTimeOffset> _now;

    public UpdateActions(
        ActionQueue queue,
        UpdatesCatalog catalog,
        UpgradeDelegate comUpgrade,
        UpgradeDelegate cliUpgrade,
        Func<string, bool, Task>? reportActivity = null,
        Func<DateTimeOffset>? now = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _comUpgrade = comUpgrade ?? throw new ArgumentNullException(nameof(comUpgrade));
        _cliUpgrade = cliUpgrade ?? throw new ArgumentNullException(nameof(cliUpgrade));
        _reportActivity = reportActivity;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public QueuedAction Update(UpgradeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _queue.Enqueue(
            entry.CatalogId,
            ActionKind.Update,
            entry.Name,
            (progress, token) => ExecuteAsync(entry, progress, token));
    }

    public IReadOnlyList<QueuedAction> UpdateMany(IEnumerable<UpgradeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var queued = new List<QueuedAction>();
        foreach (var entry in entries)
        {
            if (entry is not null)
            {
                queued.Add(Update(entry));
            }
        }

        return queued;
    }

    private async Task<ActionOutcome> ExecuteAsync(
        UpgradeEntry entry,
        IProgress<ActionProgress> progress,
        CancellationToken token)
    {
        var result = await _comUpgrade(entry.CatalogId, progress, token).ConfigureAwait(false);
        if (result.NotStarted && !token.IsCancellationRequested)
        {
            result = await _cliUpgrade(entry.CatalogId, progress, token).ConfigureAwait(false);
        }

        if (result.Success)
        {
            _catalog.MarkUpdated(entry.CatalogId, entry.AvailableVersion, _now());
        }

        if (_reportActivity is not null && !token.IsCancellationRequested)
        {
            try
            {
                await _reportActivity(entry.CatalogId, result.Success).ConfigureAwait(false);
            }
            catch
            {
                // Activity report failures never fail a completed update.
            }
        }

        return new ActionOutcome(result.Success, result.Text, result.StillRunning);
    }
}
