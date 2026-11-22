namespace QuantumUpdater.Portal;

/// <summary>
/// F6: counts tries per key in a sliding window. A key is a kind plus an address or an email.
/// Thread safe. Memory stays bounded: empty keys are dropped and the table has a size cap.
/// When the table is still full after dropping old tries, a new key is refused (fail closed);
/// existing counts are never cleared, so a flood of throwaway keys cannot reset the key under attack.
/// </summary>
public sealed class RateLimiter
{
    public const int DefaultMaxKeys = 10_000;

    private readonly TimeProvider _clock;
    private readonly int _maxKeys;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _tries = new(StringComparer.Ordinal);

    public RateLimiter(TimeProvider? clock = null)
        : this(clock, DefaultMaxKeys)
    {
    }

    /// <summary>Tests pass a small table size.</summary>
    internal RateLimiter(TimeProvider? clock, int maxKeys)
    {
        _clock = clock ?? TimeProvider.System;
        _maxKeys = maxKeys;
    }

    /// <summary>
    /// Records one try. Returns false, and records nothing, when the key already has <paramref name="limit"/> tries in the
    /// window, or when the key is new and the table is full.
    /// </summary>
    public bool TryAcquire(string key, int limit, TimeSpan window)
    {
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            if (!_tries.TryGetValue(key, out var entry))
            {
                if (_tries.Count >= _maxKeys)
                {
                    PruneAll(now);
                    if (_tries.Count >= _maxKeys)
                    {
                        return false;
                    }
                }

                entry = new Entry(window);
                _tries[key] = entry;
            }

            // A key keeps the longest window it was used with, so a shorter one never drops its tries early.
            if (window > entry.Window)
            {
                entry.Window = window;
            }

            Drop(entry, now);
            if (entry.Times.Count >= limit)
            {
                return false;
            }

            entry.Times.Enqueue(now);
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

    internal int KeyCount
    {
        get
        {
            lock (_gate)
            {
                return _tries.Count;
            }
        }
    }

    private static void Drop(Entry entry, DateTimeOffset now)
    {
        while (entry.Times.Count > 0 && entry.Times.Peek() <= now - entry.Window)
        {
            entry.Times.Dequeue();
        }
    }

    // Drops only tries that are past each key's own window. Never clears a key that still has tries.
    private void PruneAll(DateTimeOffset now)
    {
        foreach (var key in _tries.Keys.ToList())
        {
            var entry = _tries[key];
            Drop(entry, now);
            if (entry.Times.Count == 0)
            {
                _tries.Remove(key);
            }
        }
    }

    private sealed class Entry(TimeSpan window)
    {
        public TimeSpan Window { get; set; } = window;

        public Queue<DateTimeOffset> Times { get; } = new();
    }
}
