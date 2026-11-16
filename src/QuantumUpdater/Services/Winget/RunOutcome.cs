namespace QuantumUpdater.Services.Winget;

/// <summary>
/// The result of one update, install or uninstall action. Message is plain text for the owner.
/// Detail holds the last installer lines (failures only). NotStarted is true only when the action failed before it started.
/// StillRunning is set when the action has not ended and the stop did not work. It ends when the action ends.
/// </summary>
public sealed record ActionResult(bool Success, bool RestartNeeded, bool NotStarted, string Message, string? Detail, Task? StillRunning);

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

    private const string UpdatedMessage = "Updated.";
    private const int MaxDetailLength = 300;
    private const int MaxDetailLines = 3;
    private const string DetailSeparator = " · ";

    /// <summary>Maps the run result of one CLI action to an ActionResult.</summary>
    public static ActionResult FromRun(RunResult run, string userProfile)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.Cancelled)
        {
            return run.StopIncomplete
                ? StopIncomplete(run)
                : new ActionResult(false, false, false, CancelledMessage, null, null);
        }

        if (run.TimedOut)
        {
            return run.StopIncomplete
                ? StopIncomplete(run)
                : new ActionResult(false, false, false, TimedOutMessage, null, null);
        }

        if (run.ExitCode == 0)
        {
            return new ActionResult(true, false, false, UpdatedMessage, null, null);
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
