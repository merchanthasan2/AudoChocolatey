using System.Text.RegularExpressions;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services;

/// <summary>One row from winget search output.</summary>
public sealed record MarketplacePackage(string Id, string Name, string Version);

/// <summary>winget search and install for Marketplace (Phase 44.3, moved to WingetCli in Phase 46 F24, W16).</summary>
public sealed class MarketplaceService
{
    private static readonly Regex SearchQueryPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9 ._+-]{0,79}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(15);

    /// <summary>Last error from search; cleared at the start of each search.</summary>
    public string? LastError { get; private set; }

    /// <summary>Path to winget.exe, or null when not found. Looked up again when App Installer moved it (W5).</summary>
    public string? ExecutablePath => WingetLocator.Find();

    public async Task<IReadOnlyList<MarketplacePackage>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(query))
        {
            LastError = "Search query is required.";
            return Array.Empty<MarketplacePackage>();
        }

        query = query.Trim();
        if (query.Length > 80 || !SearchQueryPattern.IsMatch(query))
        {
            LastError = "Search query is not allowed.";
            return Array.Empty<MarketplacePackage>();
        }

        var (packages, error) = await WingetCli.RunQueryAsync(
            ["search", query],
            SearchTimeout,
            ParseSearch,
            cancellationToken).ConfigureAwait(false);

        LastError = error;
        return packages;
    }

    public Task<ActionResult> InstallAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        return WingetCli.RunActionAsync(
            ActionKind.Install,
            "install",
            packageId,
            ["--accept-package-agreements"],
            InstallTimeout,
            cancellationToken);
    }

    // Only catalog ids can be installed, so other rows are left out.
    private static IReadOnlyList<MarketplacePackage> ParseSearch(RunResult run)
    {
        var packages = new List<MarketplacePackage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in WingetTextTable.Parse(string.Join("\n", run.Lines)))
        {
            if (!PackageId.IsCatalogId(row.Id) || !seen.Add(row.Id))
            {
                continue;
            }

            var name = string.IsNullOrEmpty(row.Name) ? row.Id : row.Name;
            packages.Add(new MarketplacePackage(row.Id, name, row.Version));
        }

        return packages;
    }
}
