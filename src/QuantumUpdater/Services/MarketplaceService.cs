using System.Text.RegularExpressions;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services;

/// <summary>One row from winget search output.</summary>
public sealed record MarketplacePackage(string Id, string Name, string Version);

/// <summary>Outcome of winget install from Marketplace.</summary>
public sealed record MarketplaceInstallResult(bool Success, string Message, int ExitCode);

/// <summary>winget search and install for Marketplace (Phase 44.3, moved to WingetRunner in Phase 46 F24).</summary>
public sealed class MarketplaceService
{
    private static readonly Regex SearchQueryPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9 ._+-]{0,79}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const int SearchTimeoutMs = 60_000;
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(15);

    public string? LastError { get; private set; }

    public string? ExecutablePath { get; private set; }

    public MarketplaceService()
    {
        ExecutablePath = WingetLocator.Find();
    }

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

        if (ExecutablePath is null)
        {
            LastError = "winget.exe was not found.";
            return Array.Empty<MarketplacePackage>();
        }

        try
        {
            var run = await new WingetRunner(ExecutablePath).RunAsync(
                ["search", query],
                TimeSpan.FromMilliseconds(SearchTimeoutMs),
                null,
                cancellationToken).ConfigureAwait(false);

            if (run.Cancelled)
            {
                LastError = "Cancelled.";
                return Array.Empty<MarketplacePackage>();
            }

            if (run.TimedOut)
            {
                LastError = "winget timed out.";
                return Array.Empty<MarketplacePackage>();
            }

            // Only catalog ids can be installed, so other rows are left out.
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
        catch (OperationCanceledException)
        {
            LastError = "Cancelled.";
            return Array.Empty<MarketplacePackage>();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return Array.Empty<MarketplacePackage>();
        }
    }

    public async Task<MarketplaceInstallResult> InstallAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(packageId))
        {
            LastError = "Package id is required.";
            return new MarketplaceInstallResult(false, LastError, -1);
        }

        packageId = packageId.Trim();
        if (!PackageId.IsCatalogId(packageId))
        {
            LastError = "Package id contains invalid characters.";
            return new MarketplaceInstallResult(false, LastError, -1);
        }

        if (ExecutablePath is null)
        {
            LastError = "winget.exe was not found.";
            return new MarketplaceInstallResult(false, LastError, -1);
        }

        try
        {
            var run = await new WingetRunner(ExecutablePath).RunAsync(
                ["install", "--id", packageId, "--accept-package-agreements"],
                InstallTimeout,
                null,
                cancellationToken).ConfigureAwait(false);

            if (run.Cancelled)
            {
                LastError = "Cancelled.";
                return new MarketplaceInstallResult(false, LastError, -1);
            }

            if (run.TimedOut)
            {
                LastError = "winget timed out.";
                return new MarketplaceInstallResult(false, LastError, -1);
            }

            var output = string.Join("\n", run.Lines);
            var exitCode = run.ExitCode;
            var success = exitCode == 0;
            var message = success
                ? $"Install completed for {packageId}."
                : string.IsNullOrWhiteSpace(output)
                    ? $"Install failed for {packageId} (exit {exitCode})."
                    : output.Trim();

            if (!success)
            {
                LastError = message;
            }

            return new MarketplaceInstallResult(success, message, exitCode);
        }
        catch (OperationCanceledException)
        {
            LastError = "Cancelled.";
            return new MarketplaceInstallResult(false, LastError, -1);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return new MarketplaceInstallResult(false, ex.Message, -1);
        }
    }
}
