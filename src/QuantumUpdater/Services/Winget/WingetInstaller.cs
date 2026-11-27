using Microsoft.Management.Deployment;

namespace QuantumUpdater.Services.Winget;

/// <summary>
/// Runs package upgrades and installs via the winget COM API (D9, D10, 46.7.92).
/// Reports real download byte progress and install progress via <see cref="ComProgressMap"/>.
/// Falls back cleanly with <c>NotStarted = true</c> if COM or the catalog cannot start.
/// </summary>
public sealed class WingetInstaller
{
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(10);

    private readonly WingetReader _reader;

    public WingetInstaller(WingetReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>Upgrades an installed package by exact catalog id.</summary>
    public Task<ActionResult> UpgradeAsync(
        string catalogId,
        IProgress<ActionProgress>? progress,
        CancellationToken cancellationToken)
        => ExecuteAsync(isUpgrade: true, catalogId, progress, cancellationToken);

    /// <summary>Installs a catalog package by exact catalog id.</summary>
    public Task<ActionResult> InstallAsync(
        string catalogId,
        IProgress<ActionProgress>? progress,
        CancellationToken cancellationToken)
        => ExecuteAsync(isUpgrade: false, catalogId, progress, cancellationToken);

    private async Task<ActionResult> ExecuteAsync(
        bool isUpgrade,
        string catalogId,
        IProgress<ActionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!PackageId.IsCatalogId(catalogId))
        {
            return RunOutcome.NotStarted(WingetCli.IdInvalidMessage);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new ActionResult(false, false, true, RunOutcome.CancelledMessage, null, null);
        }

        WingetReader.ActionLookup? lookup;
        try
        {
            lookup = await _reader.FindForActionAsync(catalogId, isUpgrade, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ActionResult(false, false, true, RunOutcome.CancelledMessage, null, null);
        }
        catch (WingetReaderException ex)
        {
            return RunOutcome.NotStarted(ex.Message);
        }
        catch (Exception)
        {
            return RunOutcome.NotStarted("The Microsoft catalog could not be reached.");
        }

        if (lookup is null)
        {
            return ComResultMap.NoPackage();
        }

        var options = new InstallOptions
        {
            PackageInstallMode = PackageInstallMode.Silent,
            AcceptPackageAgreements = true,
            AllowUpgradeToUnknownVersion = true,
            AllowHashMismatch = false,
            Force = false,
        };

        var mapper = new ComProgressMap();
        var operation = isUpgrade
            ? lookup.Manager.UpgradePackageAsync(lookup.Package, options)
            : lookup.Manager.InstallPackageAsync(lookup.Package, options);

        if (progress is not null)
        {
            operation.Progress = (_, p) =>
            {
                var mapped = mapper.Map(
                    MapState(p.State),
                    p.BytesDownloaded,
                    p.BytesRequired,
                    p.InstallationProgress);
                progress.Report(mapped);
            };
        }

        var opTask = operation.AsTask();
        using var timeoutCts = new CancellationTokenSource(ActionTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var cancelSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = linkedCts.Token.Register(() => cancelSignal.TrySetResult());

        var completed = await Task.WhenAny(opTask, cancelSignal.Task).ConfigureAwait(false);
        bool cancelledByCallerOrTimeout = false;
        bool endedInTime = true;

        if (completed != opTask)
        {
            cancelledByCallerOrTimeout = true;
            try
            {
                operation.Cancel();
            }
            catch
            {
                // Best effort cancel on COM operation.
            }

            var waited = await Task.WhenAny(opTask, Task.Delay(CancelGrace)).ConfigureAwait(false);
            if (waited != opTask)
            {
                endedInTime = false;
                var stillRunning = ObserveStillRunningAsync(opTask);
                return ComResultMap.Map(
                    ComInstallStatus.InstallError,
                    0,
                    0,
                    rebootRequired: false,
                    cancelled: true,
                    endedInTime: false,
                    stillRunning);
            }
        }

        try
        {
            var result = await opTask.ConfigureAwait(false);
            var status = MapStatus(result.Status);
            int ext = result.ExtendedErrorCode is null ? 0 : result.ExtendedErrorCode.HResult;
            return ComResultMap.Map(
                status,
                ext,
                result.InstallerErrorCode,
                result.RebootRequired,
                cancelled: cancelledByCallerOrTimeout,
                endedInTime: endedInTime,
                stillRunning: null);
        }
        catch (OperationCanceledException)
        {
            return ComResultMap.Map(
                ComInstallStatus.InstallError,
                0,
                0,
                rebootRequired: false,
                cancelled: true,
                endedInTime: true,
                stillRunning: null);
        }
        catch (Exception ex)
        {
            return ComResultMap.Map(
                ComInstallStatus.InternalError,
                ex.HResult,
                0,
                rebootRequired: false,
                cancelled: cancelledByCallerOrTimeout,
                endedInTime: true,
                stillRunning: null);
        }
    }

    private static async Task ObserveStillRunningAsync(Task opTask)
    {
        try
        {
            await opTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            CrashLog.Write("WingetInstaller: still-running COM operation ended with error", ex);
        }
    }

    private static ComState MapState(PackageInstallProgressState state) => state switch
    {
        PackageInstallProgressState.Queued => ComState.Queued,
        PackageInstallProgressState.Downloading => ComState.Downloading,
        PackageInstallProgressState.Installing => ComState.Installing,
        PackageInstallProgressState.PostInstall => ComState.PostInstall,
        PackageInstallProgressState.Finished => ComState.Finished,
        _ => ComState.Installing,
    };

    private static ComInstallStatus MapStatus(InstallResultStatus status) => status switch
    {
        InstallResultStatus.Ok => ComInstallStatus.Ok,
        InstallResultStatus.BlockedByPolicy => ComInstallStatus.BlockedByPolicy,
        InstallResultStatus.CatalogError => ComInstallStatus.CatalogError,
        InstallResultStatus.InternalError => ComInstallStatus.InternalError,
        InstallResultStatus.InvalidOptions => ComInstallStatus.InvalidOptions,
        InstallResultStatus.DownloadError => ComInstallStatus.DownloadError,
        InstallResultStatus.InstallError => ComInstallStatus.InstallError,
        InstallResultStatus.ManifestError => ComInstallStatus.ManifestError,
        InstallResultStatus.NoApplicableInstallers => ComInstallStatus.NoApplicableInstallers,
        InstallResultStatus.NoApplicableUpgrade => ComInstallStatus.NoApplicableUpgrade,
        InstallResultStatus.PackageAgreementsNotAccepted => ComInstallStatus.PackageAgreementsNotAccepted,
        _ => ComInstallStatus.InternalError,
    };
}
