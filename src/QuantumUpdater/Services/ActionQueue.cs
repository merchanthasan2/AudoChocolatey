using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuantumUpdater.Services;

/// <summary>Where a queued action is in its life.</summary>
public enum ActionState
{
    Waiting,
    Working,
    Done,
    Failed,
    Cancelled,
}

/// <summary>One action in the queue. Read it on the screen thread only through events or snapshots.</summary>
public sealed class QueuedAction
{
    internal QueuedAction(string title)
    {
        Id = Guid.NewGuid();
        Title = title;
    }

    public Guid Id { get; }

    public string Title { get; }

    public ActionState State { get; internal set; } = ActionState.Waiting;

    /// <summary>Null unless the work reported a percent.</summary>
    public int? Percent { get; internal set; }

    /// <summary>Error text for a failed action. Null otherwise.</summary>
    public string? Message { get; internal set; }

    internal CancellationTokenSource Cts { get; } = new();
}

/// <summary>
/// First in, first out queue. One action runs at a time.
/// No WinUI types here. The app passes a function that moves work onto the screen thread.
/// </summary>
public sealed class ActionQueue
{
    private readonly object _gate = new();
    private readonly Action<Action> _post;
    private readonly List<QueuedAction> _items = new();
    private readonly List<Entry> _pending = new();
    private bool _running;

    /// <param name="post">Runs an event on the screen thread. Tests pass <c>a =&gt; a()</c>.</param>
    public ActionQueue(Action<Action> post)
    {
        _post = post ?? throw new ArgumentNullException(nameof(post));
    }

    /// <summary>Raised on every state or percent change. Runs through the post function.</summary>
    public event Action<QueuedAction>? Changed;

    /// <summary>Snapshot of every action in this queue, in the order they were added.</summary>
    public IReadOnlyList<QueuedAction> Items
    {
        get
        {
            lock (_gate)
            {
                return _items.ToList();
            }
        }
    }

    /// <summary>True while the runner is working through the queue.</summary>
    public bool IsBusy
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    /// <summary>Adds an action to the end of the queue. Returns it at once.</summary>
    public QueuedAction Enqueue(string title, Func<IProgress<int>, CancellationToken, Task<bool>> work)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(work);

        var item = new QueuedAction(title);
        lock (_gate)
        {
            _items.Add(item);
            _pending.Add(new Entry(item, work));
            Raise(item);

            if (!_running)
            {
                _running = true;
                _ = Task.Run(RunAsync);
            }
        }

        return item;
    }

    /// <summary>A waiting action is removed and set to Cancelled. A working action gets its token cancelled.</summary>
    public void Cancel(Guid id)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item is null)
            {
                return;
            }

            if (item.State == ActionState.Waiting)
            {
                _pending.RemoveAll(e => e.Item == item);
                item.State = ActionState.Cancelled;
                item.Cts.Dispose();
                Raise(item);
            }
            else if (item.State == ActionState.Working)
            {
                item.Cts.Cancel();
            }
        }
    }

    private async Task RunAsync()
    {
        while (true)
        {
            Entry entry;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    _running = false;
                    return;
                }

                entry = _pending[0];
                _pending.RemoveAt(0);
                entry.Item.State = ActionState.Working;
                Raise(entry.Item);
            }

            await ExecuteAsync(entry).ConfigureAwait(false);
        }
    }

    private async Task ExecuteAsync(Entry entry)
    {
        var item = entry.Item;
        var progress = new SyncProgress(p => Report(item, p));
        ActionState finalState;
        string? message = null;

        try
        {
            bool ok = await entry.Work(progress, item.Cts.Token).ConfigureAwait(false);
            finalState = ok ? ActionState.Done : ActionState.Failed;
        }
        catch (OperationCanceledException)
        {
            finalState = ActionState.Cancelled;
        }
        catch (Exception ex)
        {
            finalState = ActionState.Failed;
            message = ex.Message;
        }

        lock (_gate)
        {
            item.State = finalState;
            item.Message = message;
            item.Cts.Dispose();
            Raise(item);
        }
    }

    private void Report(QueuedAction item, int percent)
    {
        lock (_gate)
        {
            if (item.State != ActionState.Working)
            {
                return;
            }

            int clamped = Math.Clamp(percent, 0, 100);
            if (item.Percent == clamped)
            {
                return;
            }

            item.Percent = clamped;
            Raise(item);
        }
    }

    // Call only while holding _gate, so events keep their order.
    private void Raise(QueuedAction item)
    {
        _post(() => Changed?.Invoke(item));
    }

    private sealed record Entry(QueuedAction Item, Func<IProgress<int>, CancellationToken, Task<bool>> Work);

    private sealed class SyncProgress : IProgress<int>
    {
        private readonly Action<int> _report;

        public SyncProgress(Action<int> report)
        {
            _report = report;
        }

        public void Report(int value)
        {
            _report(value);
        }
    }
}
