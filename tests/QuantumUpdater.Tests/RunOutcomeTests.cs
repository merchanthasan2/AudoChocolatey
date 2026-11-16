using System.Runtime.CompilerServices;
using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public class RunOutcomeTests
{
    private const string Profile = "C:\\Users\\Merch";

    private static RunResult Run(
        int exitCode,
        IReadOnlyList<string>? lines = null,
        IReadOnlyList<string>? errors = null,
        bool timedOut = false,
        bool cancelled = false,
        bool stopIncomplete = false,
        Task? stillRunning = null)
    {
        return new RunResult(exitCode, timedOut, cancelled, lines ?? [], stopIncomplete, errors, stillRunning);
    }

    // Reads a fixture from the Data folder next to this test file. The file is not copied to the output folder.
    private static IReadOnlyList<string> ReadFixture(string name, [CallerFilePath] string thisFile = "")
    {
        string path = Path.Combine(Path.GetDirectoryName(thisFile)!, "Data", name);
        return File.ReadAllLines(path);
    }

    [Fact]
    public void Success_gives_updated_text()
    {
        ActionResult result = RunOutcome.FromRun(Run(0), Profile);

        Assert.True(result.Success);
        Assert.False(result.RestartNeeded);
        Assert.False(result.NotStarted);
        Assert.Equal("Updated.", result.Message);
        Assert.Null(result.Detail);
        Assert.Null(result.StillRunning);
    }

    [Fact]
    public void Failure_with_stderr_gives_last_three_lines_and_redacted_profile_path()
    {
        var errors = new List<string>
        {
            "first line",
            "problem in C:\\Users\\Merch\\AppData\\setup.log",
            "third line",
            "fourth line",
        };

        ActionResult result = RunOutcome.FromRun(Run(1, errors: errors), Profile);

        Assert.False(result.Success);
        Assert.False(result.NotStarted);
        Assert.Equal("The update stopped (code 1). Try again. If it fails again, update the app from its website.", result.Message);
        Assert.Equal("problem in %USERPROFILE%\\AppData\\setup.log \u00B7 third line \u00B7 fourth line", result.Detail);
        Assert.DoesNotContain("Merch", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Profile_path_is_redacted_in_any_letter_case()
    {
        var errors = new List<string> { "at c:\\users\\MERCH\\x" };

        ActionResult result = RunOutcome.FromRun(Run(1, errors: errors), Profile);

        Assert.Equal("at %USERPROFILE%\\x", result.Detail);
    }

    [Fact]
    public void Detail_uses_output_lines_when_there_is_no_stderr()
    {
        var lines = new List<string> { "a", "", "b", "   ", "c" };

        ActionResult result = RunOutcome.FromRun(Run(1, lines: lines, errors: []), Profile);

        Assert.Equal("a \u00B7 b \u00B7 c", result.Detail);
    }

    [Fact]
    public void Hash_mismatch_uses_table_text_and_fixture_lines()
    {
        IReadOnlyList<string> lines = ReadFixture("winget-download-hash-mismatch.txt");

        ActionResult result = RunOutcome.FromRun(Run(unchecked((int)0x8A150011), lines: lines), Profile);

        Assert.False(result.Success);
        Assert.False(result.NotStarted);
        Assert.Equal(
            "The download did not match the file the app maker published. Try again later. The app maker may be updating the file.",
            result.Message);
        Assert.NotNull(result.Detail);
        Assert.Contains("Installer hash does not match.", result.Detail);
    }

    [Fact]
    public void Cancelled_with_stop_complete_gives_cancelled_text()
    {
        ActionResult result = RunOutcome.FromRun(Run(-1, cancelled: true), Profile);

        Assert.False(result.Success);
        Assert.False(result.NotStarted);
        Assert.Equal("The update was cancelled.", result.Message);
        Assert.Null(result.StillRunning);
    }

    [Fact]
    public void Cancelled_with_stop_incomplete_keeps_the_still_running_task()
    {
        Task running = new TaskCompletionSource().Task;

        ActionResult result = RunOutcome.FromRun(
            Run(-1, cancelled: true, stopIncomplete: true, stillRunning: running),
            Profile);

        Assert.False(result.Success);
        Assert.Equal("DeskVerve could not stop the installer. The next action starts when it ends.", result.Message);
        Assert.Same(running, result.StillRunning);
    }

    [Fact]
    public void Timeout_with_stop_complete_gives_timeout_text()
    {
        ActionResult result = RunOutcome.FromRun(Run(-1, timedOut: true), Profile);

        Assert.False(result.Success);
        Assert.Equal("The update took too long and was stopped.", result.Message);
        Assert.Null(result.StillRunning);
    }

    [Fact]
    public void Timeout_with_stop_incomplete_keeps_the_still_running_task()
    {
        Task running = new TaskCompletionSource().Task;

        ActionResult result = RunOutcome.FromRun(
            Run(-1, timedOut: true, stopIncomplete: true, stillRunning: running),
            Profile);

        Assert.False(result.Success);
        Assert.Equal("DeskVerve could not stop the installer. The next action starts when it ends.", result.Message);
        Assert.Same(running, result.StillRunning);
    }

    [Fact]
    public void Exit_code_3010_is_success_with_restart_needed()
    {
        ActionResult result = RunOutcome.FromRun(Run(3010), Profile);

        Assert.True(result.Success);
        Assert.True(result.RestartNeeded);
        Assert.False(result.NotStarted);
        Assert.Equal("Updated. Restart the PC to finish.", result.Message);
        Assert.Null(result.Detail);
    }

    [Fact]
    public void Detail_is_at_most_300_characters()
    {
        string longLine = new string('x', 200);
        var errors = new List<string> { longLine, longLine, longLine };

        ActionResult result = RunOutcome.FromRun(Run(1, errors: errors), Profile);

        Assert.NotNull(result.Detail);
        Assert.Equal(300, result.Detail.Length);
    }
}
