using System.Text.RegularExpressions;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Inventory;

/// <summary>
/// Pure merger combining Registry uninstall entries, Store/MSIX packages, and Winget catalog rows
/// according to the section 5.2 matching and de-duplication hierarchy (46.5.15).
/// </summary>
public static class InstalledMerger
{
    private static readonly Regex ParenTagPattern = new(
        @"\s*\((x64|x86|64-bit|32-bit|r|tm)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingVersionPattern = new(
        @"\s+v?\d+(\.\d+){1,3}\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MultiSpacePattern = new(
        @"\s{2,}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Merges registry desktop apps, Store/MSIX apps, and winget entries into one deduplicated list.
    /// </summary>
    public static IReadOnlyList<AppEntry> Merge(
        IReadOnlyList<AppEntry> registryApps,
        IReadOnlyList<AppEntry> storeApps,
        IReadOnlyList<CatalogApp> wingetApps)
    {
        ArgumentNullException.ThrowIfNull(registryApps);
        ArgumentNullException.ThrowIfNull(storeApps);
        ArgumentNullException.ThrowIfNull(wingetApps);

        var result = new List<AppEntry>(registryApps.Count + storeApps.Count);
        var matchedWinget = new HashSet<int>();

        // Index winget rows for fast exact lookups.
        var byArp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byProductCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byFamily = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byNorm = new Dictionary<(string Name, string Publisher), int>();

        for (int i = 0; i < wingetApps.Count; i++)
        {
            var w = wingetApps[i];
            if (w.LocalId.StartsWith(@"ARP\", StringComparison.OrdinalIgnoreCase))
            {
                byArp.TryAdd(w.LocalId, i);
            }

            foreach (var code in w.ProductCodes)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    byProductCode.TryAdd(code.Trim(), i);
                }
            }

            foreach (var family in w.FamilyNames)
            {
                if (!string.IsNullOrWhiteSpace(family))
                {
                    byFamily.TryAdd(family.Trim(), i);
                }
            }

            if (w.LocalId.StartsWith(@"MSIX\", StringComparison.OrdinalIgnoreCase))
            {
                var fullName = w.LocalId[5..];
                var fam = ExtractFamilyName(fullName);
                if (!string.IsNullOrEmpty(fam))
                {
                    byFamily.TryAdd(fam, i);
                }
            }

            var normKey = (NormalizeName(w.Name), NormalizePublisher(w.Publisher));
            if (!string.IsNullOrEmpty(normKey.Item1))
            {
                byNorm.TryAdd(normKey, i);
            }
        }

        // 1. Merge registry rows.
        foreach (var reg in registryApps)
        {
            int matchIdx = -1;
            MatchKind matchKind = MatchKind.None;

            if (reg.RegistryRef is not null)
            {
                var arpId = BuildArpId(reg.RegistryRef);
                if (byArp.TryGetValue(arpId, out int idx))
                {
                    matchIdx = idx;
                    matchKind = MatchKind.Exact;
                }
                else if (!string.IsNullOrEmpty(reg.ProductCode) && byProductCode.TryGetValue(reg.ProductCode, out idx))
                {
                    matchIdx = idx;
                    matchKind = MatchKind.Exact;
                }
                else if (byProductCode.TryGetValue(reg.RegistryRef.KeyName, out idx))
                {
                    matchIdx = idx;
                    matchKind = MatchKind.Exact;
                }
            }

            if (matchIdx < 0)
            {
                var norm = (NormalizeName(reg.Name), NormalizePublisher(reg.Publisher));
                if (!string.IsNullOrEmpty(norm.Item1) && byNorm.TryGetValue(norm, out int idx))
                {
                    matchIdx = idx;
                    matchKind = MatchKind.Likely;
                }
            }

            if (matchIdx >= 0)
            {
                matchedWinget.Add(matchIdx);
                var w = wingetApps[matchIdx];
                var canWinget = matchKind == MatchKind.Exact && PackageId.IsCatalogId(w.CatalogId);
                var route = reg.IsProtected
                    ? UninstallRoute.None
                    : canWinget
                        ? UninstallRoute.Winget
                        : !string.IsNullOrEmpty(reg.ProductCode)
                            ? UninstallRoute.Msi
                            : UninstallRoute.OwnUninstaller;

                result.Add(reg with
                {
                    Source = canWinget ? AppSource.MicrosoftCatalog : reg.Source,
                    CatalogId = PackageId.IsCatalogId(w.CatalogId) ? w.CatalogId : null,
                    Match = matchKind,
                    Route = route,
                    UpdateVersion = w.IsUpdateAvailable ? w.AvailableVersion : null,
                });
            }
            else
            {
                var route = reg.IsProtected
                    ? UninstallRoute.None
                    : !string.IsNullOrEmpty(reg.ProductCode)
                        ? UninstallRoute.Msi
                        : !string.IsNullOrWhiteSpace(reg.QuietUninstallString) || !string.IsNullOrWhiteSpace(reg.UninstallString)
                            ? UninstallRoute.OwnUninstaller
                            : UninstallRoute.None;
                result.Add(reg with { Route = route });
            }
        }

        // 2. Merge Store/MSIX apps.
        foreach (var store in storeApps)
        {
            int matchIdx = -1;
            MatchKind matchKind = MatchKind.None;

            if (!string.IsNullOrEmpty(store.StoreFullName))
            {
                var fam = ExtractFamilyName(store.StoreFullName);
                if (!string.IsNullOrEmpty(fam) && byFamily.TryGetValue(fam, out int idx))
                {
                    matchIdx = idx;
                    matchKind = MatchKind.Exact;
                }
            }

            if (matchIdx < 0)
            {
                var norm = (NormalizeName(store.Name), NormalizePublisher(store.Publisher));
                if (!string.IsNullOrEmpty(norm.Item1) && byNorm.TryGetValue(norm, out int idx))
                {
                    matchIdx = idx;
                    matchKind = MatchKind.Likely;
                }
            }

            if (matchIdx >= 0)
            {
                matchedWinget.Add(matchIdx);
                var w = wingetApps[matchIdx];
                var canWinget = matchKind == MatchKind.Exact && PackageId.IsCatalogId(w.CatalogId);
                result.Add(store with
                {
                    CatalogId = PackageId.IsCatalogId(w.CatalogId) ? w.CatalogId : store.CatalogId,
                    Match = matchKind,
                    Route = store.IsProtected ? UninstallRoute.None : (canWinget ? UninstallRoute.Winget : UninstallRoute.Store),
                    UpdateVersion = w.IsUpdateAvailable ? w.AvailableVersion : null,
                });
            }
            else
            {
                result.Add(store with
                {
                    Route = store.IsProtected ? UninstallRoute.None : UninstallRoute.Store,
                });
            }
        }

        // 3. Winget-only portable apps (exact catalog id, not matched to any ARP or Store row).
        for (int i = 0; i < wingetApps.Count; i++)
        {
            if (matchedWinget.Contains(i))
            {
                continue;
            }

            var w = wingetApps[i];
            if (!PackageId.IsCatalogId(w.CatalogId))
            {
                continue;
            }

            result.Add(new AppEntry(
                Key: "winget:" + w.CatalogId!,
                Name: string.IsNullOrWhiteSpace(w.Name) ? w.CatalogId! : w.Name,
                Publisher: w.Publisher,
                Version: w.InstalledVersion,
                Source: AppSource.MicrosoftCatalog,
                CatalogId: w.CatalogId,
                RegistryRef: null,
                StoreFullName: null,
                Match: MatchKind.Exact,
                Route: UninstallRoute.Winget,
                IsProtected: false,
                SizeBytes: null,
                InstallDate: null,
                UpdateVersion: w.IsUpdateAvailable ? w.AvailableVersion : null,
                DisplayIcon: null,
                InstallLocation: null,
                ProductCode: w.ProductCodes.FirstOrDefault(),
                UninstallString: null,
                QuietUninstallString: null));
        }

        return result
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string BuildArpId(RegistryRef r)
    {
        string scope = r.Hive == Microsoft.Win32.RegistryHive.CurrentUser ? "User" : "Machine";
        string arch = r.View == Microsoft.Win32.RegistryView.Registry32 ? "X86" : "X64";
        return $@"ARP\{scope}\{arch}\{r.KeyName}";
    }

    internal static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var cleaned = ParenTagPattern.Replace(name, string.Empty);
        cleaned = TrailingVersionPattern.Replace(cleaned, string.Empty);
        cleaned = cleaned
            .Replace("x64", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("x86", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Replace("™", string.Empty, StringComparison.Ordinal);
        cleaned = MultiSpacePattern.Replace(cleaned, " ").Trim().ToLowerInvariant();
        return cleaned;
    }

    internal static string NormalizePublisher(string? publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher))
        {
            return string.Empty;
        }

        return publisher
            .Replace("Inc.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Corp.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Corporation", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("LLC", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static string ExtractFamilyName(string fullName)
    {
        // PackageFullName format: Name_Version_Architecture_ResourceId_PublisherId
        // PackageFamilyName format: Name_PublisherId
        var parts = fullName.Split('_');
        if (parts.Length >= 5)
        {
            return parts[0] + "_" + parts[^1];
        }

        return fullName;
    }
}
