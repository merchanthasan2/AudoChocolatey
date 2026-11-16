using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuantumUpdater.Services.Scan;

/// <summary>A snapshot of one scan job. Read it on the screen thread only.</summary>
public sealed class ScanJobStatus
{
    internal ScanJobStatus(string id, string title, ScanJobState state, double? fraction, string? summary, string? reason, TimeSpan? elapsed)
    {
        Id = id;
        Title = title;
        State = state;
        Fraction = fraction;
        Summary = summary;
        Reason = reason;
        Elapsed = elapsed;
    }

    public string Id { get; }

    public string Title { get; }

    public ScanJobState State { get; }

    /// <summary>The share of work done, from 0 to 1. Null when the job reports no number.</summary>
    public double? Fraction { get; }

    /// <summary>One short result line when Done. Null otherwise.</summary>
    public string? Summary { get; }

    /// <summary>A plain English reason when Failed. Null otherwise.</summary>
    public string? Reason { get; }

    /// <summary>Time the job has run. Null when the job never started.</summary>
    public TimeSpan? Elapsed { get; }
}

/// <summary>
/// Runs the Getting ready scan jobs. All jobs start at once, except jobs that wait for other jobs.
/// No WinUI types here. The app passes a function that moves work onto the screen thread.
/// </summary>
public sealed class ScanOrchestrator
{
    private static readonly TimeSpan DefaultJobTimeout = TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly Action<Action> _post;
    private readonly TimeSpan _jobTimeout;
    private readonly List<JobEntry> _entries = new();
    private bool _started;
    private bool _completedRaised;
    private double _maxPercent;

    /// <param name="jobs">The jobs to run. Ids must be unique. Dependencies must exist and must not form a cycle.</param>
    /// <param name="post">Runs an event on the screen thread. Tests pass <c>a =&gt; a()</c>.</param>
    /// <param name="jobTimeout">The time limit for each job. Null means 15 seconds.</param>
    public ScanOrchestrator(IEnumerable<IScanJob> jobs, Action<Action> post, TimeSpan? jobTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        _post = post ?? throw new ArgumentNullException(nameof(post));

        _jobTimeout = jobTimeout ?? DefaultJobTimeout;
        if (_jobTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(jobTimeout), "The job time limit must be more than zero.");
        }

        var byId = new Dictionary<string, JobEntry>(StringComparer.Ordinal);
        foreach (IScanJob job in jobs)
        {
            ArgumentNullException.ThrowIfNull(job);
            if (string.IsNullOrEmpty(job.Id))
            {
                throw new ArgumentException("A scan job needs an id.", nameof(jobs));
            }

            if (byId.ContainsKey(job.Id))
            {
                throw new ArgumentException($"Two scan jobs have the id '{job.Id}'.", nameof(jobs));
            }

            var entry = new JobEntry(job);
            byId.Add(job.Id, entry);
            _entries.Add(entry);
        }

        foreach (JobEntry entry in _entries)
        {
            IReadOnlyList<string>? dependsOn = entry.Job.DependsOn;
            if (dependsOn is null)
            {
                continue;
            }

            foreach (string id in dependsOn)
            {
                if (!byId.TryGetValue(id, out JobEntry? dependency))
                {
                    throw new ArgumentException($"Scan job '{entry.Id}' waits for '{id}', which is not a scan job.", nameof(jobs));
                }

                entry.Dependencies.Add(dependency);
            }
        }

