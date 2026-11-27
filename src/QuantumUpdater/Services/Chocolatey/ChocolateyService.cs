using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Chocolatey;

public sealed record ChocoPackage(string Id, string Version);

/// <summary>
/// Manages Chocolatey installs, updates, uninstalls, and catalog searches when choco.exe is installed.
/// Strict package id allowlist and ArgumentList only (no shell interpolation).
/// </summary>
public sealed class ChocolateyService
{
    private static readonly Regex PackagePattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._-]{0,120}\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QueryPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9 ._+-]{0,79}\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromMinutes(15);

    private readonly Func<string?> _findChoco;
    private readonly Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<RunResult>> _run;

    public ChocolateyService()
        : this(FindChocoExe, RunChocoAsync)
    {
    }

    internal ChocolateyService(
        Func<string?> findChoco,
        Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<RunResult>> run)
    {
        _findChoco = findChoco;
        _run = run;
    }

    public bool IsAvailable => _findChoco() is not null;

    public static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && PackagePattern.IsMatch(id.Trim());

    public static string? FindChocoExe()
    {
        var candidates = new List<string>();
        var envDir = Environment.GetEnvironmentVariable("ChocolateyInstall");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            candidates.Add(Path.Combine(envDir.Trim(), "bin", "choco.exe"));
        }

        var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(progData))
        {
            candidates.Add(Path.Combine(progData, "chocolatey", "bin", "choco.exe"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<IReadOnlyList<ChocoPackage>> ListInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return Array.Empty<ChocoPackage>();
        var res = await _run(["list", "--limit-output"], QueryTimeout, cancellationToken).ConfigureAwait(false);
        return ParseLimitOutput(res.Lines);
    }

    public async Task<IReadOnlyList<ChocoPackage>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(query)) return Array.Empty<ChocoPackage>();
        var trimmed = query.Trim();
        if (!QueryPattern.IsMatch(trimmed)) return Array.Empty<ChocoPackage>();

        var res = await _run(["search", trimmed, "--limit-output", "--page-size=50"], QueryTimeout, cancellationToken).ConfigureAwait(false);
        return ParseLimitOutput(res.Lines);
    }

    public async Task<ActionResult> InstallAsync(
        string id,
        IProgress<ActionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id)) return RunOutcome.NotStarted("Invalid Chocolatey package id.");
        if (!IsAvailable) return RunOutcome.NotStarted("Chocolatey is not installed on this PC.");

        progress?.Report(new ActionProgress("Installing via Chocolatey", null));
        var res = await _run(["install", id.Trim(), "-y", "--no-progress"], ActionTimeout, cancellationToken).ConfigureAwait(false);
        return res.ExitCode == 0
            ? new ActionResult(true, false, false, "Installed via Chocolatey.", null, null)
            : new ActionResult(false, false, false, "Chocolatey install failed.", res.Errors.LastOrDefault() ?? res.Lines.LastOrDefault(), null);
    }

    public async Task<ActionResult> UpgradeAsync(
        string id,
        IProgress<ActionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id)) return RunOutcome.NotStarted("Invalid Chocolatey package id.");
        if (!IsAvailable) return RunOutcome.NotStarted("Chocolatey is not installed on this PC.");

        progress?.Report(new ActionProgress("Updating via Chocolatey", null));
        var res = await _run(["upgrade", id.Trim(), "-y", "--no-progress"], ActionTimeout, cancellationToken).ConfigureAwait(false);
        return res.ExitCode == 0
            ? new ActionResult(true, false, false, "Updated via Chocolatey.", null, null)
            : new ActionResult(false, false, false, "Chocolatey update failed.", res.Errors.LastOrDefault() ?? res.Lines.LastOrDefault(), null);
    }

    public async Task<ActionResult> UninstallAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id)) return RunOutcome.NotStarted("Invalid Chocolatey package id.");
        if (!IsAvailable) return RunOutcome.NotStarted("Chocolatey is not installed on this PC.");

        var res = await _run(["uninstall", id.Trim(), "-y"], ActionTimeout, cancellationToken).ConfigureAwait(false);
        return res.ExitCode == 0
            ? new ActionResult(true, false, false, "Removed via Chocolatey.", null, null)
            : new ActionResult(false, false, false, "Chocolatey uninstall failed.", res.Errors.LastOrDefault() ?? res.Lines.LastOrDefault(), null);
    }

    internal static IReadOnlyList<ChocoPackage> ParseLimitOutput(IReadOnlyList<string> lines)
    {
        var list = new List<ChocoPackage>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            int bar = line.IndexOf('|');
            if (bar <= 0 || bar == line.Length - 1) continue;
            string id = line[..bar].Trim();
            string ver = line[(bar + 1)..].Trim();
            if (IsValidId(id) && ver.Length > 0)
            {
                list.Add(new ChocoPackage(id, ver));
            }
        }
        return list;
    }

    private static async Task<RunResult> RunChocoAsync(
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var exe = FindChocoExe();
        if (exe is null)
        {
            return new RunResult(-1, false, false, Array.Empty<string>(), false, new[] { "choco.exe not found." });
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        await Task.Run(process.Start, cancellationToken).ConfigureAwait(false);
        var outTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errTask = process.StandardError.ReadToEndAsync(cancellationToken);

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
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        }

        var stdout = (await outTask.ConfigureAwait(false)).Split('\n').Select(s => s.TrimEnd('\r')).ToList();
        var stderr = (await errTask.ConfigureAwait(false)).Split('\n').Select(s => s.TrimEnd('\r')).ToList();
        return new RunResult(process.HasExited ? process.ExitCode : -1, timedOut, cancelled, stdout, false, stderr);
    }
}
