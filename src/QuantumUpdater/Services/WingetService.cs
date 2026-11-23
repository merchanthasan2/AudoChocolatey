using System.Text.RegularExpressions;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services;

/// <summary>One upgrade candidate from winget catalog output.</summary>
public sealed record WingetPackage(string Id, string Name, string Version, string Available);

/// <summary>Runs winget list/upgrade through WingetCli (Phase 46, F24, W16).</summary>
public sealed class WingetService
{
    private static readonly Regex ExplicitTargetingSplit = new(
        @"The following packages have[^\n]*explicit targeting[^\n]*for upgrade:\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly TimeSpan CatalogQueryTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UpgradeTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Last error from the upgrade list; cleared at the start of each list.</summary>
    public string? LastError { get; private set; }

    /// <summary>Path to winget.exe, or null when not found. Looked up again when App Installer moved it (W5).</summary>
    public string? ExecutablePath => WingetLocator.Find();

    /// <summary>Rows skipped by the last upgrade list because they were cut, local or not catalog ids.</summary>
    public int SkippedRows { get; private set; }

    /// <summary>Runs <c>winget upgrade --include-unknown</c> (aligned with core/winget_engine.py).</summary>
    public async Task<IReadOnlyList<WingetPackage>> ListUpgradesAsync(CancellationToken cancellationToken = default)
    {
        LastError = null;
        SkippedRows = 0;
        int skipped = 0;
        var (packages, error) = await WingetCli.RunQueryAsync(
            ["upgrade", "--include-unknown", "--disable-interactivity", "--accept-source-agreements"],
            CatalogQueryTimeout,
            run => ParseUpgrades(run, out skipped),
            cancellationToken).ConfigureAwait(false);

        SkippedRows = skipped;
        LastError = error;
        return packages;
    }

    /// <summary>Upgrades one package by id (<c>winget upgrade --id … --exact --source winget</c>; the runner adds both).</summary>
    public Task<ActionResult> UpgradeAsync(string packageId, CancellationToken cancellationToken = default)
    {
        return WingetCli.RunActionAsync(
            ActionKind.Update,
            "upgrade",
            packageId,
            ["--include-unknown", "--disable-interactivity", "--silent", "--accept-package-agreements", "--accept-source-agreements"],
            UpgradeTimeout,
            cancellationToken);
    }

    private static IReadOnlyList<WingetPackage> ParseUpgrades(RunResult run, out int skipped)
    {
        skipped = 0;
        var stdout = string.Join("\n", run.Lines);
        var sections = ExplicitTargetingSplit.Split(stdout, 2);
        var mainText = sections[0];
        var explicitText = sections.Length > 1 ? sections[1] : "";

        var packages = new List<WingetPackage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in WingetTextTable.Parse(mainText).Concat(WingetTextTable.Parse(explicitText)))
        {
            if (row.IsCut || !PackageId.IsCatalogId(row.Id))
            {
                skipped++;
                continue;
            }

            if (seen.Add(row.Id))
            {
                var name = string.IsNullOrEmpty(row.Name) ? row.Id : row.Name;
                packages.Add(new WingetPackage(row.Id, name, row.Version, row.Available ?? ""));
            }
        }

        return packages;
    }
}
