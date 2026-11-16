using System.Collections.Concurrent;
using System.Diagnostics;
using QuantumUpdater.Services;
using Xunit;

namespace QuantumUpdater.Tests;

public class ActionQueueTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task Three_items_never_overlap()
    {
        var queue = new ActionQueue(a => a());
        var gate = new object();
        int running = 0;
        int maxRunning = 0;

        Func<IProgress<int>, CancellationToken, Task<bool>> work = async (p, ct) =>
        {
            lock (gate)
            {
                running++;
                maxRunning = Math.Max(maxRunning, running);
            }

            await Task.Delay(30, ct);

            lock (gate)
            {
                running--;
            }

            return true;
        };

        var a = queue.Enqueue("A", work);
        var b = queue.Enqueue("B", work);
        var c = queue.Enqueue("C", work);

        await WaitIdleAsync(queue);

        Assert.Equal(1, maxRunning);
        Assert.Equal(ActionState.Done, StateOf(queue, a));
        Assert.Equal(ActionState.Done, StateOf(queue, b));
        Assert.Equal(ActionState.Done, StateOf(queue, c));
    }

    [Fact]
    public async Task Order_is_kept()
    {
        var queue = new ActionQueue(a => a());
        var runs = new ConcurrentQueue<string>();

        Func<string, Func<IProgress<int>, CancellationToken, Task<bool>>> make = name => async (p, ct) =>
        {
            runs.Enqueue(name);
            await Task.Delay(5, ct);
            return true;
        };

        queue.Enqueue("A", make("A"));
        queue.Enqueue("B", make("B"));
        queue.Enqueue("C", make("C"));

        await WaitIdleAsync(queue);

        Assert.Equal(new[] { "A", "B", "C" }, runs.ToArray());
        Assert.Equal(new[] { "A", "B", "C" }, queue.Items.Select(i => i.Title).ToArray());
    }

    [Fact]
    public async Task Cancel_removes_a_waiting_item()
    {
        var queue = new ActionQueue(a => a());
        var runs = new ConcurrentQueue<string>();

        var first = queue.Enqueue("First", async (p, ct) =>
        {
            runs.Enqueue("First");
            await Task.Delay(Hold, ct);
            return true;
        });
        await WaitUntilAsync(() => StateOf(queue, first) == ActionState.Working);

        var second = queue.Enqueue("Second", (p, ct) =>
        {
            runs.Enqueue("Second");
            return Task.FromResult(true);
        });

        queue.Cancel(second.Id);

        Assert.Equal(ActionState.Cancelled, StateOf(queue, second));

        queue.Cancel(first.Id);
        await WaitIdleAsync(queue);

        Assert.Equal(ActionState.Cancelled, StateOf(queue, first));
        Assert.Equal(ActionState.Cancelled, StateOf(queue, second));
        Assert.Equal(new[] { "First" }, runs.ToArray());
    }

    [Fact]
    public async Task Cancel_stops_the_working_item()
    {
        var queue = new ActionQueue(a => a());

        var item = queue.Enqueue("Long", async (p, ct) =>
        {
            await Task.Delay(Hold, ct);
            return true;
        });
        await WaitUntilAsync(() => StateOf(queue, item) == ActionState.Working);

        var watch = Stopwatch.StartNew();
        queue.Cancel(item.Id);
        await WaitIdleAsync(queue);
        watch.Stop();

        Assert.Equal(ActionState.Cancelled, StateOf(queue, item));
        Assert.True(watch.Elapsed < Hold, "The working item ran to the end instead of stopping.");
    }

    [Fact]
    public async Task Failed_and_throwing_items_do_not_stop_the_next_one()
    {
        var queue = new ActionQueue(a => a());

        var returnsFalse = queue.Enqueue("Returns false", (p, ct) => Task.FromResult(false));
        var throws = queue.Enqueue("Throws", async (p, ct) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        });
        var next = queue.Enqueue("Next", (p, ct) => Task.FromResult(true));
        var last = queue.Enqueue("Last", (p, ct) => Task.FromResult(true));

        await WaitIdleAsync(queue);

        Assert.Equal(ActionState.Failed, StateOf(queue, returnsFalse));
        Assert.Equal(ActionState.Failed, StateOf(queue, throws));
        Assert.Equal("boom", queue.Items.Single(i => i.Id == throws.Id).Message);
        Assert.Equal(ActionState.Done, StateOf(queue, next));
        Assert.Equal(ActionState.Done, StateOf(queue, last));
    }

    [Fact]
    public async Task Percent_updates_arrive_through_Changed()
    {
        var queue = new ActionQueue(a => a());
        var seen = new ConcurrentQueue<(Guid Id, ActionState State, int? Percent)>();
        queue.Changed += a => seen.Enqueue((a.Id, a.State, a.Percent));

        var item = queue.Enqueue("Progress", (p, ct) =>
        {
            p.Report(10);
            p.Report(50);
            p.Report(100);
            return Task.FromResult(true);
        });

        await WaitIdleAsync(queue);

        var events = seen.Where(e => e.Id == item.Id).ToArray();
        Assert.Equal(ActionState.Waiting, events.First().State);
        Assert.Null(events.First().Percent);
        var reported = events.Where(e => e.State == ActionState.Working && e.Percent is not null).ToArray();
        Assert.Equal(new int?[] { 10, 50, 100 }, reported.Select(e => e.Percent).ToArray());
        Assert.Equal(ActionState.Done, events.Last().State);
    }

    private static ActionState StateOf(ActionQueue queue, QueuedAction item)
    {
        return queue.Items.Single(i => i.Id == item.Id).State;
    }

    private static Task WaitIdleAsync(ActionQueue queue)
    {
        return WaitUntilAsync(() => !queue.IsBusy);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met within 5 seconds.");
            }

            await Task.Delay(10);
        }
    }
}
