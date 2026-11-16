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
}
