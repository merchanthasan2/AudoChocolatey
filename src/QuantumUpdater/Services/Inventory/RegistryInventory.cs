using System.Globalization;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using QuantumUpdater.Models;

namespace QuantumUpdater.Services.Inventory;

/// <summary>
/// Reads installed desktop apps from the Windows uninstall keys (section 5.2).
/// It only reads. It never writes to the registry.
/// </summary>
public static class RegistryInventory
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly string[] ValueNames =
    {
        "DisplayName",
        "DisplayVersion",
        "Publisher",
        "SystemComponent",
        "ParentKeyName",
        "ReleaseType",
        "NoRemove",
        "DisplayIcon",
        "InstallLocation",
        "UninstallString",
        "QuietUninstallString",
        "EstimatedSize",
        "InstallDate",
        "WindowsInstaller",
    };

    private static readonly string[] SkippedReleaseTypes = { "Update", "Hotfix", "Security Update" };

    private static readonly string[] SkippedNamePrefixes = { "Update for", "Security Update for", "Hotfix for" };

    private static readonly Regex KbName = new(@"^KB[0-9]+$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads the four views: machine and user, each in 64-bit and 32-bit.
    /// One bad key or view is skipped. The rest still load.
    /// </summary>
    public static IReadOnlyList<AppEntry> Read()
    {
        var rows = new List<AppEntry>();
        ReadView(RegistryHive.LocalMachine, RegistryView.Registry64, rows);
        ReadView(RegistryHive.LocalMachine, RegistryView.Registry32, rows);
        ReadView(RegistryHive.CurrentUser, RegistryView.Registry64, rows);
        ReadView(RegistryHive.CurrentUser, RegistryView.Registry32, rows);
        return RemoveDuplicates(rows);
    }

    /// <summary>Applies the filter and the mapping to a dictionary of value names and values.</summary>
    /// <returns>The row, or null when the filter skips the entry.</returns>
    internal static AppEntry? MapEntry(RegistryRef reference, IReadOnlyDictionary<string, object?> values)
    {
        var name = ReadString(values, "DisplayName");
        if (name is null)
        {
            return null;
        }

        if (ReadInt(values, "SystemComponent") == 1)
        {
            return null;
        }

        if (ReadString(values, "ParentKeyName") is not null)
        {
            return null;
        }

        var releaseType = ReadString(values, "ReleaseType");
        if (releaseType is not null && Array.Exists(SkippedReleaseTypes, t => string.Equals(t, releaseType, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (IsUpdateName(name))
        {
            return null;
        }

        string? productCode = null;
        if (IsProductCode(reference.KeyName) && ReadInt(values, "WindowsInstaller") == 1)
        {
            productCode = reference.KeyName;
        }

        return new AppEntry(
            Key: $"registry:{reference.Hive}:{reference.View}:{reference.KeyName}",
            Name: name,
            Publisher: ReadString(values, "Publisher"),
            Version: ReadString(values, "DisplayVersion"),
            Source: AppSource.InstalledDirectly,
            CatalogId: null,
            RegistryRef: reference,
            StoreFullName: null,
            Match: MatchKind.None,
            Route: UninstallRoute.None,
            IsProtected: ReadInt(values, "NoRemove") == 1,
            SizeBytes: ReadSizeBytes(values),
            InstallDate: ReadInstallDate(values),
            UpdateVersion: null,
            DisplayIcon: ReadString(values, "DisplayIcon"),
            InstallLocation: ReadString(values, "InstallLocation"),
            ProductCode: productCode,
            UninstallString: ReadString(values, "UninstallString"),
            QuietUninstallString: ReadString(values, "QuietUninstallString"));
    }

    /// <summary>Keeps the first row of each DisplayName, DisplayVersion and Publisher group.</summary>
    internal static IReadOnlyList<AppEntry> RemoveDuplicates(IEnumerable<AppEntry> rows)
    {
        var seen = new HashSet<(string Name, string Version, string Publisher)>();
        var result = new List<AppEntry>();
        foreach (var row in rows)
        {
            var identity = (Normalize(row.Name), Normalize(row.Version), Normalize(row.Publisher));
            if (seen.Add(identity))
            {
                result.Add(row);
            }
        }

        return result;
    }

    /// <summary>True when the key name has the form {GUID}. Windows Installer uses this form.</summary>
    internal static bool IsProductCode(string keyName)
    {
        return Guid.TryParseExact(keyName, "B", out _);
    }

    private static void ReadView(RegistryHive hive, RegistryView view, List<AppEntry> rows)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(UninstallPath, writable: false);
            if (uninstall is null)
            {
                return;
            }

            foreach (var keyName in uninstall.GetSubKeyNames())
            {
                var entry = ReadKey(uninstall, new RegistryRef(hive, view, keyName));
                if (entry is not null)
                {
                    rows.Add(entry);
                }
            }
        }
        catch (Exception ex) when (IsRegistryError(ex))
        {
            // This view cannot be read. The other views still load.
        }
    }

    private static AppEntry? ReadKey(RegistryKey uninstall, RegistryRef reference)
    {
        try
        {
            using var key = uninstall.OpenSubKey(reference.KeyName, writable: false);
            if (key is null)
            {
                return null;
            }

            return MapEntry(reference, ReadValues(key));
        }
        catch (Exception ex) when (IsRegistryError(ex))
        {
            // This key cannot be read. The scan goes on with the next key.
            return null;
        }
    }

    private static IReadOnlyDictionary<string, object?> ReadValues(RegistryKey key)
    {
        // Registry value names are not case sensitive.
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in ValueNames)
        {
            // Do not expand %VARIABLES%. Keep the raw uninstall string.
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is not null)
            {
                values[name] = value;
            }
        }

        return values;
    }

    private static bool IsRegistryError(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException;
    }

    private static bool IsUpdateName(string name)
    {
        if (KbName.IsMatch(name))
        {
            return true;
        }

        foreach (var prefix in SkippedNamePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> values, string name)
    {
        if (!values.TryGetValue(name, out var raw) || raw is not string text)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> values, string name)
    {
        if (!values.TryGetValue(name, out var raw))
        {
            return null;
        }

        switch (raw)
        {
            case int number:
                return number;
            case long number when number >= int.MinValue && number <= int.MaxValue:
                return (int)number;
            case string text when int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                return parsed;
            default:
                return null;
        }
    }

    /// <summary>EstimatedSize is in kilobytes in the registry. This returns bytes.</summary>
    private static long? ReadSizeBytes(IReadOnlyDictionary<string, object?> values)
    {
        var kilobytes = ReadInt(values, "EstimatedSize");
        if (kilobytes is not > 0)
        {
            return null;
        }

        return (long)kilobytes.Value * 1024L;
    }

    /// <summary>InstallDate is text in the form yyyyMMdd.</summary>
    private static DateTime? ReadInstallDate(IReadOnlyDictionary<string, object?> values)
    {
        var text = ReadString(values, "InstallDate");
        if (text is null)
        {
            return null;
        }

        if (DateTime.TryParseExact(text.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return null;
    }

    private static string Normalize(string? text)
    {
        return (text ?? string.Empty).Trim().ToLowerInvariant();
    }
}
