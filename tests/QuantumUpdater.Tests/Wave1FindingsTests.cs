using System.Collections.Concurrent;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

/// <summary>Tests for the 46.16 Wave 1 low findings W5, W6, W7, W8, W9, W10, W13 and W16.</summary>
public class Wave1FindingsTests
{
    private const string Profile = "C:\\Users\\Owner";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    // ---- W5: re-locate winget after an App Installer update ----

    [Fact]
    public void W5_cached_path_is_used_while_it_is_still_valid()
    {
        int lookups = 0;
        var path = new RecheckedPath(() => { lookups++; return "C:\\A\\winget.exe"; }, _ => true);

        Assert.Equal("C:\\A\\winget.exe", path.Get());
        Assert.Equal("C:\\A\\winget.exe", path.Get());
        Assert.Equal(1, lookups);
    }

    [Fact]
    public void W5_path_is_found_again_when_the_cached_file_is_gone()
    {
        var existing = new HashSet<string> { "C:\\Old\\winget.exe" };
        string next = "C:\\Old\\winget.exe";
        int lookups = 0;
        var path = new RecheckedPath(() => { lookups++; return next; }, p => p is not null && existing.Contains(p));

        Assert.Equal("C:\\Old\\winget.exe", path.Get());

        // App Installer updated: the old folder is deleted, and the package now points to a new one.
        existing.Clear();
        existing.Add("C:\\New\\winget.exe");
        next = "C:\\New\\winget.exe";

        Assert.Equal("C:\\New\\winget.exe", path.Get());
        Assert.Equal(2, lookups);
    }

    [Fact]
    public void W5_a_found_path_that_is_not_valid_is_not_returned()
    {
        var path = new RecheckedPath(() => "C:\\Temp\\winget.exe", _ => false);

        Assert.Null(path.Get());
    }

    [Fact]
    public void W5_not_found_is_looked_up_again_on_the_next_read()
    {
        string? next = null;
        var path = new RecheckedPath(() => next, p => p is not null);

        Assert.Null(path.Get());
        next = "C:\\A\\winget.exe";
        Assert.Equal("C:\\A\\winget.exe", path.Get());
    }

    // ---- W6: trust check before the version pick ----

    private sealed record Candidate(string Name, Version Version, bool Trusted);

    [Fact]
    public void W6_newer_untrusted_package_does_not_hide_a_trusted_one()
    {
        var candidates = new[]
        {
            new Candidate("trusted-old", new Version(1, 20, 0, 0), true),
            new Candidate("untrusted-new", new Version(9, 0, 0, 0), false),
        };

        var picked = WingetPathRules.HighestTrusted(candidates, c => c.Version, c => c.Trusted);

        Assert.Equal("trusted-old", picked?.Name);
    }

    [Fact]
    public void W6_highest_trusted_version_wins()
    {
        var candidates = new[]
        {
            new Candidate("a", new Version(1, 20, 0, 0), true),
            new Candidate("b", new Version(1, 22, 0, 0), true),
            new Candidate("c", new Version(1, 21, 0, 0), true),
        };

        Assert.Equal("b", WingetPathRules.HighestTrusted(candidates, c => c.Version, c => c.Trusted)?.Name);
    }

    [Fact]
    public void W6_no_trusted_package_gives_null()
    {
        var candidates = new[] { new Candidate("x", new Version(2, 0), false) };

        Assert.Null(WingetPathRules.HighestTrusted(candidates, c => c.Version, c => c.Trusted));
    }

    [Fact]
    public void W6_a_trust_check_that_throws_counts_as_untrusted()
    {
        var candidates = new[]
        {
            new Candidate("throws", new Version(5, 0), true),
            new Candidate("ok", new Version(1, 0), true),
        };

        var picked = WingetPathRules.HighestTrusted(
            candidates,
            c => c.Version,
            c => c.Name == "throws" ? throw new UnauthorizedAccessException() : c.Trusted);

        Assert.Equal("ok", picked?.Name);
    }

    // ---- W7 and W16: one result helper that keeps stderr ----

