namespace QuantumUpdater.Services.Winget;

/// <summary>
/// Path rules for winget.exe. No Windows Runtime types here, so the unit tests can link this file.
/// </summary>
internal static class WingetPathRules
{
    /// <summary>
    /// True when the full path of <paramref name="path"/> is inside <paramref name="root"/>.
    /// Relative paths, empty input and paths that fail to normalise return false.
    /// "..", and a folder whose name only starts with the root name, do not pass.
    /// </summary>
    internal static bool IsUnderRoot(string? path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        try
        {
            if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(root))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            var rootPrefix = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// W6: picks the highest version among the trusted candidates only.
    /// An untrusted package with a higher version can never hide a trusted one.
    /// Returns default when no candidate is trusted.
    /// </summary>
    internal static T? HighestTrusted<T>(IEnumerable<T> candidates, Func<T, Version> version, Func<T, bool> isTrusted)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(isTrusted);

        return candidates
            .Where(candidate => SafeTrusted(candidate, isTrusted))
            .OrderByDescending(version)
            .FirstOrDefault();
    }

    // A trust check that throws counts as untrusted.
    private static bool SafeTrusted<T>(T candidate, Func<T, bool> isTrusted)
    {
        try
        {
            return isTrusted(candidate);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>
/// W5: a cached path that is checked again on every read. When the file is gone (for example after an
/// App Installer update moved winget.exe to a new versioned folder), it looks the path up again once.
/// Thread safe. No Windows Runtime types, so the unit tests can link this file.
/// </summary>
internal sealed class RecheckedPath
{
    private readonly Func<string?> _locate;
    private readonly Func<string?, bool> _stillValid;
    private readonly object _gate = new();
    private string? _cached;
    private bool _located;

    /// <param name="locate">Finds the path. Returns null when it is not found.</param>
    /// <param name="stillValid">True while a found path may still be used (it exists and is trusted).</param>
    internal RecheckedPath(Func<string?> locate, Func<string?, bool> stillValid)
    {
        _locate = locate ?? throw new ArgumentNullException(nameof(locate));
        _stillValid = stillValid ?? throw new ArgumentNullException(nameof(stillValid));
    }

    /// <summary>The path, or null. Looks it up again when the cached one is missing or no longer valid.</summary>
    internal string? Get()
    {
        lock (_gate)
        {
            if (_located && _cached is not null && _stillValid(_cached))
            {
                return _cached;
            }

            var found = _locate();
            _cached = found is not null && _stillValid(found) ? found : null;
            _located = true;
            return _cached;
        }
    }
}
