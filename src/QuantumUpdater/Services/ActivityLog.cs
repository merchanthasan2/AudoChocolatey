using System.Text.Json;

namespace QuantumUpdater.Services;

public sealed record ActivityEntry(DateTimeOffset At, string Action, string PackageId, bool Success);

/// <summary>Local record of install, upgrade, and remove. No file contents.</summary>
public static class ActivityLog
{
    private static readonly string PathName = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuantumUpdater",
        "activity.json");

    public static void Add(string action, string packageId, bool success)
    {
        var items = Read().ToList();
        items.Add(new ActivityEntry(DateTimeOffset.Now, action, packageId, success));
        if (items.Count > 200)
        {
            items = items.Skip(items.Count - 200).ToList();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        File.WriteAllText(PathName, JsonSerializer.Serialize(items));
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
