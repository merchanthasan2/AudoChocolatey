using System.Text.RegularExpressions;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services;

/// <summary>One upgrade candidate from winget catalog output.</summary>
public sealed record WingetPackage(string Id, string Name, string Version, string Available);

/// <summary>Outcome of a single-package winget upgrade.</summary>
public sealed record WingetResult(bool Success, string Message, int ExitCode);

/// <summary>Runs winget list/upgrade through WingetRunner and WingetTextTable (Phase 46, F24).</summary>
public sealed class WingetService
{
    private static readonly Regex ExplicitTargetingSplit = new(
        @"The following packages have[^\n]*explicit targeting[^\n]*for upgrade:\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int CatalogQueryTimeoutMs = 60_000;
    private static readonly TimeSpan UpgradeTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Last error from list or upgrade; cleared at the start of each operation.</summary>
    public string? LastError { get; private set; }

    /// <summary>Resolved path to winget.exe, or null when not found.</summary>
    public string? ExecutablePath { get; private set; }

    /// <summary>Rows skipped by the last upgrade list because they were cut, local or not catalog ids.</summary>
    public int SkippedRows { get; private set; }

    public WingetService()
    {
        ExecutablePath = FindWingetExecutable();
    }

    /// <summary>Runs <c>winget upgrade --include-unknown</c> (aligned with core/winget_engine.py).</summary>
    public async Task<IReadOnlyList<WingetPackage>> ListUpgradesAsync(CancellationToken cancellationToken = default)
    {
        LastError = null;
        SkippedRows = 0;
        if (ExecutablePath is null)
        {
            LastError = "winget.exe was not found.";
            return Array.Empty<WingetPackage>();
        }

        try
        {
            var run = await new WingetRunner(ExecutablePath).RunAsync(
                ["upgrade", "--include-unknown", "--disable-interactivity", "--accept-source-agreements"],
                TimeSpan.FromMilliseconds(CatalogQueryTimeoutMs),
                null,
                cancellationToken).ConfigureAwait(false);

            if (run.Cancelled)
            {
                LastError = "Cancelled.";
                return Array.Empty<WingetPackage>();
            }

            if (run.TimedOut)
            {
                LastError = "winget timed out.";
                return Array.Empty<WingetPackage>();
            }

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
                    SkippedRows++;
                    continue;
                }

                if (seen.Add(row.Id))
                {
                    packages.Add(ToPackage(row));
                }
            }

            return packages;
        }
        catch (OperationCanceledException)
        {
            LastError = "Cancelled.";
            return Array.Empty<WingetPackage>();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return Array.Empty<WingetPackage>();
        }
    }

    /// <summary>Upgrades one package by id (<c>winget upgrade --id … --exact</c>; the runner adds --exact).</summary>
    public async Task<WingetResult> UpgradeAsync(string packageId, CancellationToken cancellationToken = default)
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
                [
                    "upgrade", "--id", packageId, "--include-unknown", "--disable-interactivity",
                    "--silent", "--accept-package-agreements", "--accept-source-agreements",
                ],
                UpgradeTimeout,
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
                ? $"Upgrade completed for {packageId}."
                : string.IsNullOrWhiteSpace(output)
                    ? $"Upgrade failed for {packageId} (exit {exitCode})."
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

    /// <summary>Finds winget.exe through WingetLocator only. Kept for InstalledService and MarketplaceService.</summary>
    internal static string? FindWingetExecutable()
    {
        return WingetLocator.Find();
    }

    private static WingetPackage ToPackage(WingetRow row)
    {
        var name = string.IsNullOrEmpty(row.Name) ? row.Id : row.Name;
        return new WingetPackage(row.Id, name, row.Version, row.Available ?? "");
    }
}
