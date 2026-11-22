namespace QuantumUpdater.Services.Scan;

/// <summary>Where a scan job is in its life.</summary>
public enum ScanJobState
{
    Waiting,
    Working,
    Done,
    Failed,
}

/// <summary>
/// Progress of one scan job. A job reports items when it knows the item total.
/// Otherwise it reports steps. The fraction is never computed from time.
/// </summary>
public readonly record struct ScanProgress(int Step, int StepCount, int Items, int ItemTotal)
{
    /// <summary>The share of work done, from 0 to 1. Null means there is no number to show.</summary>
    public double? Fraction
    {
        get
        {
            double raw;
            if (ItemTotal > 0)
            {
                raw = (double)Items / ItemTotal;
            }
            else if (StepCount > 0)
            {
                raw = (double)Step / StepCount;
            }
            else
            {
                return null;
            }

            return Math.Clamp(raw, 0.0, 1.0);
        }
    }
}

/// <summary>The end result of one scan job.</summary>
/// <param name="Success">True when the job is Done. False when it is Failed.</param>
/// <param name="Summary">One short line, for example "7 updates". Empty when Failed.</param>
/// <param name="Reason">A plain English reason when Failed. Null when Done.</param>
public sealed record ScanResult(bool Success, string Summary, string? Reason)
{
    /// <summary>Make a successful result.</summary>
    public static ScanResult Done(string summary) => new(true, summary, null);

    /// <summary>Make a failed result with a plain English reason.</summary>
    public static ScanResult Failed(string reason) => new(false, string.Empty, reason);
}

/// <summary>One job that runs during the Getting ready scan.</summary>
public interface IScanJob
{
    /// <summary>A short unique id, for example "updates".</summary>
    string Id { get; }

    /// <summary>The title shown on the row.</summary>
    string Title { get; }

    /// <summary>Ids of the jobs this job waits for. Empty by default.</summary>
    IReadOnlyList<string> DependsOn => Array.Empty<string>();

    /// <summary>Run the job. Report progress through <paramref name="progress"/>.</summary>
    Task<ScanResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken token);
}
