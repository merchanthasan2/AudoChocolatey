using System.Text.RegularExpressions;

namespace QuantumUpdater.Services.Winget;

/// <summary>
/// Rules for package ids. Catalog ids may be used for actions.
/// Local ids (ARP\ and MSIX\) are for display and matching only.
/// </summary>
public static class PackageId
{
    private static readonly Regex CatalogPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._+-]{0,180}\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const int LocalMaxLength = 400;
    private const char Ellipsis = '…';
    private const string LocalForbidden = "\"<>|";

    /// <summary>True when the id is a catalog id that may be used for an action.</summary>
    public static bool IsCatalogId(string? id)
    {
        return id is not null
            && CatalogPattern.IsMatch(id)
            && !IsCut(id);
    }

    /// <summary>True when the id is a local ARP or MSIX id. Use for display and matching only.</summary>
    public static bool IsLocalId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > LocalMaxLength)
        {
            return false;
        }

        if (!id.StartsWith(@"ARP\", StringComparison.OrdinalIgnoreCase)
            && !id.StartsWith(@"MSIX\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (id.IndexOfAny(LocalForbidden.ToCharArray()) >= 0)
        {
            return false;
        }

        foreach (char c in id)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when the id was cut short by a text table. Never use for an action.</summary>
    public static bool IsCut(string? id)
    {
        return id is not null
            && (id.EndsWith(Ellipsis) || id.EndsWith("...", StringComparison.Ordinal));
    }
}
