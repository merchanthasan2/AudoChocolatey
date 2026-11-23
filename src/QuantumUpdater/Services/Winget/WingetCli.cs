namespace QuantumUpdater.Services.Winget;

/// <summary>
/// W16: the one place where the services run winget and turn the run into a result.
/// Actions (install, upgrade, uninstall) give an ActionResult through RunOutcome.
/// Lists and searches give rows and one error text.
/// winget.exe is looked up on every call, so an App Installer update that moved it is picked up (W5).
/// </summary>
internal static class WingetCli
{
    internal const string IdRequiredMessage = "Package id is required.";
    internal const string IdInvalidMessage = "Package id contains invalid characters.";
    private const string CouldNotStartMessage = "winget could not start.";

    private static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Runs one action on one catalog id. Checks the id before anything starts. Never throws.
    /// </summary>
    internal static async Task<ActionResult> RunActionAsync(
        ActionKind kind,
        string verb,
        string? packageId,
        IReadOnlyList<string> extraArgs,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return RunOutcome.NotStarted(IdRequiredMessage);
        }

        packageId = packageId.Trim();
        if (!PackageId.IsCatalogId(packageId))
        {
            return RunOutcome.NotStarted(IdInvalidMessage);
        }

        // Off the UI thread: the lookup makes a cross-process COM call that pumps messages (start crash 0xC000027B).
        var executable = await Task.Run(WingetLocator.Find).ConfigureAwait(false);
        if (executable is null)
        {
            return RunOutcome.NotStarted(RunOutcome.NotFoundMessage);
        }

        var args = new List<string>(extraArgs.Count + 3) { verb, "--id", packageId };
        args.AddRange(extraArgs);
        try
        {
            var run = await new WingetRunner(executable)
                .RunAsync(args, timeout, null, cancellationToken)
                .ConfigureAwait(false);
            return RunOutcome.FromRun(run, UserProfile, kind);
        }
        catch (OperationCanceledException)
        {
            return RunOutcome.FromRun(new RunResult(-1, false, true, []), UserProfile, kind);
        }
        catch (ArgumentException)
        {
            // The runner refused the arguments. Nothing started.
            return RunOutcome.NotStarted(IdInvalidMessage);
        }
        catch (Exception)
        {
            // Exception text can hold paths. The owner gets a fixed text.
            return RunOutcome.NotStarted(CouldNotStartMessage);
        }
    }

    /// <summary>
    /// Runs a list or search and parses the output. Error is null when the answer can be used. Never throws.
    /// </summary>
    internal static async Task<(IReadOnlyList<T> Items, string? Error)> RunQueryAsync<T>(
        IReadOnlyList<string> args,
        TimeSpan timeout,
        Func<RunResult, IReadOnlyList<T>> parse,
        CancellationToken cancellationToken)
    {
        // Off the UI thread: the lookup makes a cross-process COM call that pumps messages (start crash 0xC000027B).
        var executable = await Task.Run(WingetLocator.Find).ConfigureAwait(false);
        if (executable is null)
        {
            return (Array.Empty<T>(), RunOutcome.NotFoundMessage);
        }

        try
        {
            var run = await new WingetRunner(executable)
                .RunAsync(args, timeout, null, cancellationToken)
                .ConfigureAwait(false);
            if (run.Cancelled || run.TimedOut)
            {
                return (Array.Empty<T>(), RunOutcome.ListError(run, 0, UserProfile));
            }

            var items = parse(run);
            return (items, RunOutcome.ListError(run, items.Count, UserProfile));
        }
        catch (OperationCanceledException)
        {
            return (Array.Empty<T>(), RunOutcome.ListCancelledMessage);
        }
        catch (Exception)
        {
            return (Array.Empty<T>(), CouldNotStartMessage);
        }
    }
}
