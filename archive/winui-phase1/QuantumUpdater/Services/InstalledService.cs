using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace QuantumUpdater.Services;

/// <summary>One installed package from winget list output.</summary>
public sealed record InstalledPackage(string Id, string Name, string Version);

/// <summary>Runs winget list/uninstall subprocesses (Phase 44.2).</summary>
public sealed class InstalledService
{
    private static readonly Regex PackageIdPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._+-]{0,180}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const int CatalogQueryTimeoutMs = 60_000;

    /// <summary>Last error from list or uninstall; cleared at the start of each operation.</summary>
    public string? LastError { get; private set; }

    /// <summary>Resolved path to winget.exe, or null when not found.</summary>
    public string? ExecutablePath { get; private set; }

    public InstalledService()
    {
        ExecutablePath = WingetService.FindWingetExecutable();
    }

    /// <summary>Runs <c>winget list</c> and parses the tabular output.</summary>
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
            var stdout = await RunCaptureAsync(
                ExecutablePath,
                ["list", "--disable-interactivity", "--accept-source-agreements"],
                CatalogQueryTimeoutMs,
                cancellationToken).ConfigureAwait(false);

            var packages = new List<InstalledPackage>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in ParseTable(stdout))
            {
                if (seen.Add(row.Id))
                {
                    packages.Add(row);
                }
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

    /// <summary>Uninstalls one package by id when it passes the package-id allow-list.</summary>
    public async Task<WingetResult> UninstallAsync(string packageId, CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(packageId))
        {
            LastError = "Package id is required.";
            return new WingetResult(false, LastError, -1);
        }

        packageId = packageId.Trim();
        if (!PackageIdPattern.IsMatch(packageId))
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
            var (exitCode, output) = await RunCaptureWithExitAsync(
                ExecutablePath,
                ["uninstall", "--id", packageId, "--disable-interactivity", "--accept-source-agreements"],
                TimeSpan.FromMinutes(10),
                cancellationToken).ConfigureAwait(false);

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

    private static IEnumerable<InstalledPackage> ParseTable(string text)
    {
        var lines = text.Split('\n');
        var rows = new List<InstalledPackage>();
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
            var availPos = headerLine.IndexOf("Available", StringComparison.Ordinal);
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

                if (clean.StartsWith('-') || clean.Contains("upgrades available", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }

                if (clean.StartsWith("The following", StringComparison.Ordinal))
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
                string installedVer;

                if (verPos >= 0 && verPos > idPos)
                {
                    pkgId = rowLine[idPos..verPos].Trim();
                    if (availPos >= 0)
                    {
                        installedVer = rowLine[verPos..availPos].Trim();
                    }
                    else if (matchPos >= 0)
                    {
                        installedVer = rowLine[verPos..matchPos].Trim();
                    }
                    else if (srcPos >= 0 && verPos < srcPos)
                    {
                        installedVer = rowLine[verPos..srcPos].Trim();
                    }
                    else
                    {
                        installedVer = rowLine.Length > verPos ? rowLine[verPos..].Trim() : "";
                    }
                }
                else
                {
                    pkgId = rowLine[idPos..].Trim();
                    installedVer = "";
                }

                if (!string.IsNullOrEmpty(pkgId) && seenIds.Add(pkgId))
                {
                    rows.Add(new InstalledPackage(pkgId, string.IsNullOrEmpty(name) ? pkgId : name, installedVer));
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