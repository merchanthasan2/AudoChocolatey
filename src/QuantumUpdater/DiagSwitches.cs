using System;
using System.Collections.Generic;

namespace QuantumUpdater;

// TEMP DIAGNOSTIC 46.7.37. Reads the DESKVERVE_DIAG switches one time (comma list, ignore case).
// Remove with task 46.7.85 when the start crash is fixed.
internal static class DiagSwitches
{
    private const string VariableName = "DESKVERVE_DIAG";
    private static readonly string RawText = Environment.GetEnvironmentVariable(VariableName) ?? string.Empty;
    private static readonly HashSet<string> Names = Parse(RawText);

    /// <summary>The text of the variable as it was set. Empty when not set.</summary>
    public static string RawValue => RawText;

    /// <summary>True when the named switch is in the variable.</summary>
    public static bool Has(string name) => Names.Contains(name);

    private static HashSet<string> Parse(string text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in text.Split(','))
        {
            var name = part.Trim();
            if (name.Length > 0)
            {
                set.Add(name);
            }
        }

        return set;
    }
}
