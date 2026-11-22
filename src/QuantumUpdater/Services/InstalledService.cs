using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services;

/// <summary>
/// One installed package from winget list output.
/// IsLocal rows (ARP\ and MSIX\) and IsCut rows are shown for display only.
/// </summary>
public sealed record InstalledPackage(string Id, string Name, string Version, bool IsLocal = false, bool IsCut = false)
{
    /// <summary>True only when an action such as uninstall may run on this row.</summary>
    public bool CanAct => PackageId.IsCatalogId(Id) && !IsCut;
}

/// <summary>Runs winget list/uninstall through WingetCli (Phase 46, F24, W16).</summary>
public sealed class InstalledService
{
    private static readonly TimeSpan CatalogQueryTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UninstallTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Last error from the installed list; cleared at the start of each list.</summary>
    public string? LastError { get; private set; }

    /// <summary>Path to winget.exe, or null when not found. Looked up again when App Installer moved it (W5).</summary>
    public string? ExecutablePath => WingetLocator.Find();

    /// <summary>Runs <c>winget list</c> and parses the tabular output. Keeps every row, including local rows.</summary>
    public async Task<IReadOnlyList<InstalledPackage>> ListInstalledAsync(CancellationToken cancellationToken = default)
    {
        LastError = null;
        var (packages, error) = await WingetCli.RunQueryAsync(
            ["list", "--disable-interactivity", "--accept-source-agreements"],
            CatalogQueryTimeout,
            ParseInstalled,
            cancellationToken).ConfigureAwait(false);

        LastError = error;
        return packages;
    }

    /// <summary>Uninstalls one package by id (<c>winget uninstall --id … --exact</c>; the runner adds --exact).</summary>
    public Task<ActionResult> UninstallAsync(string packageId, CancellationToken cancellationToken = default)
    {
        return WingetCli.RunActionAsync(ActionKind.Remove, "uninstall", packageId, [], UninstallTimeout, cancellationToken);
    }

    // Every row is kept. The same id can appear twice (for example two installed versions).
    private static IReadOnlyList<InstalledPackage> ParseInstalled(RunResult run)
    {
        var packages = new List<InstalledPackage>();
        foreach (var row in WingetTextTable.Parse(string.Join("\n", run.Lines)))
        {
            var name = string.IsNullOrEmpty(row.Name) ? row.Id : row.Name;
            packages.Add(new InstalledPackage(row.Id, name, row.Version, row.IsLocal, row.IsCut));
        }

        return packages;
    }
}
