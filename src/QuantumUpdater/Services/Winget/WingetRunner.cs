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
        return await StartCheckedAsync(finalArgs, timeout, onLine, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Arguments must come from BuildArguments. Only tests call this directly, to run FakeWinget modes.
    /// </summary>
    internal async Task<RunResult> StartCheckedAsync(
        List<string> finalArgs,
        TimeSpan timeout,
        Action<string>? onLine,
        CancellationToken token)
    {
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
            // W9: a reader left behind after the grace time can still fail later. Observe its fault.
            var stdoutTask = ReadLinesAsync(process.StandardOutput, HandleStdoutLine);
            var stderrTask = ReadLinesAsync(process.StandardError, HandleStderrLine);
            _ = ObserveFaults(stdoutTask);
            _ = ObserveFaults(stderrTask);

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
                    var exitWait = process.WaitForExitAsync();
                    _ = ObserveFaults(exitWait);
                    await exitWait.WaitAsync(ReaderGrace).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    stopIncomplete = true;
                }
            }

            try
            {
                var readers = Task.WhenAll(stdoutTask, stderrTask);
                _ = ObserveFaults(readers);
                await readers.WaitAsync(ReaderGrace).ConfigureAwait(false);
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

    // The only winget verbs the app may run.
    private const string UpgradeVerb = "upgrade";
    private const string SourceFlag = "--source";
    private const string WingetSource = "winget";
    private const string CountFlag = "--count";
    private const string QueryFlag = "--query";
    private const string VersionFlag = "--version";

    // Flags that take no value. Every verb may use them.
    private static readonly string[] SwitchFlagsForAll =
    [
        "--accept-source-agreements",
        "--accept-package-agreements",
        "--disable-interactivity",
        "--silent",
        "--exact",
        "--include-unknown",
    ];

    // Flags that take one value. Every verb may use them.
    private static readonly string[] ValueFlagsForAll = [IdFlag, SourceFlag, CountFlag];

    // NeedsId: the verb is always an action and must name exactly one package (W8).
    private sealed record VerbRules(bool AllowsPositional, bool IsAction, bool NeedsId, HashSet<string> SwitchFlags, HashSet<string> ValueFlags);

    private static readonly Dictionary<string, VerbRules> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        // list and search take a plain word (the search text) and --query. show also takes --version.
        ["list"] = MakeRules(allowsPositional: true, isAction: false, QueryFlag),
        ["search"] = MakeRules(allowsPositional: true, isAction: false, QueryFlag),
        ["show"] = MakeRules(allowsPositional: true, isAction: false, QueryFlag, VersionFlag),
        // Action verbs never take a plain word. With an --id they are the action form.
        // upgrade without an --id is the list of available updates, so only install and uninstall need one.
        ["install"] = MakeRules(allowsPositional: false, isAction: true, VersionFlag, needsId: true),
        ["upgrade"] = MakeRules(allowsPositional: false, isAction: true, VersionFlag),
        ["uninstall"] = MakeRules(allowsPositional: false, isAction: true, VersionFlag, needsId: true),
    };

    private static VerbRules MakeRules(bool allowsPositional, bool isAction, string? extraValueFlag = null, string? secondValueFlag = null, bool needsId = false)
    {
        var switches = new HashSet<string>(SwitchFlagsForAll, StringComparer.OrdinalIgnoreCase);
        var values = new HashSet<string>(ValueFlagsForAll, StringComparer.OrdinalIgnoreCase);
        if (extraValueFlag is not null)
        {
            values.Add(extraValueFlag);
        }

        if (secondValueFlag is not null)
        {
            values.Add(secondValueFlag);
        }

        return new VerbRules(allowsPositional, isAction, needsId, switches, values);
    }

    /// <summary>
    /// Checks the arguments against the allowlist and adds the required flags. Returns a new list.
    /// The first argument must be an allowed verb, and every flag must be allowed for that verb.
    /// A flag that takes a value takes the next argument, which must not start with "--".
    /// install, upgrade and uninstall with an --id take exactly one --id and no plain words.
    /// upgrade gets "--source winget" when no --source is given, and refuses any other source.
    /// Throws ArgumentException when an argument is null, has a control character, is a short flag,
    /// is not allowed, or when an --id value is not a catalog id.
    /// </summary>
    internal static List<string> BuildArguments(IReadOnlyList<string> args)
    {
        var result = new List<string>(args.Count + 4);
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

        if (result.Count == 0 || !Verbs.TryGetValue(result[0], out var rules))
        {
            throw new ArgumentException("The first argument must be an allowed winget verb.", nameof(args));
        }

        int idCount = 0;
        var sources = new List<string>();
        for (int i = 1; i < result.Count; i++)
        {
            var arg = result[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (!rules.AllowsPositional)
                {
                    throw new ArgumentException("This verb does not take a plain word.", nameof(args));
                }

                continue;
            }

            if (rules.ValueFlags.Contains(arg))
            {
                if (i + 1 >= result.Count || result[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{arg} needs a value.", nameof(args));
                }

                i++;
                var value = result[i];
                if (string.Equals(arg, IdFlag, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCatalogId(value, args);
                    idCount++;
                }
                else if (string.Equals(arg, SourceFlag, StringComparison.OrdinalIgnoreCase))
                {
                    sources.Add(value);
                }

                continue;
            }

            if (!rules.SwitchFlags.Contains(arg))
            {
                throw new ArgumentException("A winget flag is not allowed for this verb.", nameof(args));
            }
        }

        if ((rules.IsAction && idCount > 1) || (rules.NeedsId && idCount != 1))
        {
            throw new ArgumentException("Use exactly one --id.", nameof(args));
        }

        if (string.Equals(result[0], UpgradeVerb, StringComparison.OrdinalIgnoreCase))
        {
            if (sources.Count == 0)
            {
                result.Add(SourceFlag);
                result.Add(WingetSource);
            }
            else if (sources.Any(source => !string.Equals(source, WingetSource, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("upgrade only uses the winget source.", nameof(args));
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

        if (idCount > 0 && !ContainsFlag(result, ExactFlag))
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

    /// <summary>
    /// W9: reads the fault of a task that may be left behind, so it never shows up as an unobserved task exception.
    /// Returns the continuation, so tests can wait for it. The continuation never throws.
    /// </summary>
    internal static Task ObserveFaults(Task task)
    {
        return task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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