        CheckForCycles();
        Recompute();
    }

    /// <summary>Raised on every job change. Runs through the post function.</summary>
    public event Action<ScanJobStatus>? Changed;

    /// <summary>Raised one time, when every job is Done or Failed. Runs through the post function.</summary>
    public event Action? Completed;

    /// <summary>Snapshot of every job, in the order they were registered.</summary>
    public IReadOnlyList<ScanJobStatus> Jobs
    {
        get
        {
            lock (_gate)
            {
                return _entries.Select(ToStatus).ToList();
            }
        }
    }

    /// <summary>The overall percent, from 0 to 100. It never goes down.</summary>
    public double OverallPercent
    {
        get
        {
            lock (_gate)
            {
                return _maxPercent;
            }
        }
    }

    /// <summary>True when every job is Done or Failed.</summary>
    public bool IsComplete
    {
        get
        {
            lock (_gate)
            {
                return AllFinished();
            }
        }
    }

    /// <summary>Starts every job and returns when every job is Done or Failed. Can be called one time only.</summary>
    public async Task RunAsync(CancellationToken token)
    {
        lock (_gate)
        {
            if (_started)
            {
                throw new InvalidOperationException("The scan already started.");
            }

            _started = true;
        }

        Task[] runs = _entries.Select(entry => RunEntryAsync(entry, token)).ToArray();
        await Task.WhenAll(runs).ConfigureAwait(false);
        lock (_gate)
        {
            RaiseCompletedIfAllFinished();
        }
    }

    /// <summary>Runs one Failed job again. Other jobs are not run. Does nothing when the job is not Failed.</summary>
    public async Task RetryAsync(string jobId, CancellationToken token)
    {
        JobEntry entry = _entries.FirstOrDefault(e => e.Id == jobId)
            ?? throw new ArgumentException($"There is no scan job with the id '{jobId}'.", nameof(jobId));

        await RunJobAsync(entry, token, onlyIfFailed: true).ConfigureAwait(false);
    }

    private async Task RunEntryAsync(JobEntry entry, CancellationToken token)
    {
        try
        {
            if (entry.Dependencies.Count > 0)
            {
                Task waitFor = Task.WhenAll(entry.Dependencies.Select(d => d.Finished.Task));
                await waitFor.WaitAsync(token).ConfigureAwait(false);
            }

            await RunJobAsync(entry, token, onlyIfFailed: false).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Stopped while the job was still waiting for other jobs.
            SetFinished(entry, success: false, summary: null, reason: "Stopped");
        }
        finally
        {
            entry.Finished.TrySetResult();
        }
    }

    private async Task RunJobAsync(JobEntry entry, CancellationToken token, bool onlyIfFailed)
    {
        int run;
        lock (_gate)
        {
            if (onlyIfFailed && entry.State != ScanJobState.Failed)
            {
                return;
            }

            run = ++entry.Run;
            entry.State = ScanJobState.Working;
            entry.Fraction = null;
            entry.Summary = null;
            entry.Reason = null;
            entry.Stopwatch.Restart();
            entry.Started = true;
            Recompute();
            Raise(entry);
        }

        bool success = false;
        string? summary = null;
        string? reason = null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cts.Token.Register(() => stopped.TrySetResult());
        var progress = new ProgressSink(p => ReportProgress(entry, run, p));

        try
        {
            token.ThrowIfCancellationRequested();
            cts.CancelAfter(_jobTimeout);

            // Run the job on a thread pool thread, so sync work in the job cannot block this caller.
            Task<ScanResult> work = Task.Run(() => entry.Job.RunAsync(progress, cts.Token));
            Task timeLimit = Task.Delay(_jobTimeout, cts.Token);
            Task first = await Task.WhenAny(work, stopped.Task, timeLimit).ConfigureAwait(false);
            if (first == work)
            {
                ScanResult? result = await work.ConfigureAwait(false);
                if (result is null)
                {
                    reason = "No result was returned.";
                }
                else if (result.Success)
                {
                    success = true;
                    summary = result.Summary;
                }
                else
                {
                    reason = result.Reason ?? "No reason was given.";
                }
            }
            else
            {
                reason = token.IsCancellationRequested ? "Stopped" : "Took too long";
            }
        }
        catch (OperationCanceledException)
        {
            if (token.IsCancellationRequested)
            {
                reason = "Stopped";
            }
            else if (cts.IsCancellationRequested)
            {
                reason = "Took too long";
            }
            else
            {
                reason = "Stopped";
            }
        }
        catch (Exception ex)
        {
            reason = string.IsNullOrWhiteSpace(ex.Message) ? "An unknown error stopped the job." : ex.Message;
        }

        SetFinished(entry, success, summary, reason);
    }

    private void SetFinished(JobEntry entry, bool success, string? summary, string? reason)
    {
        lock (_gate)
        {
            entry.Stopwatch.Stop();
            entry.State = success ? ScanJobState.Done : ScanJobState.Failed;
            entry.Summary = success ? summary : null;
            entry.Reason = success ? null : reason;
            if (success)
            {
                entry.Fraction = 1.0;
            }

            Recompute();
            Raise(entry);
            RaiseCompletedIfAllFinished();
        }
    }

    private void ReportProgress(JobEntry entry, int run, ScanProgress progress)
    {
        lock (_gate)
        {
            if (entry.State != ScanJobState.Working || entry.Run != run)
            {
                return;
            }

            double? fraction = progress.Fraction;
            if (entry.Fraction == fraction)
            {
                return;
            }

            entry.Fraction = fraction;
            Recompute();
            Raise(entry);
        }
    }

    // Call only while holding _gate.
    private void Recompute()
    {
        if (_entries.Count == 0)
        {
            _maxPercent = 100.0;
            return;
        }

        double sum = 0;
        foreach (JobEntry entry in _entries)
        {
            sum += entry.State switch
            {
                ScanJobState.Done or ScanJobState.Failed => 1.0,
                ScanJobState.Working => entry.Fraction ?? 0.0,
                _ => 0.0,
            };
        }

        double percent = sum / _entries.Count * 100.0;
        _maxPercent = Math.Clamp(Math.Max(_maxPercent, percent), 0.0, 100.0);
    }

    // Call only while holding _gate.
    private bool AllFinished()
    {
        return _entries.All(e => e.State is ScanJobState.Done or ScanJobState.Failed);
    }

    // Call only while holding _gate. Raises Completed one time only.
    private void RaiseCompletedIfAllFinished()
    {
        if (_completedRaised || !AllFinished())
        {
            return;
        }

        _completedRaised = true;
        _post(() => Completed?.Invoke());
    }

    // Call only while holding _gate, so events keep their order.
    private void Raise(JobEntry entry)
    {
        ScanJobStatus snapshot = ToStatus(entry);
        _post(() => Changed?.Invoke(snapshot));
    }

    // Call only while holding _gate.
    private static ScanJobStatus ToStatus(JobEntry entry)
    {
        return new ScanJobStatus(
            entry.Id,
            entry.Title,
            entry.State,
            entry.Fraction,
            entry.Summary,
            entry.Reason,
            entry.Started ? entry.Stopwatch.Elapsed : null);
    }

    private void CheckForCycles()
    {
        var state = new Dictionary<JobEntry, int>();
        foreach (JobEntry entry in _entries)
        {
            Visit(entry, state);
        }
    }

    private static void Visit(JobEntry entry, Dictionary<JobEntry, int> state)
    {
        if (state.TryGetValue(entry, out int mark))
        {
            if (mark == 1)
            {
                throw new ArgumentException($"Scan job '{entry.Id}' waits for itself through other jobs.", "jobs");
            }

            return;
        }

        state[entry] = 1;
        foreach (JobEntry dependency in entry.Dependencies)
        {
            Visit(dependency, state);
        }

        state[entry] = 2;
    }

    private sealed class JobEntry
    {
        public JobEntry(IScanJob job)
        {
            Job = job;
            Id = job.Id;
            Title = job.Title;
        }

        public IScanJob Job { get; }

        public string Id { get; }

        public string Title { get; }

        public ScanJobState State { get; set; } = ScanJobState.Waiting;

        public double? Fraction { get; set; }

        public string? Summary { get; set; }

        public string? Reason { get; set; }

        public bool Started { get; set; }

        /// <summary>Counts the runs of this job. A late progress report from an old run is ignored.</summary>
        public int Run { get; set; }

        public Stopwatch Stopwatch { get; } = new();

        /// <summary>Set when the job leaves the wait for other jobs. Dependent jobs wait on it.</summary>
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<JobEntry> Dependencies { get; } = new();
    }

    private sealed class ProgressSink : IProgress<ScanProgress>
    {
        private readonly Action<ScanProgress> _report;

        public ProgressSink(Action<ScanProgress> report)
        {
            _report = report;
        }

        public void Report(ScanProgress value)
        {
            _report(value);
        }
    }
}
