using System.Collections.Generic;

namespace QuantumUpdater.Views.Controls;

/// <summary>Pure rules for app letter tiles. No WinUI types, so the test project can link this file.</summary>
public static class LetterTileRules
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>Tile colours as #RRGGBB text.</summary>
    public static IReadOnlyList<string> PaletteHex { get; } =
        new[] { "#1F6FB5", "#8A5CC2", "#2E8B57", "#C0504D", "#B07D1A", "#2F7F8F", "#A0522D", "#5A5A5A" };

    /// <summary>The first letter or digit of the name, in upper case. "?" when there is none.</summary>
    public static char Letter(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return '?';
        }

        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                return char.ToUpperInvariant(c);
            }
        }

        return '?';
    }

    /// <summary>
    /// Palette index for the name: FNV-1a 32-bit hash of the upper-case name, modulo the palette size.
    /// Stable between runs. Do not use string.GetHashCode, it changes at each start.
    /// </summary>
    public static int ColourIndex(string? name)
    {
        var text = (name ?? "").ToUpperInvariant();
        uint hash = FnvOffsetBasis;
        unchecked
        {
            foreach (var c in text)
            {
                hash ^= c;
                hash *= FnvPrime;
            }
        }

        return (int)(hash % (uint)PaletteHex.Count);
    }
}
