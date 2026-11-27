namespace QuantumUpdater.Services.Winget;

/// <summary>
/// Mirror of the winget COM PackageInstallProgressState values.
/// Pure C# enum so tests do not reference WinRT COM projections.
/// </summary>
public enum ComState
{
    Queued,
    Downloading,
    Installing,
    PostInstall,
    Finished,
}

/// <summary>
/// Maps winget COM install/upgrade progress events to <see cref="ActionProgress"/>.
/// Rules (D8, 46.7.90):
/// - Queued -> "Waiting for Windows", percent null.
/// - Downloading -> percent = BytesDownloaded * 100 / BytesRequired only when BytesRequired > 0.
/// - Installing -> percent only when InstallationProgress > 0 (0..1 or 0..100 normalized); else null.
/// - PostInstall -> "Finishing", percent null.
/// - Finished -> "Done", percent 100.
/// - Percent never goes backwards within the same phase.
/// </summary>
public sealed class ComProgressMap
{
    private ComState? _lastState;
    private int _maxPercent;

    public ActionProgress Map(
        ComState state,
        ulong bytesDownloaded,
        ulong bytesRequired,
        double installationProgress)
    {
        if (_lastState != state)
        {
            _lastState = state;
            _maxPercent = 0;
        }

        switch (state)
        {
            case ComState.Queued:
                return new ActionProgress("Waiting for Windows", null);

            case ComState.Downloading:
                if (bytesRequired == 0)
                {
                    return new ActionProgress("Downloading", _maxPercent > 0 ? _maxPercent : null);
                }

                int dlPct = (int)Math.Clamp((double)bytesDownloaded * 100.0 / bytesRequired, 0.0, 100.0);
                _maxPercent = Math.Max(_maxPercent, dlPct);
                return new ActionProgress("Downloading", _maxPercent);

            case ComState.Installing:
                if (installationProgress <= 0.0 || double.IsNaN(installationProgress) || double.IsInfinity(installationProgress))
                {
                    return new ActionProgress("Installing", _maxPercent > 0 ? _maxPercent : null);
                }

                double scaled = installationProgress <= 1.0
                    ? installationProgress * 100.0
                    : installationProgress;
                int instPct = (int)Math.Clamp(Math.Round(scaled), 1.0, 100.0);
                _maxPercent = Math.Max(_maxPercent, instPct);
                return new ActionProgress("Installing", _maxPercent);

            case ComState.PostInstall:
                return new ActionProgress("Finishing", null);

            case ComState.Finished:
                _maxPercent = 100;
                return new ActionProgress("Done", 100);

            default:
                return new ActionProgress("Working", null);
        }
    }
}
