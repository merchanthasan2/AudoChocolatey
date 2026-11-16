using System.Collections.Generic;
using System.Diagnostics;
using QuantumUpdater.Services;
using Xunit;

namespace QuantumUpdater.Tests;

public class ActivitySummaryTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Update_with_percent_and_phase_gives_the_bar_text()
    {
        var queue = new ActionQueue(a => a());
        var gates = new List<TaskCompletionSource<ActionOutcome>>();
        var first = Add(queue, gates, ActionKind.Update, "Mozilla Firefox", new ActionProgress("Downloading", 45));
        Add(queue, gates, ActionKind.Update, "Visual Studio Code");
        Add(queue, gates, ActionKind.Update, "7-Zip");

        await WaitUntilAsync(() => first.Percent == 45);
        var summary = ActivitySummary.From(queue.CurrentBatch);

        Assert.True(summary.Visible);
        Assert.Equal("Updating Mozilla Firefox", summary.Headline);
        Assert.Equal("1 of 3 · Downloading 45%", summary.Detail);
        Assert.Equal(45, summary.Percent);
        ReleaseAll(gates);
    }

    [Fact]
    public async Task Install_with_phase_and_no_percent_gives_the_phase_only()
    {
        var queue = new ActionQueue(a => a());
        var gates = new List<TaskCompletionSource<ActionOutcome>>();
        var first = Add(queue, gates, ActionKind.Install, "Zoom Workplace", new ActionProgress("Installing", null));
        Add(queue, gates, ActionKind.Update, "7-Zip");
        Add(queue, gates, ActionKind.Update, "Notion");

        await WaitUntilAsync(() => first.Phase == "Installing");
        var summary = ActivitySummary.From(queue.CurrentBatch);

        Assert.True(summary.Visible);
        Assert.Equal("Installing Zoom Workplace", summary.Headline);
        Assert.Equal("1 of 3 · Installing", summary.Detail);
        Assert.Null(summary.Percent);
        ReleaseAll(gates);
    }

    [Fact]
    public async Task Remove_with_no_phase_and_no_percent_gives_Working()
    {
        var queue = new ActionQueue(a => a());
        var gates = new List<TaskCompletionSource<ActionOutcome>>();
        var first = Add(queue, gates, ActionKind.Remove, "Canon IJ Scan Utility");
        Add(queue, gates, ActionKind.Update, "7-Zip");
        Add(queue, gates, ActionKind.Update, "Notion");

        await WaitUntilAsync(() => first.State == ActionState.Working);
        var summary = ActivitySummary.From(queue.CurrentBatch);

        Assert.True(summary.Visible);
        Assert.Equal("Removing Canon IJ Scan Utility", summary.Headline);
        Assert.Equal("1 of 3 · Working", summary.Detail);
        Assert.Null(summary.Percent);
        ReleaseAll(gates);
    }

    [Fact]
    public async Task Percent_with_no_phase_gives_Working_and_the_percent()
    {
        var queue = new ActionQueue(a => a());
        var gates = new List<TaskCompletionSource<ActionOutcome>>();
        var first = Add(queue, gates, ActionKind.Update, "7-Zip", new ActionProgress(null, 45));
        Add(queue, gates, ActionKind.Update, "Notion");
        Add(queue, gates, ActionKind.Update, "Spotify");

        await WaitUntilAsync(() => first.Percent == 45);
        var summary = ActivitySummary.From(queue.CurrentBatch);

        Assert.Equal("Updating 7-Zip", summary.Headline);
        Assert.Equal("1 of 3 · Working 45%", summary.Detail);
        Assert.Equal(45, summary.Percent);
        ReleaseAll(gates);
    }

    [Fact]
    public async Task Position_counts_the_ended_items()
    {
        var queue = new ActionQueue(a => a());
        var gates = new List<TaskCompletionSource<ActionOutcome>>();
        var first = Add(queue, gates, ActionKind.Update, "First");
        var second = Add(queue, gates, ActionKind.Update, "Second");
        Add(queue, gates, ActionKind.Update, "Third");

        await WaitUntilAsync(() => first.State == ActionState.Working);
        gates[0].SetResult(new ActionOutcome(true, null));
        await WaitUntilAsync(() => second.State == ActionState.Working);
        var summary = ActivitySummary.From(queue.CurrentBatch);

        Assert.Equal("Updating Second", summary.Headline);
        Assert.Equal("2 of 3 · Working", summary.Detail);
        ReleaseAll(gates);
    }

    [Fact]
    public async Task Failed_and_cancelled_items_count_as_ended()
    {
        var queue = new ActionQueue(a => a());
        var gates = new List<TaskCompletionSource<ActionOutcome>>();
        var first = Add(queue, gates, ActionKind.Update, "First");
        var second = Add(queue, gates, ActionKind.Update, "Second");
        var third = Add(queue, gates, ActionKind.Update, "Third");
        Add(queue, gates, ActionKind.Update, "Fourth");

        await WaitUntilAsync(() => first.State == ActionState.Working);
        gates[0].SetResult(new ActionOutcome(false, "boom"));
        await WaitUntilAsync(() => second.State == ActionState.Working);
        queue.Cancel(third.Id);
        var summary = ActivitySummary.From(queue.CurrentBatch);

        Assert.Equal(ActionState.Failed, first.State);
        Assert.Equal(ActionState.Cancelled, third.State);
        Assert.Equal("Updating Second", summary.Headline);
        Assert.Equal("3 of 4 · Working", summary.Detail);
        ReleaseAll(gates);
    }

    [Fact]
    public async Task Not_visible_when_every_item_has_ended()
    {
        var queue = new ActionQueue(a => a());
        var gates = new List<TaskCompletionSource<ActionOutcome>>();
        var only = Add(queue, gates, ActionKind.Update, "Only");

        await WaitUntilAsync(() => only.State == ActionState.Working);
        gates[0].SetResult(new ActionOutcome(true, null));
        await WaitUntilAsync(() => only.State == ActionState.Done);
        var summary = ActivitySummary.From(queue.CurrentBatch);

        Assert.False(summary.Visible);
        Assert.Equal("", summary.Headline);
        Assert.Equal("", summary.Detail);
        Assert.Null(summary.Percent);
    }

    [Fact]
    public void Empty_batch_is_not_visible()
    {
        var summary = ActivitySummary.From(Array.Empty<QueuedAction>());

        Assert.False(summary.Visible);
        Assert.Equal("", summary.Headline);
        Assert.Equal("", summary.Detail);
        Assert.Null(summary.Percent);
    }

    /// <summary>Adds one action whose work waits for a gate the test releases. Optionally reports progress first.</summary>
    private static QueuedAction Add(
        ActionQueue queue,
        List<TaskCompletionSource<ActionOutcome>> gates,
        ActionKind kind,
        string title,
        ActionProgress? report = null)
    {
        var gate = new TaskCompletionSource<ActionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        gates.Add(gate);

        return queue.Enqueue(title, kind, title, (progress, ct) =>
        {
            if (report is ActionProgress value)
            {
                progress.Report(value);
            }

            return gate.Task;
        });
    }

    private static void ReleaseAll(List<TaskCompletionSource<ActionOutcome>> gates)
    {
        foreach (var gate in gates)
        {
            gate.TrySetResult(new ActionOutcome(true, null));
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > WaitLimit)
            {
                throw new TimeoutException("The queue did not reach the expected state in time.");
            }

            await Task.Delay(5);
        }
    }
}
