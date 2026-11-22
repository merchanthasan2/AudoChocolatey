namespace QuantumUpdater.Services;

/// <summary>
/// The text and numbers of the activity bar, made from one batch of the queue.
/// Visible when an item is Waiting or Working. The current item is the Working item, or else the first Waiting item.
/// Detail: "1 of 3 · Downloading 45%". Position = ended items + 1. No WinUI types, so the test project can link this file.
/// </summary>
public sealed record ActivitySummary(bool Visible, string Headline, string Detail, int? Percent)
{
    private static readonly ActivitySummary Hidden = new(false, "", "", null);

    /// <summary>Makes the bar text from the batch. An empty batch or a batch with no open item is hidden.</summary>
    public static ActivitySummary From(IReadOnlyList<QueuedAction> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        QueuedAction? current = batch.FirstOrDefault(i => i.State == ActionState.Working)
            ?? batch.FirstOrDefault(i => i.State == ActionState.Waiting);
        if (current is null)
        {
            return Hidden;
        }

        int ended = batch.Count(i =>
            i.State == ActionState.Done || i.State == ActionState.Failed || i.State == ActionState.Cancelled);
        string position = $"{ended + 1} of {batch.Count}";

        string headline = $"{VerbOf(current.Kind)} {current.Title}";
        string detail = position + DetailTail(current);
        return new ActivitySummary(true, headline, detail, current.Percent);
    }

    private static string VerbOf(ActionKind kind) => kind switch
    {
        ActionKind.Update => "Updating",
        ActionKind.Install => "Installing",
        ActionKind.Remove => "Removing",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown action kind."),
    };

    private static string DetailTail(QueuedAction item)
    {
        bool hasPhase = !string.IsNullOrWhiteSpace(item.Phase);

        if (item.Percent is int percent)
        {
            string word = hasPhase ? item.Phase!.Trim() : "Working";
            return $" · {word} {percent}%";
        }

        return hasPhase ? $" · {item.Phase!.Trim()}" : " · Working";
    }
}
