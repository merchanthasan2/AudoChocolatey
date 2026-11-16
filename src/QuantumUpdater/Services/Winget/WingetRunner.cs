using System.Diagnostics;
using System.Text;

namespace QuantumUpdater.Services.Winget;

/// <summary>The result of one winget run.</summary>
public sealed record RunResult(int ExitCode, bool TimedOut, bool Cancelled, IReadOnlyList<string> Lines);

/// <summary>
/// Runs winget.exe with a fixed argument list. Never uses a shell or one command string.
/// On cancel or timeout it kills the whole process tree (F16).
/// Adds the flags that stop prompts, and adds --exact when an id is given (F4).
/// </summary>
public sealed class WingetRunner
{
    private const string IdFlag = "--id";
    private const string IdFlagPrefix = "--id=";
    private const string ExactFlag = "--exact";
    private const string DisableInteractivityFlag = "--disable-interactivity";
    private const string AcceptSourceAgreementsFlag = "--accept-source-agreements";

    // Time allowed for the output readers to finish after the process ends.
    private static readonly TimeSpan ReaderGrace = TimeSpan.FromSeconds(5);

    private readonly string _executablePath;

    /// <param name="executablePath">Full path to winget.exe. The app passes WingetLocator.Find().</param>
    public WingetRunner(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _executablePath = executablePath;
    }

    /// <summary>
    /// Runs winget with the given arguments. Each stdout line goes to onLine in order.
    /// Throws ArgumentException before any process starts when an argument is not allowed.
    /// </summary>
    public async Task<RunResult> RunAsync(
        IReadOnlyList<string> args,
        TimeSpan timeout,
        Action<string>? onLine,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }

        var finalArgs = BuildArguments(args);
        if (token.IsCancellationRequested)
        {
            return new RunResult(-1, false, true, []);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in finalArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var collected = new List<string>();
        Exception? callbackError = null;

        void HandleStdoutLine(string line)
        {
            lock (collected)
            {
                collected.Add(line);
            }

            if (onLine is null || callbackError is not null)
            {
                return;
            }

            try
            {
                onLine(line);
            }
            catch (Exception ex)
            {
                // Keep reading so the process never blocks on a full pipe. Report the error at the end.
                callbackError = ex;
            }
        }

        // Read both pipes at the same time so the process never blocks on a full pipe.
        var stdoutTask = ReadLinesAsync(process.StandardOutput, HandleStdoutLine);
        var stderrTask = ReadLinesAsync(process.StandardError, _ => { });

        bool cancelled = false;
        bool timedOut = false;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = token.IsCancellationRequested;
            timedOut = !cancelled;
            KillProcessTree(process);
            await process.WaitForExitAsync().ConfigureAwait(false);
        }

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(ReaderGrace).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A child outside the tree still holds the pipe. Use the lines read so far.
        }

        if (callbackError is not null)
        {
            throw new InvalidOperationException("The onLine callback failed.", callbackError);
        }

        List<string> snapshot;
        lock (collected)
        {
            snapshot = collected.ToList();
        }

        return new RunResult(process.ExitCode, timedOut, cancelled, snapshot);
    }

    /// <summary>
    /// Checks the arguments and adds the required flags. Returns a new list.
    /// Throws ArgumentException when an argument is null, has a control character,
    /// or when an --id value is not a catalog id.
    /// </summary>
    internal static List<string> BuildArguments(IReadOnlyList<string> args)
    {
        var result = new List<string>(args.Count + 3);
        foreach (var arg in args)
        {
            if (arg is null)
            {
                throw new ArgumentException("An argument is null.", nameof(args));
            }

            if (arg.Any(c => char.IsControl(c)))
            {
                throw new ArgumentException("An argument contains a control character.", nameof(args));
            }

            // Short flags (-i, -e, -q ...) would skip the --id check and the forced --exact. Long flags only.
            if (arg.Length > 1 && arg[0] == '-' && arg[1] != '-')
            {
                throw new ArgumentException("Use long winget flags only (for example --id, not -i).", nameof(args));
            }

            result.Add(arg);
        }

        bool hasId = false;
        for (int i = 0; i < result.Count; i++)
        {
            var arg = result[i];
            if (string.Equals(arg, IdFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= result.Count)
                {
                    throw new ArgumentException("--id needs a package id.", nameof(args));
                }

                ValidateCatalogId(result[i + 1], args);
                hasId = true;
            }
            else if (arg.StartsWith(IdFlagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                ValidateCatalogId(arg[IdFlagPrefix.Length..], args);
                hasId = true;
            }
        }

        if (!ContainsFlag(result, DisableInteractivityFlag))
        {
            result.Add(DisableInteractivityFlag);
        }

        if (!ContainsFlag(result, AcceptSourceAgreementsFlag))
        {
            result.Add(AcceptSourceAgreementsFlag);
        }

        if (hasId && !ContainsFlag(result, ExactFlag))
        {
            result.Add(ExactFlag);
        }

        return result;
    }

    private static void ValidateCatalogId(string value, IReadOnlyList<string> args)
    {
        if (!PackageId.IsCatalogId(value))
        {
            throw new ArgumentException("The package id is not a catalog id.", nameof(args));
        }
    }

    private static bool ContainsFlag(List<string> args, string flag)
    {
        return args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task ReadLinesAsync(StreamReader reader, Action<string> handle)
    {
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            handle(line);
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process ended on its own before the kill.
        }
    }
}
