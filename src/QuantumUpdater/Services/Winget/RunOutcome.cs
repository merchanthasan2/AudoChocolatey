namespace QuantumUpdater.Services.Winget;

/// <summary>
/// The result of one update, install or uninstall action. Message is plain text for the owner.
/// Detail holds the last installer lines (failures only). NotStarted is true only when the action failed before it started.
/// StillRunning is set when the action has not ended and the stop did not work. It ends when the action ends.
/// </summary>
public sealed record ActionResult(bool Success, bool RestartNeeded, bool NotStarted, string Message, string? Detail, Task? StillRunning)
{
    /// <summary>The message, with the detail after it when there is one (W7). For one status line.</summary>
    public string Text => string.IsNullOrEmpty(Detail) ? Message : $"{Message} ({Detail})";
}

/// <summary>
/// Maps one CLI run result to an ActionResult. Pure: no file access, no WinUI.
/// The user profile path is passed in, so tests control it.
/// </summary>
public static class RunOutcome
{
    /// <summary>The text when a cancel ended the action.</summary>
    internal const string CancelledMessage = "The update was cancelled.";

    /// <summary>The text when the stop did not work. The next action waits.</summary>
    internal const string StopIncompleteMessage = "DeskVerve could not stop the installer. The next action starts when it ends.";

    /// <summary>The text when the time limit ended the action.</summary>
    internal const string TimedOutMessage = "The update took too long and was stopped.";

    /// <summary>The text when a list or search was cancelled.</summary>
    internal const string ListCancelledMessage = "Cancelled.";

    /// <summary>The text when a list or search took too long.</summary>
    internal const string ListTimedOutMessage = "winget timed out.";

    /// <summary>The text when winget.exe was not found or is not trusted.</summary>
    internal const string NotFoundMessage = "winget.exe was not found.";

    private const string UpdatedMessage = "Updated.";
    private const int MaxDetailLength = 300;
    private const int MaxDetailLines = 3;
    private const string DetailSeparator = " · ";

    /// <summary>
    /// Maps the run result of one CLI action to an ActionResult.
    /// W16: the one result helper for update, install and remove. The kind only changes the fixed
    /// done, cancelled and timed-out texts. W7: a failure keeps the last stderr lines in Detail.
    /// </summary>
    public static ActionResult FromRun(RunResult run, string userProfile, ActionKind kind = ActionKind.Update)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.Cancelled)
        {
            return run.StopIncomplete
                ? StopIncomplete(run)
                : new ActionResult(false, false, false, CancelledText(kind), null, null);
        }

        if (run.TimedOut)
        {
            return run.StopIncomplete
                ? StopIncomplete(run)
                : new ActionResult(false, false, false, TimedOutText(kind), null, null);
        }

        if (run.ExitCode == 0)
        {
            return new ActionResult(true, false, false, DoneText(kind), null, null);
        }

        ErrorText text = WingetErrorText.For(run.ExitCode);
        if (text.IsSuccess)
        {
            return new ActionResult(true, text.RestartNeeded, false, text.Reason, null, null);
        }

        return new ActionResult(
            false,
            false,
            false,
            Join(text.Reason, text.WhatToDo),
            BuildDetail(run.Errors, run.Lines, userProfile),
            null);
    }

    /// <summary>A result for an action that never started, for example a bad id or no winget.exe.</summary>
    public static ActionResult NotStarted(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new ActionResult(false, false, true, message, null, null);
    }

    /// <summary>
    /// The error text for a list or search run, or null when the run gave a usable answer.
    /// A run with rows is usable even when winget ended with an error code.
    /// W7: a failed run with no rows names the exit code and keeps the last stderr lines.
    /// </summary>
    public static string? ListError(RunResult run, int rowCount, string userProfile)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.Cancelled)
        {
            return run.StopIncomplete ? StopIncompleteMessage : ListCancelledMessage;
        }

        if (run.TimedOut)
        {
            return run.StopIncomplete ? StopIncompleteMessage : ListTimedOutMessage;
        }

        // An empty answer ("No installed package found") is not an error unless winget wrote to stderr.
        if (run.ExitCode == 0 || rowCount > 0 || NonEmpty(run.Errors).Count == 0)
        {
            return null;
        }

        string detail = BuildDetail(run.Errors, Array.Empty<string>(), userProfile) ?? string.Empty;
        return $"winget stopped (code {CodeText(run.ExitCode)}). {detail}".Trim();
    }

    private static string CodeText(int exitCode)
    {
        uint code = unchecked((uint)exitCode);
        return code >= 0x80000000u
            ? $"0x{code:X8}"
            : code.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string DoneText(ActionKind kind) => kind switch
    {
        ActionKind.Install => "Installed.",
        ActionKind.Remove => "Removed.",
        _ => UpdatedMessage,
    };

    private static string CancelledText(ActionKind kind) => kind switch
    {
        ActionKind.Install => "The install was cancelled.",
        ActionKind.Remove => "The removal was cancelled.",
        _ => CancelledMessage,
    };

    private static string TimedOutText(ActionKind kind) => kind switch
    {
        ActionKind.Install => "The install took too long and was stopped.",
        ActionKind.Remove => "The removal took too long and was stopped.",
        _ => TimedOutMessage,
    };

    // Joins the reason and the next step. Trims the space when the next step is empty.
    internal static string Join(string reason, string whatToDo)
    {
        return $"{reason} {whatToDo}".Trim();
    }

    // The stop did not work: keep the task, so the caller can wait for it.
    private static ActionResult StopIncomplete(RunResult run)
    {
        return new ActionResult(false, false, false, StopIncompleteMessage, null, run.StillRunning);
    }

    // Uses the last non-empty stderr lines. When there are none, uses the last stdout lines.
    // The user profile path is replaced first, then the text is cut, so a cut never leaves a part of the path.
    private static string? BuildDetail(IReadOnlyList<string> errors, IReadOnlyList<string> lines, string userProfile)
    {
        List<string> source = NonEmpty(errors);
        if (source.Count == 0)
        {
            source = NonEmpty(lines);
        }

        if (source.Count == 0)
        {
            return null;
        }

        var last = source.TakeLast(MaxDetailLines);
        string text = CrashLog.Redact(string.Join(DetailSeparator, last), userProfile);
        return text.Length > MaxDetailLength ? text[..MaxDetailLength] : text;
    }

    private static List<string> NonEmpty(IReadOnlyList<string> items)
    {
        return items.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
    }
}
