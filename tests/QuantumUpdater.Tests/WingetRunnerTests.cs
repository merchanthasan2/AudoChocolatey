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

        var result = await NewRunner().RunAsync(["--sleep"], LongTimeout, null, cts.Token);

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

        var result = await NewRunner().RunAsync(["--sleep"], TimeSpan.FromSeconds(1), null, CancellationToken.None);

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

        var result = await NewRunner().RunAsync(["alpha"], LongTimeout, seen.Add, CancellationToken.None);

        Assert.Equal(ExpectedLines, result.Lines);
        Assert.Equal(ExpectedLines, seen);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task Exit_code_is_returned()
    {
        var result = await NewRunner().RunAsync(["--fail"], LongTimeout, null, CancellationToken.None);

        Assert.Equal(7, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task Exact_is_added_when_id_is_present()
    {
        var result = await NewRunner().RunAsync(["--id", "Git.Git"], LongTimeout, null, CancellationToken.None);

        Assert.Single(result.Lines, line => line == "--exact");
    }

    [Fact]
    public async Task Disable_interactivity_is_added_once()
    {
        var result = await NewRunner().RunAsync(["--disable-interactivity"], LongTimeout, null, CancellationToken.None);

        Assert.Single(result.Lines, line => line == "--disable-interactivity");
        Assert.Single(result.Lines, line => line == "--accept-source-agreements");
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

    [Theory]
    [InlineData("-i")]
    [InlineData("-e")]
    [InlineData("-q")]
    public async Task Short_flags_are_rejected_before_start(string flag)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewRunner().RunAsync(["uninstall", flag, "Mozilla.Firefox"], LongTimeout, null, CancellationToken.None));
    }
}
