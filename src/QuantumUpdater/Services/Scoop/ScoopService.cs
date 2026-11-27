using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Scoop;

public sealed record ScoopPackage(string Name, string Version, string? Bucket, string? Info = null);

public sealed record ScoopOutdated(string Name, string InstalledVersion, string LatestVersion);

/// <summary>
/// Manages Scoop buckets, installs, updates, uninstalls, and catalog searches.
/// Uses strict allowlists and ProcessStartInfo.ArgumentList only (never shell interpolation).
/// </summary>
public sealed class ScoopService
{
    private static readonly Regex AppNamePattern = new(
        @"^(?:[A-Za-z0-9][A-Za-z0-9._-]{0,40}/)?[A-Za-z0-9][A-Za-z0-9._-]{0,80}\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BucketNamePattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._-]{0,40}\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QueryPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9 ._+-]{0,79}\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromMinutes(10);

    private static readonly string PowerShellExe = Path.Combine(
        Environment.SystemDirectory,
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    private readonly Func<string?> _findScoop;
    private readonly Func<IReadOnlyList<string>, TimeSpan, Action<string>?, CancellationToken, Task<RunResult>> _run;

    public ScoopService()
        : this(FindScoopScript, RunPowerShellScoopAsync)
    {
    }

    internal ScoopService(
        Func<string?> findScoop,
        Func<IReadOnlyList<string>, TimeSpan, Action<string>?, CancellationToken, Task<RunResult>> run)
    {
        _findScoop = findScoop;
        _run = run;
    }

    public bool IsAvailable => _findScoop() is not null;

    public static bool IsValidAppName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && AppNamePattern.IsMatch(name.Trim());

    public static bool IsValidBucket(string? name, string? url) =>
        !string.IsNullOrWhiteSpace(name)
        && BucketNamePattern.IsMatch(name.Trim())
        && Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    public static string? FindScoopScript()
    {
        var candidates = new List<string>();
        var scoopEnv = Environment.GetEnvironmentVariable("SCOOP");
        if (!string.IsNullOrWhiteSpace(scoopEnv))
        {
            candidates.Add(Path.Combine(scoopEnv.Trim(), "shims", "scoop.ps1"));
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            candidates.Add(Path.Combine(profile, "scoop", "shims", "scoop.ps1"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<IReadOnlyList<ScoopPackage>> ListInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return Array.Empty<ScoopPackage>();
        }

        var res = await _run(["list"], QueryTimeout, null, cancellationToken).ConfigureAwait(false);
        return ParseList(res.Lines);
    }

    public async Task<IReadOnlyList<ScoopPackage>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ScoopPackage>();
        }

        var trimmed = query.Trim();
        if (!QueryPattern.IsMatch(trimmed))
        {
            return Array.Empty<ScoopPackage>();
        }

        var res = await _run(["search", trimmed], QueryTimeout, null, cancellationToken).ConfigureAwait(false);
        return ParseSearch(res.Lines);
    }

    public async Task<IReadOnlyList<ScoopOutdated>> ListOutdatedAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return Array.Empty<ScoopOutdated>();
        }

        var res = await _run(["status"], QueryTimeout, null, cancellationToken).ConfigureAwait(false);
        return ParseStatus(res.Lines);
    }

    public async Task<ActionResult> AddBucketAsync(
        string bucketName,
        string bucketUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidBucket(bucketName, bucketUrl))
        {
            return RunOutcome.NotStarted("Invalid Scoop bucket name or HTTPS URL.");
        }

        if (!IsAvailable)
        {
            return RunOutcome.NotStarted("Scoop is not installed on this PC.");
        }

        var res = await _run(["bucket", "add", bucketName.Trim(), bucketUrl.Trim()], QueryTimeout, null, cancellationToken)
            .ConfigureAwait(false);

        string combined = string.Join("\n", res.Lines.Concat(res.Errors));
        if (res.ExitCode == 0 || combined.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return new ActionResult(true, false, false, $"Bucket '{bucketName}' ready.", null, null);
        }

