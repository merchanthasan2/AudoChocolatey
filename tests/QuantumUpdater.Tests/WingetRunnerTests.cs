using System.ComponentModel;
using System.Diagnostics;
using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public class WingetRunnerTests
{
    private static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(1);

    private static readonly string[] ExpectedLines =
    [
        "alpha",
        "--disable-interactivity",
        "--accept-source-agreements",
        "line1",
        "line2",
        "line3",
        "line4",
        "line5",
    ];

    private static WingetRunner NewRunner()
    {
        return new WingetRunner(Path.Combine(AppContext.BaseDirectory, "FakeWinget", "FakeWinget.exe"));
    }

    private static int RunningFakeCount()
    {
        var found = Process.GetProcessesByName("FakeWinget");
        foreach (var process in found)
        {
            process.Dispose();
        }

        return found.Length;
    }

    [Fact]
    public async Task Cancel_ends_the_process_within_two_seconds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var watch = Stopwatch.StartNew();

        var result = await NewRunner().StartCheckedAsync(["--sleep"], LongTimeout, null, cts.Token);

        watch.Stop();
        Assert.True(result.Cancelled);
        Assert.False(result.TimedOut);
        Assert.True(watch.Elapsed <= TimeSpan.FromSeconds(2), $"Took {watch.Elapsed}.");
        Assert.Equal(0, RunningFakeCount());
    }

    [Fact]
    public async Task Timeout_ends_a_thirty_second_sleep_within_two_seconds()
    {
        var watch = Stopwatch.StartNew();

        var result = await NewRunner().StartCheckedAsync(["--sleep"], TimeSpan.FromSeconds(1), null, CancellationToken.None);

        watch.Stop();
        Assert.True(result.TimedOut);
        Assert.False(result.Cancelled);
        Assert.True(watch.Elapsed <= TimeSpan.FromSeconds(2), $"Took {watch.Elapsed}.");
        Assert.Equal(0, RunningFakeCount());
    }

    [Fact]
    public async Task Lines_arrive_in_order_and_match_the_result()
    {
        var seen = new List<string>();

        var result = await NewRunner().StartCheckedAsync(["alpha", "--disable-interactivity", "--accept-source-agreements"], LongTimeout, seen.Add, CancellationToken.None);

        Assert.Equal(ExpectedLines, result.Lines);
        Assert.Equal(ExpectedLines, seen);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task Exit_code_is_returned()
    {
        var result = await NewRunner().StartCheckedAsync(["--fail"], LongTimeout, null, CancellationToken.None);

        Assert.Equal(7, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task Exact_is_added_when_id_is_present()
    {
        var result = await NewRunner().RunAsync(["install", "--id", "Git.Git"], LongTimeout, null, CancellationToken.None);

        Assert.Single(result.Lines, line => line == "--exact");
    }

    [Fact]
    public async Task Disable_interactivity_is_added_once()
    {
        var result = await NewRunner().RunAsync(["list", "--disable-interactivity"], LongTimeout, null, CancellationToken.None);

        Assert.Single(result.Lines, line => line == "--disable-interactivity");
        Assert.Single(result.Lines, line => line == "--accept-source-agreements");
    }

    [Fact]
    public async Task Stderr_lines_are_kept()
    {
        var result = await NewRunner().StartCheckedAsync(["--stderr", "3"], LongTimeout, null, CancellationToken.None);

        Assert.Equal(3, result.Errors.Count);
        Assert.Equal("error line 1", result.Errors[0]);
        Assert.Contains("done", result.Lines);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Progress_mode_prints_the_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"progress-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "a\rb\rc\ndone\n");
        try
        {
            var result = await NewRunner().StartCheckedAsync(["--progress", path], LongTimeout, null, CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.False(result.TimedOut);
            Assert.False(result.Cancelled);
            // The reader ends a line at \r, at \n and at \r\n. So "a\rb\rc\n" gives three lines.
            Assert.Equal(new[] { "a", "b", "c", "done" }, result.Lines);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Only_the_last_20_stderr_lines_are_kept()
    {
        var result = await NewRunner().StartCheckedAsync(["--stderr", "50"], LongTimeout, null, CancellationToken.None);

        Assert.Equal(20, result.Errors.Count);
        Assert.Equal("error line 31", result.Errors[0]);
        Assert.Equal("error line 50", result.Errors[19]);
    }

    [Fact]
    public async Task Local_arp_id_is_rejected_before_start()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewRunner().RunAsync(["--id", @"ARP\Machine\X64\Foo"], LongTimeout, null, CancellationToken.None));
    }

    [Fact]
    public async Task Argument_with_newline_is_rejected_before_start()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewRunner().RunAsync(["a\nb"], LongTimeout, null, CancellationToken.None));
    }

    [Fact]
    public async Task Public_run_refuses_a_non_verb_before_start()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewRunner().RunAsync(["--sleep"], LongTimeout, null, CancellationToken.None));
    }

    [Theory]
    [InlineData("-i")]
    [InlineData("-e")]
    [InlineData("-q")]
    public async Task Short_flags_are_rejected_before_start(string flag)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewRunner().RunAsync(["uninstall", flag, "Mozilla.Firefox"], LongTimeout, null, CancellationToken.None));
    }

    [Fact]
    public async Task Failed_kill_on_timeout_returns_timed_out_with_stop_incomplete()
    {
        var runner = new WingetRunner(Path.Combine(AppContext.BaseDirectory, "FakeWinget", "FakeWinget.exe"))
        {
            KillTree = _ => throw new Win32Exception(5, "Access is denied."),
        };
        try
        {
            var result = await runner.StartCheckedAsync(["--sleep"], TimeSpan.FromSeconds(1), null, CancellationToken.None);

            Assert.True(result.TimedOut);
            Assert.False(result.Cancelled);
            Assert.True(result.StopIncomplete);
            Assert.NotNull(result.StillRunning);
        }
        finally
        {
            // The fake could not be stopped by the runner. Stop it here so no test leaves it running.
            KillRunningFakes();
        }
    }

    [Fact]
    public async Task Incomplete_stop_returns_still_running_that_ends_when_the_process_ends()
    {
        var runner = new WingetRunner(Path.Combine(AppContext.BaseDirectory, "FakeWinget", "FakeWinget.exe"))
        {
            // The kill does nothing, so the fake keeps running and the stop stays incomplete.
            KillTree = _ => { },
        };
        try
        {
            var result = await runner.StartCheckedAsync(["--sleep"], TimeSpan.FromSeconds(1), null, CancellationToken.None);

            Assert.True(result.TimedOut);
            Assert.True(result.StopIncomplete);
            Assert.NotNull(result.StillRunning);
            Assert.False(result.StillRunning!.IsCompleted);

            KillRunningFakes();

            await result.StillRunning.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            KillRunningFakes();
        }
    }

    // Stops every running fake winget. A test uses it so no FakeWinget is left running.
    private static void KillRunningFakes()
    {
        foreach (var process in Process.GetProcessesByName("FakeWinget"))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already ended.
            }
            catch (Win32Exception)
            {
                // Could not stop it. The test already checked the result.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    [Fact]
    public void Kill_failures_are_reported_and_never_thrown()
    {
        using var process = Process.GetCurrentProcess();

        Assert.False(WingetRunner.KillProcessTree(process, _ => throw new Win32Exception(5)));
        Assert.False(WingetRunner.KillProcessTree(process, _ => throw new AggregateException("kill failed")));
        Assert.True(WingetRunner.KillProcessTree(process, _ => throw new InvalidOperationException("already ended")));
        Assert.True(WingetRunner.KillProcessTree(process, _ => { }));
    }

    // Argument lists the app may pass. The first six are the forms the callers use today.
    public static IEnumerable<object[]> AllowedLists()
    {
        // InstalledService.cs:45
        yield return new object[] { new[] { "list", "--disable-interactivity", "--accept-source-agreements" } };
        // InstalledService.cs:110
        yield return new object[] { new[] { "uninstall", "--id", "Mozilla.Firefox" } };
        // MarketplaceService.cs:58 (the search text is a plain word)
        yield return new object[] { new[] { "search", "zoom" } };
        // MarketplaceService.cs:130
        yield return new object[] { new[] { "install", "--id", "Mozilla.Firefox", "--accept-package-agreements" } };
        // WingetService.cs:50
        yield return new object[] { new[] { "upgrade", "--include-unknown", "--disable-interactivity", "--accept-source-agreements" } };
        // WingetService.cs:128
        yield return new object[]
        {
            new[]
            {
                "upgrade", "--id", "Mozilla.Firefox", "--include-unknown", "--disable-interactivity",
                "--silent", "--accept-package-agreements", "--accept-source-agreements",
            },
        };
        // Extra allowed forms.
        yield return new object[] { new[] { "upgrade", "--id", "Mozilla.Firefox" } };
        yield return new object[] { new[] { "list" } };
        yield return new object[] { new[] { "search", "--query", "zoom", "--count", "50" } };
        yield return new object[] { new[] { "show", "--id", "Mozilla.Firefox" } };
        yield return new object[] { new[] { "install", "--id", "7zip.7zip", "--silent", "--accept-package-agreements" } };
        yield return new object[] { new[] { "uninstall", "--id", "Mozilla.Firefox", "--silent" } };
    }

    // Argument lists the app must refuse before any process starts.
    public static IEnumerable<object[]> RefusedLists()
    {
        yield return new object[] { new[] { "configure" } };
        yield return new object[] { new[] { "upgrade", "--id", "Mozilla.Firefox", "--id", "Git.Git" } };
        yield return new object[] { new[] { "upgrade", "--id", "Mozilla.Firefox", "extra" } };
        yield return new object[] { new[] { "upgrade", "--id", "Mozilla.Firefox", "--source", "msstore" } };
        yield return new object[] { new[] { "install", "--query", "zoom" } };
        yield return new object[] { new[] { "upgrade", "--all" } };
        yield return new object[] { new[] { "uninstall", "--id", "Mozilla.Firefox", "--purge" } };
        yield return new object[] { new[] { "install", "--id", "Mozilla.Firefox", "--override", "/S" } };
        yield return new object[] { new[] { "list", "--id" } };
        yield return new object[] { new[] { "search", "--query", "--silent" } };
    }

    [Theory]
    [MemberData(nameof(AllowedLists))]
    public void Allowed_argument_lists_are_accepted(string[] args)
    {
        var result = WingetRunner.BuildArguments(args);

        Assert.Equal(args[0], result[0]);
    }

    [Theory]
    [MemberData(nameof(RefusedLists))]
    public void Refused_argument_lists_are_rejected(string[] args)
    {
        Assert.Throws<ArgumentException>(() => WingetRunner.BuildArguments(args));
    }

    [Fact]
    public void Upgrade_with_id_gets_source_winget_and_exact()
    {
        var result = WingetRunner.BuildArguments(["upgrade", "--id", "Mozilla.Firefox"]);

        Assert.Equal(1, result.Count(a => a == "--source"));
        Assert.Equal("winget", result[result.IndexOf("--source") + 1]);
        Assert.Single(result, a => a == "--exact");
    }

    [Fact]
    public void Upgrade_keeps_a_given_winget_source_once()
    {
        var result = WingetRunner.BuildArguments(["upgrade", "--source", "winget"]);

        Assert.Equal(1, result.Count(a => a == "--source"));
        Assert.Equal("winget", result[result.IndexOf("--source") + 1]);
    }

    [Fact]
    public void Install_does_not_get_a_source_flag()
    {
        var result = WingetRunner.BuildArguments(["install", "--id", "7zip.7zip"]);

        Assert.DoesNotContain("--source", result);
    }
}
