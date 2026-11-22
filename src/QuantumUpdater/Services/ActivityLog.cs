using System.Text.Json;

namespace QuantumUpdater.Services;

public sealed record ActivityEntry(DateTimeOffset At, string Action, string PackageId, bool Success);

/// <summary>Local record of install, upgrade, and remove. No file contents.</summary>
public static class ActivityLog
{
    private const int MaxEntries = 200;

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
            var items = Read().ToList();
            items.Add(new ActivityEntry(DateTimeOffset.Now, action, packageId, success));
            if (items.Count > MaxEntries)
            {
                items = items.Skip(items.Count - MaxEntries).ToList();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
            File.WriteAllText(PathName, JsonSerializer.Serialize(items));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The local log is a convenience. The action result is still shown and reported.
        }
    }

    public static IReadOnlyList<ActivityEntry> Read()
    {
        try
        {
            if (!File.Exists(PathName))
            {
                return Array.Empty<ActivityEntry>();
            }

            return JsonSerializer.Deserialize<List<ActivityEntry>>(File.ReadAllText(PathName))
                ?? new List<ActivityEntry>();
        }
        catch
        {
            return Array.Empty<ActivityEntry>();
        }
    }
}
