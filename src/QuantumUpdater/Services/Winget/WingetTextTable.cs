using System.Text;
using System.Text.RegularExpressions;

namespace QuantumUpdater.Services.Winget;

/// <summary>
/// One data row of a winget text table (list, upgrade or search output).
/// </summary>
/// <remarks>
/// When <see cref="IsCut"/> is true, the id or name was cut short by the console.
/// Never use a cut row for an action. Callers must also check
/// <c>PackageId.IsCatalogId</c> before any install or upgrade.
/// <see cref="IsLocal"/> rows (ARP\ and MSIX\) are for display and matching only.
/// </remarks>
public sealed record WingetRow(
    string Name,
    string Id,
    string Version,
    string? Available,
    string? Source,
    bool IsLocal,
    bool IsCut);

/// <summary>
/// Parses the fixed-width text table that winget prints.
/// Column positions come from the header line. Widths are counted in console cells,
/// so wide characters (CJK, emoji) take two cells.
/// </summary>
public static class WingetTextTable
{
    private static readonly Regex HeaderWordPattern = new(
        @"(?<!\S)(Name|Id|Version|Available|Match|Source)(?!\S)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // W10: the footer lines winget prints after a table: "N upgrades available.",
    // "N package(s) have pins that prevent upgrade ..." and
    // "N package(s) have version numbers that cannot be determined ...".
    private static readonly Regex FooterPattern = new(
        @"^\d+\s+(upgrades?\s+available|packages?(\(s\))?\s+ha(s|ve)\s+(pins?|version\s+numbers?)\b)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string NoiseChars = "-\\|/█▒▓░";

    /// <summary>
    /// Parses winget text output into rows.
    /// Footer lines, separator lines, progress lines and rows without a version are skipped.
    /// W10: a line counts as a header only when a dash separator line comes next, so an app whose
    /// DisplayName reads like a header cannot change the columns. Such a line is read as a row.
    /// </summary>
    public static IReadOnlyList<WingetRow> Parse(string output)
    {
        var rows = new List<WingetRow>();
        if (string.IsNullOrEmpty(output))
        {
            return rows;
        }

        if (output[0] == '﻿')
        {
            output = output[1..];
        }

        var lines = output.Split('\n');
        List<Column>? columns = null;

        // A header candidate waits for the next non-empty line. It becomes the header only when that line is a separator.
        string? pendingLine = null;
        List<Column>? pendingColumns = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var lastCr = line.LastIndexOf('\r');
            if (lastCr >= 0)
            {
                // Progress lines overwrite themselves with carriage returns. Keep the last text.
                line = line[(lastCr + 1)..];
            }

            var clean = line.Trim();
            if (clean.Length == 0)
            {
                continue;
            }

            if (pendingColumns is not null)
            {
                var candidate = pendingLine!;
                var candidateColumns = pendingColumns;
                pendingLine = null;
                pendingColumns = null;
                if (IsSeparator(clean))
                {
                    columns = candidateColumns;
                    continue;
                }

                // Not a header after all. Read it as an ordinary line under the current columns.
                AddRow(rows, candidate, columns);
            }

            var headerColumns = TryReadHeader(line);
            if (headerColumns is not null)
            {
                pendingLine = line;
                pendingColumns = headerColumns;
                continue;
            }

            AddRow(rows, line, columns);
        }

        if (pendingLine is not null)
        {
            AddRow(rows, pendingLine, columns);
        }

        return rows;
    }

    // Adds the line as a row when it is a data row under the current columns.
    private static void AddRow(List<WingetRow> rows, string line, List<Column>? columns)
    {
        if (columns is null)
        {
            return;
        }

        var clean = line.Trim();
        if (clean.Length == 0 || IsSeparator(clean) || IsNoise(clean) || IsFooter(clean))
        {
            return;
        }

        var row = ReadRow(line, columns);

        // W10: every real row has a version. A footer or a wrapped line has none.
        if (row.Version.Length == 0)
        {
            return;
        }

        rows.Add(row);
    }

    /// <summary>Returns the column list when the line is a header (has Name, Id and Version). Otherwise null.</summary>
    private static List<Column>? TryReadHeader(string line)
    {
        var found = new List<Column>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in HeaderWordPattern.Matches(line))
        {
            var key = match.Value;
            if (keys.Add(key))
            {
                found.Add(new Column(key, CellWidth(line, match.Index)));
            }
        }

        if (!keys.Contains("Name") || !keys.Contains("Id") || !keys.Contains("Version"))
        {
            return null;
        }

        found.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Text before the first column belongs to the first column.
        found[0] = found[0] with { Start = 0 };
        return found;
    }

    private static WingetRow ReadRow(string line, List<Column> columns)
    {
        var builders = new StringBuilder[columns.Count];
        for (var i = 0; i < builders.Length; i++)
        {
            builders[i] = new StringBuilder();
        }

        var cell = 0;
        var col = 0;
        var index = 0;
        while (index < line.Length)
        {
            int length;
            int width;
            if (char.IsHighSurrogate(line[index]) && index + 1 < line.Length && char.IsLowSurrogate(line[index + 1]))
            {
                length = 2;
                width = 2;
            }
            else
            {
                length = 1;
                width = IsWide(line[index]) ? 2 : 1;
            }

            while (col + 1 < columns.Count && columns[col + 1].Start <= cell)
            {
                col++;
            }

            builders[col].Append(line, index, length);
            cell += width;
            index += length;
        }

        string Value(string key)
        {
            var position = columns.FindIndex(c => c.Key == key);
            return position < 0 ? string.Empty : builders[position].ToString().Trim();
        }

        var id = Value("Id");
        var name = Value("Name");
        var version = Value("Version");
        var available = NullIfEmpty(Value("Available"));
        var source = NullIfEmpty(Value("Source"));

        return new WingetRow(
            name,
            id,
            version,
            available,
            source,
            PackageId.IsLocalId(id),
            PackageId.IsCut(id) || PackageId.IsCut(name));
    }

    private static bool IsSeparator(string clean)
    {
        return clean.All(c => c == '-');
    }

    private static bool IsNoise(string clean)
    {
        // Spinner and progress-bar lines contain no letters or digits.
        return clean.All(c => char.IsWhiteSpace(c) || NoiseChars.Contains(c) || c == '%' || char.IsDigit(c));
    }

    private static bool IsFooter(string clean)
    {
        return FooterPattern.IsMatch(clean)
            || clean.StartsWith("The following packages", StringComparison.Ordinal);
    }

    private static string? NullIfEmpty(string value)
    {
        return value.Length == 0 ? null : value;
    }

    /// <summary>Counts console cells in the first <paramref name="end"/> characters of the text.</summary>
    private static int CellWidth(string text, int end)
    {
        var cells = 0;
        var index = 0;
        while (index < end)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                cells += 2;
                index += 2;
                continue;
            }

            cells += IsWide(text[index]) ? 2 : 1;
            index++;
        }

        return cells;
    }

    /// <summary>True when the character takes two console cells.</summary>
    private static bool IsWide(char c)
    {
        return (c >= 'ᄀ' && c <= 'ᅟ')
            || (c >= '⺀' && c <= '꓏')
            || (c >= '가' && c <= '힣')
            || (c >= '豈' && c <= '﫿')
            || (c >= '︰' && c <= '﹏')
            || (c >= '＀' && c <= '｠')
            || (c >= '￠' && c <= '￦');
    }

    private sealed record Column(string Key, int Start);
}
