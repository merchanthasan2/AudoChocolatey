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

/// <summary>Runs winget list/uninstall through WingetRunner and WingetTextTable (Phase 46, F24).</summary>
public sealed class InstalledService
{
    private const int CatalogQueryTimeoutMs = 60_000;
    private static readonly TimeSpan UninstallTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Last error from list or uninstall; cleared at the start of each operation.</summary>
    public string? LastError { get; private set; }

    /// <summary>Resolved path to winget.exe, or null when not found.</summary>
    public string? ExecutablePath { get; private set; }

    public InstalledService()
    {
        ExecutablePath = WingetLocator.Find();
    }

    /// <summary>Runs <c>winget list</c> and parses the tabular output. Keeps every row, including local rows.</summary>
    public async Task<IReadOnlyList<InstalledPackage>> ListInstalledAsync(CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (ExecutablePath is null)
        {
            LastError = "winget.exe was not found.";
            return Array.Empty<InstalledPackage>();
        }

        try
        {
            var run = await new WingetRunner(ExecutablePath).RunAsync(
                ["list", "--disable-interactivity", "--accept-source-agreements"],
                TimeSpan.FromMilliseconds(CatalogQueryTimeoutMs),
                null,
                cancellationToken).ConfigureAwait(false);

            if (run.Cancelled)
            {
                LastError = "Cancelled.";
                return Array.Empty<InstalledPackage>();
            }

            if (run.TimedOut)
            {
                LastError = "winget timed out.";
                return Array.Empty<InstalledPackage>();
            }

            // Every row is kept. The same id can appear twice (for example two installed versions).
            var packages = new List<InstalledPackage>();
            foreach (var row in WingetTextTable.Parse(string.Join("\n", run.Lines)))
            {
                var name = string.IsNullOrEmpty(row.Name) ? row.Id : row.Name;
                packages.Add(new InstalledPackage(row.Id, name, row.Version, row.IsLocal, row.IsCut));
            }

            return packages;
        }
        catch (OperationCanceledException)
        {
            LastError = "Cancelled.";
            return Array.Empty<InstalledPackage>();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return Array.Empty<InstalledPackage>();
        }
    }

    /// <summary>Uninstalls one package by id (<c>winget uninstall --id … --exact</c>; the runner adds --exact).</summary>
    public async Task<WingetResult> UninstallAsync(string packageId, CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(packageId))
        {
            LastError = "Package id is required.";
            return new WingetResult(false, LastError, -1);
        }

        packageId = packageId.Trim();
        if (!PackageId.IsCatalogId(packageId))
        {
            LastError = "Package id contains invalid characters.";
            return new WingetResult(false, LastError, -1);
        }

        if (ExecutablePath is null)
        {
            LastError = "winget.exe was not found.";
            return new WingetResult(false, LastError, -1);
        }

        try
        {
            var run = await new WingetRunner(ExecutablePath).RunAsync(
                ["uninstall", "--id", packageId],
                UninstallTimeout,
                null,
                cancellationToken).ConfigureAwait(false);

            if (run.Cancelled)
            {
                LastError = "Cancelled.";
                return new WingetResult(false, LastError, -1);
            }

            if (run.TimedOut)
            {
                LastError = "winget timed out.";
                return new WingetResult(false, LastError, -1);
            }

            var output = string.Join("\n", run.Lines);
            var exitCode = run.ExitCode;
            var success = exitCode == 0;
            var message = success
                ? $"Uninstall completed for {packageId}."
                : string.IsNullOrWhiteSpace(output)
                    ? $"Uninstall failed for {packageId} (exit {exitCode})."
                    : output.Trim();

            if (!success)
            {
                LastError = message;
            }

            return new WingetResult(success, message, exitCode);
        }
        catch (OperationCanceledException)
        {
            LastError = "Cancelled.";
            return new WingetResult(false, LastError, -1);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return new WingetResult(false, ex.Message, -1);
        }
    }
}
