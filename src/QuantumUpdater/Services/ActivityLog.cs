using System.Text.Json;

namespace QuantumUpdater.Services;

public sealed record ActivityEntry(DateTimeOffset At, string Action, string PackageId, bool Success);

/// <summary>
/// Local record of install, upgrade, and remove. No file contents.
/// One lock covers each read-modify-write, so two actions that end together never lose an entry.
/// Writes go through SafeLocalFile (junction-safe, temp file plus replace).
/// </summary>
public static class ActivityLog
{
    private const int MaxEntries = 200;
    private const long MaxFileBytes = 1024 * 1024;

    private static readonly object Gate = new();

    /// <summary>The log file. Tests point it at a temp folder.</summary>
    internal static string PathName { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuantumUpdater",
        "activity.json");

    /// <summary>Adds one entry and keeps the last 200. Never throws: a failed write must not fail the action.</summary>
    public static void Add(string action, string packageId, bool success)
    {
        try
        {
            lock (Gate)
            {
                var path = PathName;
                var items = ReadCore(path).ToList();
                items.Add(new ActivityEntry(DateTimeOffset.Now, action, packageId, success));
                if (items.Count > MaxEntries)
                {
                    items = items.Skip(items.Count - MaxEntries).ToList();
                }

                SafeLocalFile.TryWrite(
                    Path.GetDirectoryName(path)!,
                    Path.GetFileName(path),
                    JsonSerializer.SerializeToUtf8Bytes(items));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // The local log is a convenience. The action result is still shown and reported.
        }
    }

    public static IReadOnlyList<ActivityEntry> Read()
    {
        lock (Gate)
        {
            return ReadCore(PathName);
        }
    }

    private static IReadOnlyList<ActivityEntry> ReadCore(string path)
    {
        try
        {
            var bytes = SafeLocalFile.TryRead(Path.GetDirectoryName(path)!, Path.GetFileName(path), MaxFileBytes);
            if (bytes is null)
            {
                return Array.Empty<ActivityEntry>();
            }

            return JsonSerializer.Deserialize<List<ActivityEntry>>(bytes) ?? new List<ActivityEntry>();
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException)
        {
            return Array.Empty<ActivityEntry>();
        }
    }
}