    private static RunResult Run(int exitCode, IReadOnlyList<string>? lines = null, IReadOnlyList<string>? errors = null, bool timedOut = false, bool cancelled = false, bool stopIncomplete = false)
    {
        return new RunResult(exitCode, timedOut, cancelled, lines ?? [], stopIncomplete, errors);
    }

    [Theory]
    [InlineData(ActionKind.Update, "Updated.", "The update was cancelled.", "The update took too long and was stopped.")]
    [InlineData(ActionKind.Install, "Installed.", "The install was cancelled.", "The install took too long and was stopped.")]
    [InlineData(ActionKind.Remove, "Removed.", "The removal was cancelled.", "The removal took too long and was stopped.")]
    public void W16_kind_sets_the_fixed_texts(ActionKind kind, string done, string cancelled, string timedOut)
    {
        Assert.Equal(done, RunOutcome.FromRun(Run(0), Profile, kind).Message);
        Assert.Equal(cancelled, RunOutcome.FromRun(Run(-1, cancelled: true), Profile, kind).Message);
        Assert.Equal(timedOut, RunOutcome.FromRun(Run(-1, timedOut: true), Profile, kind).Message);
    }

    [Fact]
    public void W7_failed_action_keeps_stderr_in_the_text()
    {
        var result = RunOutcome.FromRun(
            Run(unchecked((int)0x8A150011), lines: ["Downloading"], errors: ["Installer hash does not match."]),
            Profile,
            ActionKind.Install);

        Assert.False(result.Success);
        Assert.Equal("Installer hash does not match.", result.Detail);
        Assert.Contains("Installer hash does not match.", result.Text);
        Assert.StartsWith(result.Message, result.Text);
    }

    [Fact]
    public void W7_text_is_the_message_when_there_is_no_detail()
    {
        var result = RunOutcome.FromRun(Run(0), Profile, ActionKind.Remove);

        Assert.Equal(result.Message, result.Text);
    }

    [Fact]
    public void W16_not_started_is_marked()
    {
        var result = RunOutcome.NotStarted("Package id is required.");

        Assert.False(result.Success);
        Assert.True(result.NotStarted);
        Assert.Equal("Package id is required.", result.Message);
        Assert.Null(result.StillRunning);
    }

    [Fact]
    public void W7_list_error_keeps_stderr_and_the_code_without_the_profile_path()
    {
        string? error = RunOutcome.ListError(
            Run(unchecked((int)0x8A15000F), errors: ["Failed to open source at C:\\Users\\Owner\\AppData\\x"]),
            0,
            Profile);

        Assert.NotNull(error);
        Assert.Contains("0x8A15000F", error);
        Assert.Contains("Failed to open source", error);
        Assert.DoesNotContain("C:\\Users\\Owner", error);
    }

    [Fact]
    public void W7_list_with_rows_is_not_an_error_even_with_an_exit_code()
    {
        Assert.Null(RunOutcome.ListError(Run(5, errors: ["warning"]), 3, Profile));
    }

    [Fact]
    public void W7_empty_list_without_stderr_is_not_an_error()
    {
        Assert.Null(RunOutcome.ListError(Run(unchecked((int)0x8A150014), lines: ["No installed package found matching input criteria."]), 0, Profile));
    }

    [Fact]
    public void W16_list_cancel_timeout_and_stop_texts()
    {
        Assert.Equal(RunOutcome.ListCancelledMessage, RunOutcome.ListError(Run(-1, cancelled: true), 0, Profile));
        Assert.Equal(RunOutcome.ListTimedOutMessage, RunOutcome.ListError(Run(-1, timedOut: true), 0, Profile));
        Assert.Equal(RunOutcome.StopIncompleteMessage, RunOutcome.ListError(Run(-1, timedOut: true, stopIncomplete: true), 0, Profile));
    }

    // ---- W8: install and uninstall name exactly one package ----

