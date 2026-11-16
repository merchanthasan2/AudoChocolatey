using System.Threading;

namespace QuantumUpdater.Views.Controls;

/// <summary>
/// One icon element of a recycled row. Each Begin gives a new ticket and forgets the older ones.
/// A background icon load checks IsCurrent before it shows its image. No WinUI types, so the test project can link this file.
/// </summary>
public sealed class IconSlot
{
    private int _current;

    /// <summary>The key given to the last Begin. Null when Begin was never called.</summary>
    public string? Key { get; private set; }

    /// <summary>Starts a new icon load for the key. Returns the ticket for that load.</summary>
    public int Begin(string key)
    {
        Key = key;
        return Interlocked.Increment(ref _current);
    }

    /// <summary>True only when the ticket is the last ticket from Begin. A fresh slot has no current ticket.</summary>
    public bool IsCurrent(int ticket)
    {
        return ticket != 0 && Volatile.Read(ref _current) == ticket;
    }
}
