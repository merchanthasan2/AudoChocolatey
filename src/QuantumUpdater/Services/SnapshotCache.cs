using System.Text.Json;

namespace QuantumUpdater.Services;

/// <summary>A saved value and the UTC time it was saved.</summary>
public sealed record Snapshot<T>(T Value, DateTimeOffset SavedAt);

/// <summary>
/// Saves and loads small data files, for example the saved updates list.
/// Each file holds a schema number, the saved time and the value.
/// Never throws for file or JSON errors. No WinUI types here.
/// </summary>
public sealed class SnapshotCache
{
    private const long MaxBytes = 20 * 1024 * 1024;
    private const int MaxNameLength = 40;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <param name="folder">The folder that holds the files.</param>
    public SnapshotCache(string folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }

    /// <summary>The app cache folder: %LOCALAPPDATA%\QuantumUpdater\cache\.</summary>
    public static SnapshotCache Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuantumUpdater",
        "cache"));

    /// <summary>The folder that holds the files.</summary>
    public string Folder { get; }

    /// <summary>
    /// Saves the value to "name.json" through SafeLocalFile: a temp file with a random name, then one replace.
    /// Does nothing when the folder or a file is a junction or link, or when the JSON is over 20 MB.
    /// Throws ArgumentException when the name is not valid.
    /// </summary>
    public void Save<T>(string name, int schema, T value)
    {
        string target = FilePath(name);

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            new Envelope<T>(schema, DateTimeOffset.UtcNow, value),
            Options);
        if (bytes.Length > MaxBytes)
        {
            return;
        }

        SafeLocalFile.TryWrite(Folder, Path.GetFileName(target), bytes);
    }

    /// <summary>
    /// Loads "name.json". Returns null when the file is missing, a junction or link, over 20 MB,
    /// or has another schema (the file is kept). A bad file is deleted and gives null.
    /// Throws ArgumentException when the name is not valid.
    /// </summary>
    public Snapshot<T>? Load<T>(string name, int schema)
    {
        string path = FilePath(name);

        try
        {
            byte[]? bytes = SafeLocalFile.TryRead(Folder, Path.GetFileName(path), MaxBytes);
            if (bytes is null)
            {
                return null;
            }

            RawEnvelope? raw = ReadEnvelope(bytes);
            if (raw is null)
            {
                DeleteQuietly(path);
                return null;
            }

            if (raw.Schema != schema)
            {
                return null;
            }

            if (!TryReadValue(raw.Value, out T? value))
            {
                DeleteQuietly(path);
                return null;
            }

            return new Snapshot<T>(value!, raw.SavedAt);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static RawEnvelope? ReadEnvelope(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<RawEnvelope>(bytes, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadValue<T>(JsonElement element, out T? value)
    {
        value = default;
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        try
        {
            value = element.Deserialize<T>(Options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string FilePath(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!IsValidName(name))
        {
            throw new ArgumentException("Name must be 1 to 40 characters: a to z, 0 to 9 or hyphen.", nameof(name));
        }

        return Path.Combine(Folder, name + ".json");
    }

    // Plain character checks. A regular expression would accept a trailing newline.
    private static bool IsValidName(string name)
    {
        if (name.Length < 1 || name.Length > MaxNameLength)
        {
            return false;
        }

        foreach (char c in name)
        {
            bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    // Best effort. Never follows a junction folder.
    private void DeleteQuietly(string path)
    {
        SafeLocalFile.TryDelete(Folder, Path.GetFileName(path));
    }

    private sealed record Envelope<T>(int Schema, DateTimeOffset SavedAt, T Value);

    private sealed record RawEnvelope(int Schema, DateTimeOffset SavedAt, JsonElement Value);
}