    [Theory]
    [InlineData("install")]
    [InlineData("uninstall")]
    [InlineData("install", "--silent")]
    [InlineData("uninstall", "--accept-source-agreements")]
    [InlineData("install", "--source", "winget")]
    public void W8_install_and_uninstall_without_an_id_are_refused(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => WingetRunner.BuildArguments(args));
    }

    [Fact]
    public void W8_upgrade_without_an_id_is_still_the_list_form()
    {
        var result = WingetRunner.BuildArguments(["upgrade", "--include-unknown"]);

        Assert.Equal("upgrade", result[0]);
        Assert.DoesNotContain("--exact", result);
    }

    // ---- W9: faults of abandoned tasks are observed ----

    [Fact]
    public async Task W9_observe_faults_reads_a_later_fault()
    {
        var source = new TaskCompletionSource();
        var observer = WingetRunner.ObserveFaults(source.Task);

        source.SetException(new ObjectDisposedException("pipe"));

        await observer.WaitAsync(WaitLimit);
        Assert.True(observer.IsCompletedSuccessfully);
    }

    [Fact]
    public void W9_observe_faults_does_not_run_for_a_task_that_ends_well()
    {
        var observer = WingetRunner.ObserveFaults(Task.CompletedTask);

        Assert.True(observer.IsCanceled);
    }

    // ---- W10: stricter header and footer parsing ----

    private static string Spaces(int count) => new(' ', count);

    private static string Header() =>
        "Name" + Spaces(20) + "Id" + Spaces(22) + "Version" + Spaces(7) + "Available" + Spaces(3) + "Source";

    private static string Separator() => new('-', 80);

    private static string Row(string name, string id, string version, string available = "", string source = "winget") =>
        name.PadRight(24) + id.PadRight(24) + version.PadRight(14) + available.PadRight(12) + source;

    [Fact]
    public void W10_display_name_that_looks_like_a_header_does_not_change_the_columns()
    {
        // An app installed for the user can choose any DisplayName, including a fake header with other column places.
        var text = string.Join("\r\n",
            Header(),
            Separator(),
            Row("Acme Tool", "Acme.Tool", "3.1", "3.2"),
            "Id Name                    Version Source Available",
            Row("Other App", "Other.App", "1.0", "1.1"));

        var rows = WingetTextTable.Parse(text);

        Assert.Contains(rows, r => r.Id == "Acme.Tool" && r.Version == "3.1");
        Assert.Contains(rows, r => r.Id == "Other.App" && r.Version == "1.0" && r.Available == "1.1");
    }

    [Fact]
    public void W10_header_without_a_separator_starts_no_table()
    {
        var text = string.Join("\r\n",
            Header(),
            Row("Acme Tool", "Acme.Tool", "3.1"));

        Assert.Empty(WingetTextTable.Parse(text));
    }

    [Theory]
    [InlineData("1 package(s) have pins that prevent upgrade. Use the 'winget pin' command to view and edit pins.")]
    [InlineData("2 packages have pins that prevent upgrade.")]
    [InlineData("3 package(s) have version numbers that cannot be determined. Use --include-unknown to see all results.")]
    [InlineData("1 upgrades available.")]
    public void W10_footer_lines_are_not_rows(string footer)
    {
        var text = string.Join("\r\n",
            Header(),
            Separator(),
            Row("Acme Tool", "Acme.Tool", "3.1", "3.2"),
            footer);

        var row = Assert.Single(WingetTextTable.Parse(text));
        Assert.Equal("Acme.Tool", row.Id);
    }

    [Fact]
    public void W10_a_line_without_a_version_is_not_a_row()
    {
        var text = string.Join("\r\n",
            Header(),
            Separator(),
            Row("Acme Tool", "Acme.Tool", "3.1"),
            "Some wrapped text that is not a row");

        var row = Assert.Single(WingetTextTable.Parse(text));
        Assert.Equal("Acme.Tool", row.Id);
    }

    [Fact]
    public void W10_a_second_real_table_still_reads()
    {
        var text = string.Join("\r\n",
            Header(),
            Separator(),
            Row("Acme Tool", "Acme.Tool", "3.1", "3.2"),
            "The following packages have an upgrade available, but require explicit targeting for upgrade:",
            Header(),
            Separator(),
            Row("Pinned App", "Pinned.App", "2.0", "2.5"));

        var rows = WingetTextTable.Parse(text);

        Assert.Equal(new[] { "Acme.Tool", "Pinned.App" }, rows.Select(r => r.Id).ToArray());
    }

    // ---- W13: events after unlock, snapshots, and a post that throws ----

    [Fact]
    public async Task W13_a_post_that_throws_does_not_stop_the_queue()
    {
        int posts = 0;
        var queue = new ActionQueue(a =>
        {
            if (Interlocked.Increment(ref posts) % 2 == 1)
            {
                throw new InvalidOperationException("dispatcher gone");
            }

            a();
        });

        var first = queue.Enqueue("a", ActionKind.Update, "A", (p, ct) => Task.FromResult(new ActionOutcome(true, null)));
        var second = queue.Enqueue("b", ActionKind.Update, "B", (p, ct) => Task.FromResult(new ActionOutcome(true, null)));

        await WaitUntilAsync(() => !queue.IsBusy);

        Assert.Equal(ActionState.Done, queue.Items.Single(i => i.Id == first.Id).State);
        Assert.Equal(ActionState.Done, queue.Items.Single(i => i.Id == second.Id).State);
    }

    [Fact]
    public async Task W13_a_handler_that_throws_does_not_stop_the_queue()
    {
        var queue = new ActionQueue(a => a());
        queue.Changed += _ => throw new InvalidOperationException("handler bug");

        var item = queue.Enqueue("a", ActionKind.Update, "A", (p, ct) => Task.FromResult(new ActionOutcome(true, null)));
        var next = queue.Enqueue("b", ActionKind.Update, "B", (p, ct) => Task.FromResult(new ActionOutcome(true, null)));

        await WaitUntilAsync(() => !queue.IsBusy);

        Assert.Equal(ActionState.Done, queue.Items.Single(i => i.Id == item.Id).State);
        Assert.Equal(ActionState.Done, queue.Items.Single(i => i.Id == next.Id).State);
    }

    [Fact]
    public async Task W13_events_run_outside_the_queue_lock()
    {
        // The handler reads the queue from another thread. Inside the lock this would wait until the time limit.
        var queue = new ActionQueue(a => a());
        var reads = new ConcurrentQueue<bool>();
        queue.Changed += _ => reads.Enqueue(Task.Run(() => queue.Items.Count).Wait(TimeSpan.FromSeconds(2)));

        queue.Enqueue("a", ActionKind.Update, "A", (p, ct) => Task.FromResult(new ActionOutcome(true, null)));
        await WaitUntilAsync(() => !queue.IsBusy);
        await WaitUntilAsync(() => reads.Count >= 3);

        Assert.All(reads, Assert.True);
    }

    [Fact]
    public async Task W13_event_item_is_a_snapshot_with_the_same_id()
    {
        var queue = new ActionQueue(a => a());
        var seen = new ConcurrentQueue<QueuedAction>();
        queue.Changed += seen.Enqueue;

        var item = queue.Enqueue("a", ActionKind.Install, "A", (p, ct) =>
        {
            p.Report(new ActionProgress("Downloading", 40));
            return Task.FromResult(new ActionOutcome(true, null));
        });
        await WaitUntilAsync(() => !queue.IsBusy);
        await WaitUntilAsync(() => seen.Any(s => s.State == ActionState.Done));

        var states = seen.Select(s => s.State).ToList();
        Assert.Equal(ActionState.Waiting, states[0]);
        Assert.Contains(ActionState.Working, states);
        Assert.Equal(ActionState.Done, states[^1]);
        Assert.All(seen, s => Assert.Equal(item.Id, s.Id));
        Assert.All(seen, s => Assert.NotSame(item, s));

        // The first snapshot kept its values after the live item moved on.
        Assert.Equal(ActionState.Waiting, seen.First().State);
        Assert.Null(seen.First().Percent);
        Assert.Contains(seen, s => s.Phase == "Downloading" && s.Percent == 40);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met within 5 seconds.");
            }

            await Task.Delay(10);
        }
    }
}
