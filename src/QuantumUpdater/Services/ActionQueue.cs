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

/// <summary>What kind of work an action does.</summary>
public enum ActionKind
{
    Update,
    Install,
    Remove,
}

/// <summary>A progress report from the work. Null means the work did not report that part.</summary>
public readonly record struct ActionProgress(string? Phase, int? Percent);

/// <summary>The result of the work of one action.</summary>
public sealed record ActionOutcome(bool Success, string? Message);

/// <summary>One action in the queue. Read it on the screen thread only through events or snapshots.</summary>
public sealed class QueuedAction
{
    internal QueuedAction(string key, ActionKind kind, string title)
    {
        Id = Guid.NewGuid();
        Key = key;
        Kind = kind;
        Title = title;
    }

    public Guid Id { get; }

    /// <summary>The key that stops the same app from being queued twice while it waits or works.</summary>
    public string Key { get; }

    public ActionKind Kind { get; }

    /// <summary>The app name only.</summary>
    public string Title { get; }

    public ActionState State { get; internal set; } = ActionState.Waiting;

    /// <summary>Phase text from the work, for example "Downloading". Null unless the work reported one.</summary>
    public string? Phase { get; internal set; }

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
    private readonly List<QueuedAction> _batch = new();
    private readonly List<Entry> _pending = new();
    private bool _running;

    /// <param name="post">Runs an event on the screen thread. Tests pass <c>a =&gt; a()</c>.</param>
    public ActionQueue(Action<Action> post)
    {
        _post = post ?? throw new ArgumentNullException(nameof(post));
    }

    /// <summary>Raised on every state, phase or percent change. Runs through the post function.</summary>
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

    /// <summary>
    /// Snapshot of the actions added since the queue was last idle.
    /// The queue is idle when no action is Waiting or Working.
    /// </summary>
    public IReadOnlyList<QueuedAction> CurrentBatch
    {
        get
        {
            lock (_gate)
            {
                return _batch.ToList();
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

        return Add(
            Guid.NewGuid().ToString(),
            ActionKind.Update,
            title,
            (progress, ct) => RunLegacyAsync(work, progress, ct));
    }

    /// <summary>
    /// Adds an action to the end of the queue. If an action with the same key (ignore case)
    /// is Waiting or Working, returns that action and adds nothing.
    /// </summary>
    public QueuedAction Enqueue(
        string key,
        ActionKind kind,
        string title,
        Func<IProgress<ActionProgress>, CancellationToken, Task<ActionOutcome>> work)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(work);

        return Add(key, kind, title, work);
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

    private static async Task<ActionOutcome> RunLegacyAsync(
        Func<IProgress<int>, CancellationToken, Task<bool>> work,
        IProgress<ActionProgress> progress,
        CancellationToken ct)
    {
        var percent = new SyncProgress<int>(v => progress.Report(new ActionProgress(null, v)));
        bool ok = await work(percent, ct).ConfigureAwait(false);
        return new ActionOutcome(ok, null);
    }

    private QueuedAction Add(
        string key,
        ActionKind kind,
        string title,
        Func<IProgress<ActionProgress>, CancellationToken, Task<ActionOutcome>> work)
    {
        lock (_gate)
        {
            var existing = _items.FirstOrDefault(i =>
                string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)
                && (i.State == ActionState.Waiting || i.State == ActionState.Working));
            if (existing is not null)
            {
                return existing;
            }

            bool idle = !_items.Any(i => i.State == ActionState.Waiting || i.State == ActionState.Working);
            if (idle)
            {
                _batch.Clear();
            }

            var item = new QueuedAction(key, kind, title);
            _items.Add(item);
            _batch.Add(item);
            _pending.Add(new Entry(item, work));
            Raise(item);

            if (!_running)
            {
                _running = true;
                _ = Task.Run(RunAsync);
            }

            return item;
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
        var progress = new SyncProgress<ActionProgress>(p => Report(item, p));
        ActionState finalState;
        string? message = null;

        try
        {
            ActionOutcome outcome = await entry.Work(progress, item.Cts.Token).ConfigureAwait(false);
            finalState = outcome.Success ? ActionState.Done : ActionState.Failed;
            if (!outcome.Success)
            {
                message = outcome.Message;
            }
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

    private void Report(QueuedAction item, ActionProgress report)
    {
        lock (_gate)
        {
            if (item.State != ActionState.Working)
            {
                return;
            }

            int? percent = report.Percent is null ? null : Math.Clamp(report.Percent.Value, 0, 100);
            if (item.Phase == report.Phase && item.Percent == percent)
            {
                return;
            }

            item.Phase = report.Phase;
            item.Percent = percent;
            Raise(item);
        }
    }

    // Call only while holding _gate, so events keep their order.
    private void Raise(QueuedAction item)
    {
        _post(() => Changed?.Invoke(item));
    }

    private sealed record Entry(
        QueuedAction Item,
        Func<IProgress<ActionProgress>, CancellationToken, Task<ActionOutcome>> Work);

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public SyncProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value)
        {
            _report(value);
        }
    }
}
