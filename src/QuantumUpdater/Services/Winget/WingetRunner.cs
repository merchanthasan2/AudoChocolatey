using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace QuantumUpdater.Services.Winget;

/// <summary>
/// The result of one winget run. StopIncomplete is true when the process could not be stopped.
/// ErrorLines holds the last stderr lines (at most 20). Read them with Errors.
/// StillRunning is set only when StopIncomplete is true and the process has not ended. It ends when the process ends.
/// </summary>
public sealed record RunResult(int ExitCode, bool TimedOut, bool Cancelled, IReadOnlyList<string> Lines, bool StopIncomplete = false, IReadOnlyList<string>? ErrorLines = null, Task? StillRunning = null)
{
    /// <summary>The last stderr lines. Empty when winget wrote none.</summary>
    public IReadOnlyList<string> Errors => ErrorLines ?? Array.Empty<string>();
}

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

    // Only the last stderr lines are kept, and each one is cut to a fixed length.
    private const int MaxErrorLines = 20;
    private const int MaxErrorLineLength = 500;

    private readonly string _executablePath;

    /// <summary>Stops the process tree. Tests replace it to make the stop fail.</summary>
    internal Action<Process> KillTree { get; init; } = process => process.Kill(entireProcessTree: true);

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

        var process = new Process { StartInfo = startInfo };
        var handedOff = false;
        try
        {
            process.Start();

            var collected = new List<string>();
            var errorLines = new List<string>();
            Exception? callbackError = null;

            void HandleStderrLine(string line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                var kept = line.Length > MaxErrorLineLength ? line[..MaxErrorLineLength] : line;
                lock (errorLines)
                {
                    errorLines.Add(kept);
                    if (errorLines.Count > MaxErrorLines)
                    {
                        errorLines.RemoveAt(0);
                    }
                }
            }

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
            var stderrTask = ReadLinesAsync(process.StandardError, HandleStderrLine);

            bool cancelled = false;
            bool timedOut = false;
            bool stopIncomplete = false;
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
                stopIncomplete = !KillProcessTree(process, KillTree);
                try
                {
                    // Bounded: a process that could not be killed must not block the caller.
                    await process.WaitForExitAsync().WaitAsync(ReaderGrace).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    stopIncomplete = true;
                }
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

            List<string> errorSnapshot;
            lock (errorLines)
            {
                errorSnapshot = errorLines.ToList();
            }

            // The stop failed and the process may still run. Hand it over: do not dispose it now.
            bool stillRunningNow = stopIncomplete && IsRunning(process);
            int exitCode = ReadExitCode(process);
            Task? stillRunning = null;
            if (stillRunningNow)
            {
                stillRunning = WaitThenDisposeAsync(process);
                handedOff = true;
            }

            return new RunResult(exitCode, timedOut, cancelled, snapshot, stopIncomplete, errorSnapshot, stillRunning);
        }
        finally
        {
            if (!handedOff)
            {
                process.Dispose();
            }
        }
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

    /// <summary>Waits for the process to end, then disposes it. Never throws.</summary>
    private static async Task WaitThenDisposeAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // The process ended or was never started.
        }
        catch (Win32Exception)
        {
            // The process state could not be read.
        }
        finally
        {
            process.Dispose();
        }
    }

    // True when the process has not ended. A process whose state cannot be read counts as running.
    private static bool IsRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return true;
        }
    }

    /// <summary>Stops the process tree. Returns false when the stop failed. Never throws.</summary>
    internal static bool KillProcessTree(Process process, Action<Process> kill)
    {
        try
        {
            if (!process.HasExited)
            {
                kill(process);
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            // The process ended on its own before the kill.
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (AggregateException)
        {
            return false;
        }
    }

    // The exit code is not set while the process still runs. Return -1 then.
    private static int ReadExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
        catch (Win32Exception)
        {
            return -1;
        }
    }
}
