namespace QuantumUpdater.Services.Winget;

/// <summary>
/// Mirror of the winget COM InstallResultStatus values (Microsoft.Management.Deployment).
/// The names and the order match the winmd of Microsoft.WindowsPackageManager.ComInterop 1.29.380.
/// </summary>
public enum ComInstallStatus
{
    Ok,
    BlockedByPolicy,
    CatalogError,
    InternalError,
    InvalidOptions,
    DownloadError,
    InstallError,
    ManifestError,
    NoApplicableInstallers,
    NoApplicableUpgrade,
    PackageAgreementsNotAccepted,
}

/// <summary>
/// Maps the result of one COM update action to an ActionResult. Pure: no COM calls here.
/// The COM code reads the values and passes them in, so tests run without winget.
/// </summary>
public static class ComResultMap
{
    private const string NoPackageMessage = "This app is no longer in the Microsoft catalog.";
    private const string AlreadyUpToDateMessage = "Already up to date";
    private const string UpdatedMessage = "Updated.";
    private const string UpdatedRestartMessage = "Updated. Restart the PC to finish.";
    private const string UnknownFailureMessage = "The update stopped. Try again.";

    /// <summary>
    /// Maps one COM result. cancelled = the person or the time limit asked to stop.
    /// endedInTime = the operation reached an end state (Completed, Canceled or Error) in time.
    /// stillRunning is used only when cancelled and not endedInTime.
    /// </summary>
    public static ActionResult Map(
        ComInstallStatus status,
        int extendedErrorCode,
        uint installerErrorCode,
        bool rebootRequired,
        bool cancelled,
        bool endedInTime,
        Task? stillRunning)
    {
        // A finished result wins over a late cancel: when the update completed before the cancel
        // reached winget, the app is updated and the person must see that.
        bool finished = endedInTime && status is ComInstallStatus.Ok or ComInstallStatus.NoApplicableUpgrade;
        if (cancelled && !finished)
        {
            return endedInTime
                ? new ActionResult(false, false, false, RunOutcome.CancelledMessage, null, null)
                : new ActionResult(false, false, false, RunOutcome.StopIncompleteMessage, null, stillRunning);
        }

        switch (status)
        {
            case ComInstallStatus.Ok:
                return rebootRequired
                    ? new ActionResult(true, true, false, UpdatedRestartMessage, null, null)
                    : new ActionResult(true, false, false, UpdatedMessage, null, null);

            case ComInstallStatus.NoApplicableUpgrade:
                return new ActionResult(true, false, false, AlreadyUpToDateMessage, null, null);

            case ComInstallStatus.InstallError when installerErrorCode != 0:
                return FromText(WingetErrorText.For(unchecked((int)installerErrorCode)), notStarted: false);

            default:
                return Failure(status, extendedErrorCode);
        }
    }

    /// <summary>The result when the app is not in the Microsoft catalog. This is a failure after the start.</summary>
    public static ActionResult NoPackage()
    {
        return new ActionResult(false, false, false, NoPackageMessage, null, null);
    }

    // A failure status. The extended error code gives the text when it is set. Else the status gives a plain text.
    private static ActionResult Failure(ComInstallStatus status, int extendedErrorCode)
    {
        bool notStarted = status is ComInstallStatus.InvalidOptions or ComInstallStatus.CatalogError;

        if (extendedErrorCode != 0)
        {
            return FromText(WingetErrorText.For(extendedErrorCode), notStarted);
        }

        return new ActionResult(false, false, notStarted, PlainText(status), null, null);
    }

    // Uses the table text. A success text (for example 3010) is a success with restart when needed.
    private static ActionResult FromText(ErrorText text, bool notStarted)
    {
        if (text.IsSuccess)
        {
            return new ActionResult(true, text.RestartNeeded, false, text.Reason, null, null);
        }

        return new ActionResult(false, false, notStarted, RunOutcome.Join(text.Reason, text.WhatToDo), null, null);
    }

    // The plain text for a failure status that has no winget error code.
    private static string PlainText(ComInstallStatus status)
    {
        return status switch
        {
            ComInstallStatus.BlockedByPolicy => "A Windows policy on this PC blocks this installation.",
            ComInstallStatus.CatalogError => "The app list could not be reached. Try again later.",
            ComInstallStatus.InternalError => "The update stopped with an internal error. Try again later.",
            ComInstallStatus.InvalidOptions => "The update could not start. Try again later.",
            ComInstallStatus.DownloadError => "The download failed. Check your internet connection, then try again.",
            ComInstallStatus.InstallError => "The installer stopped with an error. Try again.",
            ComInstallStatus.ManifestError => "The app's install details could not be read. Try again later.",
            ComInstallStatus.NoApplicableInstallers => "There is no installer for this version of Windows. Check the app maker's website.",
            ComInstallStatus.PackageAgreementsNotAccepted => "The app's license terms were not accepted. Try again.",
            _ => UnknownFailureMessage,
        };
    }
}
