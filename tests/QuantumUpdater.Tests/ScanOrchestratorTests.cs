using System.Collections.Concurrent;
using System.Diagnostics;
using QuantumUpdater.Services.Scan;
using Xunit;

namespace QuantumUpdater.Tests;

public class ScanOrchestratorTests
{
    // Every test stops after this time, so no test can hang.
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private sealed class FakeJob : IScanJob
    {
        private readonly Func<IProgress<ScanProgress>, CancellationToken, Task<ScanResult>> _body;

        public FakeJob(string id, Func<IProgress<ScanProgress>, CancellationToken, Task<ScanResult>> body, params string[] dependsOn)
        {
            Id = id;
            Title = id;
            DependsOn = dependsOn;
            _body = body;
        }

        public string Id { get; }

        public string Title { get; }

        public IReadOnlyList<string> DependsOn { get; }

        public Task<ScanResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken token)
        {
            return _body(progress, token);
        }
    }

    private static Func<IProgress<ScanProgress>, CancellationToken, Task<ScanResult>> Succeeds(int milliseconds)
    {
        return async (progress, token) =>
        {
            await Task.Delay(milliseconds, token);
            return ScanResult.Done("ok");
        };
    }

    private static ScanJobStatus Find(ScanOrchestrator orchestrator, string id)
    {
        return orchestrator.Jobs.Single(j => j.Id == id);
    }

    [Fact]
    public async Task Three_jobs_of_one_second_run_in_parallel()
    {
        var jobs = new[]
        {
            new FakeJob("a", Succeeds(1000)),
            new FakeJob("b", Succeeds(1000)),
            new FakeJob("c", Succeeds(1000)),
        };
        var orchestrator = new ScanOrchestrator(jobs, a => a());
        using var cts = new CancellationTokenSource(Limit);

        var clock = Stopwatch.StartNew();
        await orchestrator.RunAsync(cts.Token).WaitAsync(Limit);
        clock.Stop();

        Assert.True(clock.ElapsedMilliseconds < 1600, $"Took {clock.ElapsedMilliseconds} ms.");
        Assert.All(orchestrator.Jobs, j => Assert.Equal(ScanJobState.Done, j.State));
    }

    [Fact]
    public async Task Throwing_job_fails_with_its_message_and_others_finish()
    {
        var jobs = new[]
        {
            new FakeJob("bad", (p, t) => throw new InvalidOperationException("Disk not ready")),
            new FakeJob("good", Succeeds(100)),
        };
        var orchestrator = new ScanOrchestrator(jobs, a => a());
        using var cts = new CancellationTokenSource(Limit);

        await orchestrator.RunAsync(cts.Token).WaitAsync(Limit);

        var bad = Find(orchestrator, "bad");
        Assert.Equal(ScanJobState.Failed, bad.State);
        Assert.Equal("Disk not ready", bad.Reason);
        Assert.Equal(ScanJobState.Done, Find(orchestrator, "good").State);
    }

    [Fact]
    public async Task Slow_job_is_stopped_at_the_time_limit()
    {
        var jobs = new[] { new FakeJob("slow", Succeeds(20000)) };
        var orchestrator = new ScanOrchestrator(jobs, a => a(), TimeSpan.FromSeconds(1));
        using var cts = new CancellationTokenSource(Limit);

        var clock = Stopwatch.StartNew();
        await orchestrator.RunAsync(cts.Token).WaitAsync(Limit);
        clock.Stop();

        Assert.True(clock.ElapsedMilliseconds < 2000, $"Took {clock.ElapsedMilliseconds} ms.");
        var slow = Find(orchestrator, "slow");
        Assert.Equal(ScanJobState.Failed, slow.State);
        Assert.Equal("Took too long", slow.Reason);
    }

    [Fact]
    public async Task Completed_fires_exactly_once_and_not_again_on_retry()
    {
        var calls = new int[1];
        var jobs = new[]
        {
            new FakeJob("flaky", (p, t) =>
            {
                int n = Interlocked.Increment(ref calls[0]);
                return Task.FromResult(n == 1 ? ScanResult.Failed("First try failed") : ScanResult.Done("ok"));
            }),
            new FakeJob("steady", Succeeds(50)),
        };
        var orchestrator = new ScanOrchestrator(jobs, a => a());
        var completed = new int[1];
        orchestrator.Completed += () => Interlocked.Increment(ref completed[0]);
        using var cts = new CancellationTokenSource(Limit);

        await orchestrator.RunAsync(cts.Token).WaitAsync(Limit);
        Assert.Equal(1, completed[0]);

        await orchestrator.RetryAsync("flaky", cts.Token).WaitAsync(Limit);
        Assert.Equal(ScanJobState.Done, Find(orchestrator, "flaky").State);
        Assert.Equal(1, completed[0]);
    }

    [Fact]
    public async Task Overall_percent_never_goes_down()
    {
        var calls = new int[1];
        var jobs = new[]
        {
            new FakeJob("reader", async (p, t) =>
            {
                for (int i = 1; i <= 4; i++)
                {
                    p.Report(new ScanProgress(Step: 0, StepCount: 0, Items: i, ItemTotal: 4));
                    await Task.Delay(20, t);
                }

                return ScanResult.Done("read");
            }),
            new FakeJob("flaky", (p, t) =>
            {
                int n = Interlocked.Increment(ref calls[0]);
                return Task.FromResult(n == 1 ? ScanResult.Failed("First try failed") : ScanResult.Done("ok"));
            }),
        };
        var orchestrator = new ScanOrchestrator(jobs, a => a());
        var seen = new ConcurrentQueue<double>();
        orchestrator.Changed += _ => seen.Enqueue(orchestrator.OverallPercent);
        using var cts = new CancellationTokenSource(Limit);

        await orchestrator.RunAsync(cts.Token).WaitAsync(Limit);
        await orchestrator.RetryAsync("flaky", cts.Token).WaitAsync(Limit);

        double previous = -1;
        foreach (double value in seen)
        {
            Assert.True(value >= previous, $"Percent went from {previous} to {value}.");
            previous = value;
        }

        Assert.NotEmpty(seen);
        Assert.Equal(100.0, orchestrator.OverallPercent);
    }

    [Fact]
    public async Task Dependent_job_starts_only_after_its_dependency_ends()
    {
        var log = new ConcurrentQueue<string>();
        var jobs = new[]
        {
            new FakeJob("first", async (p, t) =>
            {
                await Task.Delay(300, t);
                log.Enqueue("first-end");
                return ScanResult.Done("ok");
            }),
            new FakeJob("second", (p, t) =>
            {
                log.Enqueue("second-start");
                return Task.FromResult(ScanResult.Done("ok"));
            }, "first"),
        };
        var orchestrator = new ScanOrchestrator(jobs, a => a());
        using var cts = new CancellationTokenSource(Limit);

        await orchestrator.RunAsync(cts.Token).WaitAsync(Limit);

        Assert.Equal(new[] { "first-end", "second-start" }, log.ToArray());
    }

    [Fact]
    public void Circular_dependency_throws_argument_exception()
    {
        var jobs = new[]
        {
            new FakeJob("a", Succeeds(10), "b"),
            new FakeJob("b", Succeeds(10), "a"),
        };

        Assert.Throws<ArgumentException>(() => new ScanOrchestrator(jobs, a => a()));
    }

    [Fact]
    public void Duplicate_id_and_unknown_dependency_throw_argument_exception()
    {
        var duplicate = new[]
        {
            new FakeJob("same", Succeeds(10)),
            new FakeJob("same", Succeeds(10)),
        };
        var unknown = new[] { new FakeJob("a", Succeeds(10), "missing") };

        Assert.Throws<ArgumentException>(() => new ScanOrchestrator(duplicate, a => a()));
        Assert.Throws<ArgumentException>(() => new ScanOrchestrator(unknown, a => a()));
    }

    [Fact]
    public async Task Retry_turns_a_failed_job_into_done_and_runs_only_that_job()
    {
        var flakyCalls = new int[1];
        var steadyCalls = new int[1];
        var jobs = new[]
        {
            new FakeJob("flaky", (p, t) =>
            {
                int n = Interlocked.Increment(ref flakyCalls[0]);
                return Task.FromResult(n == 1 ? ScanResult.Failed("First try failed") : ScanResult.Done("3 updates"));
            }),
            new FakeJob("steady", (p, t) =>
            {
                Interlocked.Increment(ref steadyCalls[0]);
                return Task.FromResult(ScanResult.Done("ok"));
            }),
        };
        var orchestrator = new ScanOrchestrator(jobs, a => a());
        using var cts = new CancellationTokenSource(Limit);

        await orchestrator.RunAsync(cts.Token).WaitAsync(Limit);
        Assert.Equal(ScanJobState.Failed, Find(orchestrator, "flaky").State);
        Assert.Equal("First try failed", Find(orchestrator, "flaky").Reason);

        await orchestrator.RetryAsync("flaky", cts.Token).WaitAsync(Limit);

        var flaky = Find(orchestrator, "flaky");
        Assert.Equal(ScanJobState.Done, flaky.State);
        Assert.Equal("3 updates", flaky.Summary);
        Assert.Null(flaky.Reason);
        Assert.Equal(2, flakyCalls[0]);
        Assert.Equal(1, steadyCalls[0]);
    }
}
