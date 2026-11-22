namespace QuantumUpdater.Portal;

/// <summary>
/// F6: counts tries per key in a sliding window. A key is a kind plus an address or an email.
/// Thread safe. Memory stays bounded: empty keys are dropped and the table has a size cap.
/// </summary>
public sealed class RateLimiter
{
    private const int MaxKeys = 10_000;

    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _tries = new(StringComparer.Ordinal);

    public RateLimiter(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Records one try. Returns false, and records nothing, when the key already has <paramref name="limit"/> tries in the window.</summary>
    public bool TryAcquire(string key, int limit, TimeSpan window)
    {
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            if (!_tries.TryGetValue(key, out var queue))
            {
                if (_tries.Count >= MaxKeys)
                {
                    PruneAll(now, window);
                }

                queue = new Queue<DateTimeOffset>();
                _tries[key] = queue;
            }

            Drop(queue, now, window);
            if (queue.Count >= limit)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }

    /// <summary>Forgets the tries for a key, for example after a good sign-in.</summary>
    public void Reset(string key)
    {
        lock (_gate)
        {
            _tries.Remove(key);
        }
    }

    private static void Drop(Queue<DateTimeOffset> queue, DateTimeOffset now, TimeSpan window)
    {
        while (queue.Count > 0 && queue.Peek() <= now - window)
        {
            queue.Dequeue();
        }
    }

    private void PruneAll(DateTimeOffset now, TimeSpan window)
    {
        foreach (var key in _tries.Keys.ToList())
        {
            var queue = _tries[key];
            Drop(queue, now, window);
            if (queue.Count == 0)
            {
                _tries.Remove(key);
            }
        }

        // Still full: many keys are busy at once. Start over rather than grow without end.
        if (_tries.Count >= MaxKeys)
        {
            _tries.Clear();
        }
    }
}
