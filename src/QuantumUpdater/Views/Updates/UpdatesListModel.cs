using QuantumUpdater.Models;
using QuantumUpdater.Services;
using QuantumUpdater.Views.Controls;

namespace QuantumUpdater.Views.Updates;

public enum UpdatesSort
{
    Name,
    Source,
}

public sealed record UpdatesViewResult(
    IReadOnlyList<ListItem> Items,
    int ReadyCount,
    int VersionNotKnownCount,
    int UpdatedTodayCount,
    int TotalActionableCount,
    int SelectedCount,
    IReadOnlyList<UpgradeEntry> AllActionable,
    IReadOnlyList<UpgradeEntry> SelectedEntries,
    string UpdateAllLabel,
    string UpdateSelectedLabel);

/// <summary>
/// Pure projection from <see cref="UpgradeEntry"/> + <see cref="ActionQueue"/> state into
/// <see cref="VirtualList"/> items and tile/button counts (46.7.75).
/// </summary>
public static class UpdatesListModel
{
    public static UpdatesViewResult Build(
        IReadOnlyList<UpgradeEntry> entries,
        IReadOnlyList<QueuedAction> queueBatch,
        bool isQueueHeld,
        string filterText,
        UpdatesSort sort,
        ISet<string> collapsedGroups,
        ISet<string> selectedKeys)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(queueBatch);
        ArgumentNullException.ThrowIfNull(collapsedGroups);
        ArgumentNullException.ThrowIfNull(selectedKeys);

        var batchByKey = new Dictionary<string, (QueuedAction Action, int WaitingRank)>(StringComparer.OrdinalIgnoreCase);
        int waitRank = 0;
        foreach (var item in queueBatch)
        {
            if (item.State == ActionState.Waiting)
            {
                waitRank++;
                batchByKey[item.Key] = (item, waitRank);
            }
            else
            {
                batchByKey[item.Key] = (item, 0);
            }
        }

        string q = (filterText ?? string.Empty).Trim();
        IEnumerable<UpgradeEntry> filtered = entries;
        if (q.Length > 0)
        {
            filtered = entries.Where(e =>
                e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || e.CatalogId.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(e.Publisher) && e.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        filtered = sort == UpdatesSort.Source
            ? filtered.OrderBy(e => e.SourceLabel ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(e => e.CatalogId, StringComparer.OrdinalIgnoreCase);

        var readyRows = new List<UpgradeEntry>();
        var unknownRows = new List<UpgradeEntry>();
        var todayRows = new List<UpgradeEntry>();

        foreach (var e in filtered)
        {
            switch (e.Group)
            {
                case UpgradeGroup.Ready:
                    readyRows.Add(e);
                    break;
                case UpgradeGroup.VersionNotKnown:
                    unknownRows.Add(e);
                    break;
                case UpgradeGroup.UpdatedToday:
                    todayRows.Add(e);
                    break;
            }
        }

        var allActionable = entries
            .Where(e => e.Group is UpgradeGroup.Ready or UpgradeGroup.VersionNotKnown)
            .ToList();
        var selectedEntries = allActionable
            .Where(e => selectedKeys.Contains(e.CatalogId))
            .ToList();

        var items = new List<ListItem>();
        AddGroup(items, "ready", "Ready to update", readyRows, batchByKey, isQueueHeld, collapsedGroups, selectedKeys);
        AddGroup(items, "unknown", "Version not known", unknownRows, batchByKey, isQueueHeld, collapsedGroups, selectedKeys);
        AddGroup(items, "today", "Updated today", todayRows, batchByKey, isQueueHeld, collapsedGroups, selectedKeys);

        int readyTotal = entries.Count(e => e.Group == UpgradeGroup.Ready);
        int unknownTotal = entries.Count(e => e.Group == UpgradeGroup.VersionNotKnown);
        int todayTotal = entries.Count(e => e.Group == UpgradeGroup.UpdatedToday);
        int actionableTotal = readyTotal + unknownTotal;

        return new UpdatesViewResult(
            Items: items,
            ReadyCount: readyTotal,
            VersionNotKnownCount: unknownTotal,
            UpdatedTodayCount: todayTotal,
            TotalActionableCount: actionableTotal,
            SelectedCount: selectedEntries.Count,
            AllActionable: allActionable,
            SelectedEntries: selectedEntries,
            UpdateAllLabel: $"Update all ({actionableTotal})",
            UpdateSelectedLabel: $"Update selected ({selectedEntries.Count})");
    }

    private static void AddGroup(
        List<ListItem> items,
        string groupId,
        string title,
        List<UpgradeEntry> rows,
        Dictionary<string, (QueuedAction Action, int WaitingRank)> batchByKey,
        bool isQueueHeld,
        ISet<string> collapsedGroups,
        ISet<string> selectedKeys)
    {
        if (rows.Count == 0)
        {
            return;
        }

        bool expanded = !collapsedGroups.Contains(groupId);
        items.Add(new GroupItem(groupId, title, rows.Count, expanded));
        if (!expanded)
        {
            return;
        }

        foreach (var entry in rows)
        {
            items.Add(new RowItem(ToRowModel(entry, batchByKey, isQueueHeld, selectedKeys)));
        }
    }

    private static AppRowModel ToRowModel(
        UpgradeEntry entry,
        Dictionary<string, (QueuedAction Action, int WaitingRank)> batchByKey,
        bool isQueueHeld,
        ISet<string> selectedKeys)
    {
        if (entry.Group == UpgradeGroup.UpdatedToday)
        {
            return new AppRowModel
            {
                Key = entry.CatalogId,
                Name = entry.Name,
                Publisher = entry.Publisher,
                FromVersion = null,
                ToVersion = null,
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Updated,
                DoneVersion = entry.UpdatedVersion,
                DoneAt = entry.UpdatedAt,
                CanSelect = false,
            };
        }

        AppRowState state = AppRowState.Ready;
        int place = 0;
        bool held = false;
        string? phase = null;
        int? percent = null;
        string? error = null;

        if (batchByKey.TryGetValue(entry.CatalogId, out var q))
        {
            switch (q.Action.State)
            {
                case ActionState.Waiting:
                    state = AppRowState.Waiting;
                    place = q.WaitingRank;
                    held = isQueueHeld;
                    break;
                case ActionState.Working:
                    state = AppRowState.Working;
                    phase = q.Action.Phase;
                    percent = q.Action.Percent;
                    break;
                case ActionState.Done:
                    state = AppRowState.Updated;
                    break;
                case ActionState.Failed:
                    state = AppRowState.Failed;
                    error = q.Action.Message;
                    break;
                case ActionState.Cancelled:
                    state = AppRowState.Ready;
                    break;
            }
        }

        bool canSelect = state is AppRowState.Ready or AppRowState.Failed;
        return new AppRowModel
        {
            Key = entry.CatalogId,
            Name = entry.Name,
            Publisher = entry.Publisher,
            FromVersion = entry.InstalledVersion,
            ToVersion = entry.AvailableVersion,
            SourceLabel = entry.SourceLabel ?? "Microsoft catalog",
            State = state,
            PlaceInLine = place,
            IsHeld = held,
            Phase = phase,
            Percent = percent,
            ErrorText = error,
            DoneVersion = entry.AvailableVersion,
            DoneAt = state == AppRowState.Updated ? DateTimeOffset.Now : null,
            CanSelect = canSelect,
            IsSelected = canSelect && selectedKeys.Contains(entry.CatalogId),
        };
    }
}
