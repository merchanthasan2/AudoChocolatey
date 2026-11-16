using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace QuantumUpdater.Services;

/// <summary>One row from winget search output.</summary>
public sealed record MarketplacePackage(string Id, string Name, string Version);

/// <summary>Outcome of winget install from Marketplace.</summary>
public sealed record MarketplaceInstallResult(bool Success, string Message, int ExitCode);

/// <summary>winget search and install for Marketplace (Phase 44.3).</summary>
public sealed class MarketplaceService
{
    private static readonly Regex PackageIdPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._+-]{0,180}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SearchQueryPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9 ._+-]{0,79}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const int SearchTimeoutMs = 60_000;

    public string? LastError { get; private set; }

    public string? ExecutablePath { get; private set; }

    public MarketplaceService()
    {
        ExecutablePath = WingetService.FindWingetExecutable();
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
            var stdout = await RunCaptureAsync(
                ExecutablePath,
                ["search", query, "--disable-interactivity", "--accept-source-agreements"],
                SearchTimeoutMs,
                cancellationToken).ConfigureAwait(false);

            return ParseSearchTable(stdout).Where(row => PackageIdPattern.IsMatch(row.Id)).ToList();
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
        if (!PackageIdPattern.IsMatch(packageId))
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
            var (exitCode, output) = await RunCaptureWithExitAsync(
                ExecutablePath,
                [
                    "install", "--id", packageId, "--exact", "--disable-interactivity",
                    "--accept-package-agreements", "--accept-source-agreements",
                ],
                TimeSpan.FromMinutes(15),
                cancellationToken).ConfigureAwait(false);

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

    private static async Task<string> RunCaptureAsync(
        string executable,
        IReadOnlyList<string> arguments,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var (_, output) = await RunCaptureWithExitAsync(
            executable,
            arguments,
            TimeSpan.FromMilliseconds(timeoutMs),
            cancellationToken).ConfigureAwait(false);
        return output;
    }

    private static async Task<(int ExitCode, string Output)> RunCaptureWithExitAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(executable, arguments) };
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start winget.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort.
            }

            throw new TimeoutException("winget timed out.");
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var combined = new StringBuilder();
        if (!string.IsNullOrEmpty(stdout))
        {
            combined.Append(stdout);
        }

        if (!string.IsNullOrEmpty(stderr))
        {
            if (combined.Length > 0)
            {
                combined.AppendLine();
            }

            combined.Append(stderr);
        }

        return (process.ExitCode, combined.ToString());
    }

    private static ProcessStartInfo BuildStartInfo(string executable, IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        return psi;
    }

    private static IReadOnlyList<MarketplacePackage> ParseSearchTable(string text)
    {
        var lines = text.Split('\n');
        var rows = new List<MarketplacePackage>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            if (!line.Contains("Name", StringComparison.Ordinal) ||
                !line.Contains("Id", StringComparison.Ordinal) ||
                !line.Contains("Version", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            var headerLine = line;
            var idPos = headerLine.IndexOf("Id", StringComparison.Ordinal);
            var verPos = headerLine.IndexOf("Version", StringComparison.Ordinal);
            var matchPos = headerLine.IndexOf("Match", StringComparison.Ordinal);
            var srcPos = headerLine.IndexOf("Source", StringComparison.Ordinal);

            i++;
            if (i < lines.Length && lines[i].TrimStart().StartsWith('-'))
            {
                i++;
            }

            while (i < lines.Length)
            {
                var rowLine = lines[i];
                var clean = rowLine.Trim();
                if (string.IsNullOrEmpty(clean))
                {
                    i++;
                    continue;
                }

                if (rowLine.Contains("Name", StringComparison.Ordinal) &&
                    rowLine.Contains("Id", StringComparison.Ordinal) &&
                    rowLine.Contains("Version", StringComparison.Ordinal))
                {
                    break;
                }

                if (clean.StartsWith('-') ||
                    clean.Contains("packages found", StringComparison.OrdinalIgnoreCase) ||
                    clean.Contains("No package found", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }

                if (IsProgressLine(rowLine, idPos))
                {
                    i++;
                    continue;
                }

                if (rowLine.TrimEnd('\r').Length < idPos)
                {
                    i++;
                    continue;
                }

                var name = rowLine[..idPos].Trim();
                if (name.EndsWith('…'))
                {
                    name = name[..^1].TrimEnd();
                }
                else if (name.EndsWith("...", StringComparison.Ordinal))
                {
                    name = name[..^3].TrimEnd();
                }

                string pkgId;
                string version;

                if (verPos >= 0 && verPos > idPos)
                {
                    pkgId = rowLine[idPos..verPos].Trim();
                    if (matchPos >= 0 && matchPos > verPos)
                    {
                        version = rowLine[verPos..matchPos].Trim();
                    }
                    else if (srcPos >= 0 && srcPos > verPos)
                    {
                        version = rowLine[verPos..srcPos].Trim();
                    }
                    else
                    {
                        version = rowLine.Length > verPos ? rowLine[verPos..].Trim() : "";
                    }
                }
                else
                {
                    pkgId = rowLine[idPos..].Trim();
                    version = "";
                }

                if (!string.IsNullOrEmpty(pkgId) && seenIds.Add(pkgId))
                {
                    rows.Add(new MarketplacePackage(
                        pkgId,
                        string.IsNullOrEmpty(name) ? pkgId : name,
                        version));
                }

                i++;
            }
        }

        return rows;
    }

    private static bool IsProgressLine(string raw, int idColumn)
    {
        if (!raw.Contains('%'))
        {
            return false;
        }

        if (raw.TrimEnd('\r').Length < idColumn)
        {
            return true;
        }

        if (Regex.IsMatch(raw, @"[\|\\/#█▓░]+.*%"))
        {
            return true;
        }

        return Regex.IsMatch(raw.Trim(), @"^[\d.]+\s*%");
    }
}