        return new ActionResult(false, false, false, $"Could not add Scoop bucket '{bucketName}'.", FirstError(res), null);
    }

    public async Task<ActionResult> InstallAsync(
        string appName,
        string? bucketName = null,
        string? bucketUrl = null,
        IProgress<ActionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidAppName(appName))
        {
            return RunOutcome.NotStarted("Invalid Scoop package name.");
        }

        if (!IsAvailable)
        {
            return RunOutcome.NotStarted("Scoop is not installed on this PC. Install Scoop first from scoop.sh.");
        }

        if (!string.IsNullOrWhiteSpace(bucketName) && !string.IsNullOrWhiteSpace(bucketUrl))
        {
            progress?.Report(new ActionProgress($"Adding bucket {bucketName.Trim()}", null));
            var bucketRes = await AddBucketAsync(bucketName, bucketUrl, cancellationToken).ConfigureAwait(false);
            if (!bucketRes.Success)
            {
                return bucketRes;
            }
        }

        progress?.Report(new ActionProgress("Installing via Scoop", null));
        var res = await _run(["install", appName.Trim()], ActionTimeout, null, cancellationToken).ConfigureAwait(false);
        if (res.Cancelled)
        {
            return new ActionResult(false, false, false, "The install was cancelled.", null, null);
        }

        if (res.ExitCode == 0)
        {
            return new ActionResult(true, false, false, "Installed via Scoop.", null, null);
        }

        return new ActionResult(false, false, false, "Scoop install failed.", FirstError(res), null);
    }

    public async Task<ActionResult> UpgradeAsync(
        string appName,
        IProgress<ActionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidAppName(appName))
        {
            return RunOutcome.NotStarted("Invalid Scoop package name.");
        }

        if (!IsAvailable)
        {
            return RunOutcome.NotStarted("Scoop is not installed on this PC.");
        }

        progress?.Report(new ActionProgress("Updating via Scoop", null));
        var res = await _run(["update", appName.Trim()], ActionTimeout, null, cancellationToken).ConfigureAwait(false);
        return res.ExitCode == 0
            ? new ActionResult(true, false, false, "Updated via Scoop.", null, null)
            : new ActionResult(false, false, false, "Scoop update failed.", FirstError(res), null);
    }

    public async Task<ActionResult> UninstallAsync(string appName, CancellationToken cancellationToken = default)
    {
        if (!IsValidAppName(appName))
        {
            return RunOutcome.NotStarted("Invalid Scoop package name.");
        }

        if (!IsAvailable)
        {
            return RunOutcome.NotStarted("Scoop is not installed on this PC.");
        }

        var res = await _run(["uninstall", appName.Trim()], ActionTimeout, null, cancellationToken).ConfigureAwait(false);
        return res.ExitCode == 0
            ? new ActionResult(true, false, false, "Removed via Scoop.", null, null)
            : new ActionResult(false, false, false, "Scoop uninstall failed.", FirstError(res), null);
    }

    private static bool IsSeparatorLine(string line) =>
        line.Contains('-') && line.All(c => c == '-' || c == ' ');

    internal static IReadOnlyList<ScoopPackage> ParseList(IReadOnlyList<string> lines)
    {
        var results = new List<ScoopPackage>();
        bool seenSeparator = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (IsSeparatorLine(line))
            {
                seenSeparator = true;
                continue;
            }

            if (!seenSeparator) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && IsValidAppName(parts[0]))
            {
                string? bucket = parts.Length >= 3 ? parts[2] : null;
                results.Add(new ScoopPackage(parts[0], parts[1], bucket));
            }
        }

        return results;
    }

    internal static IReadOnlyList<ScoopPackage> ParseSearch(IReadOnlyList<string> lines)
    {
        var results = new List<ScoopPackage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? currentBucket = null;
        bool tableMode = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith('\'') && line.Contains("bucket:", StringComparison.OrdinalIgnoreCase))
            {
                int endQuote = line.IndexOf('\'', 1);
                if (endQuote > 1)
                {
                    currentBucket = line[1..endQuote];
                }
                continue;
            }

            if (IsSeparatorLine(line))
            {
                tableMode = true;
                continue;
            }

            if (line.StartsWith("Name ", StringComparison.OrdinalIgnoreCase) && line.Contains("Version", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (tableMode)
            {
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && IsValidAppName(parts[0]) && seen.Add(parts[0]))
                {
                    string? bucket = parts.Length >= 3 ? parts[2] : currentBucket;
                    results.Add(new ScoopPackage(parts[0], parts[1], bucket));
                }
                continue;
            }

            // Legacy Scoop format: "appname (1.2.3)"
            int paren = line.IndexOf('(');
            int close = paren > 0 ? line.IndexOf(')', paren) : -1;
            if (paren > 0 && close > paren)
            {
                string name = line[..paren].Trim();
                string ver = line[(paren + 1)..close].Trim();
                if (IsValidAppName(name) && seen.Add(name))
                {
                    results.Add(new ScoopPackage(name, ver, currentBucket));
                }
            }
        }

        return results;
    }

    internal static IReadOnlyList<ScoopOutdated> ParseStatus(IReadOnlyList<string> lines)
    {
        var results = new List<ScoopOutdated>();
        bool seenSeparator = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (IsSeparatorLine(line))
            {
                seenSeparator = true;
                continue;
            }

            if (!seenSeparator) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && IsValidAppName(parts[0]))
            {
                results.Add(new ScoopOutdated(parts[0], parts[1], parts[2]));
            }
        }

        return results;
    }

    private static string? FirstError(RunResult res)
    {
        var text = res.Errors.LastOrDefault(l => !string.IsNullOrWhiteSpace(l))
            ?? res.Lines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static async Task<RunResult> RunPowerShellScoopAsync(
        IReadOnlyList<string> scoopArgs,
        TimeSpan timeout,
        Action<string>? onLine,
        CancellationToken cancellationToken)
    {
        var script = FindScoopScript();
        if (script is null || !File.Exists(PowerShellExe))
        {
            return new RunResult(-1, false, false, Array.Empty<string>(), false, new[] { "Scoop not found." });
        }

        var psi = new ProcessStartInfo
        {
            FileName = PowerShellExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);
        foreach (var arg in scoopArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        await Task.Run(process.Start, cancellationToken).ConfigureAwait(false);

        var stdout = new List<string>();
        var stderr = new List<string>();
        var outTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                lock (stdout) stdout.Add(line);
                onLine?.Invoke(line);
            }
        });
        var errTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                lock (stderr) stderr.Add(line);
            }
        });

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        bool cancelled = false;
        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = cancellationToken.IsCancellationRequested;
            timedOut = !cancelled;
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill errors.
            }
        }

        try { await Task.WhenAll(outTask, errTask).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); } catch { }
        return new RunResult(process.HasExited ? process.ExitCode : -1, timedOut, cancelled, stdout, false, stderr);
    }
}
