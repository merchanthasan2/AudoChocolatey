using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public class ComResultMapTests
{
    private static ActionResult Map(
        ComInstallStatus status,
        int extendedErrorCode = 0,
        uint installerErrorCode = 0,
        bool rebootRequired = false,
        bool cancelled = false,
        bool endedInTime = true,
        Task? stillRunning = null)
    {
        return ComResultMap.Map(status, extendedErrorCode, installerErrorCode, rebootRequired, cancelled, endedInTime, stillRunning);
    }

    [Fact]
    public void Ok_is_success_without_restart()
    {
        ActionResult result = Map(ComInstallStatus.Ok);

        Assert.True(result.Success);
        Assert.False(result.RestartNeeded);
        Assert.False(result.NotStarted);
        Assert.Equal("Updated.", result.Message);
    }

    [Fact]
    public void Ok_with_reboot_required_asks_for_restart()
    {
        ActionResult result = Map(ComInstallStatus.Ok, rebootRequired: true);

        Assert.True(result.Success);
        Assert.True(result.RestartNeeded);
        Assert.Equal("Updated. Restart the PC to finish.", result.Message);
    }

    [Fact]
    public void NoApplicableUpgrade_is_success_already_up_to_date()
    {
        ActionResult result = Map(ComInstallStatus.NoApplicableUpgrade);

        Assert.True(result.Success);
        Assert.False(result.RestartNeeded);
        Assert.False(result.NotStarted);
        Assert.Equal("Already up to date", result.Message);
    }

    [Fact]
    public void InstallError_with_installer_code_1603_gives_table_text()
    {
        ActionResult result = Map(ComInstallStatus.InstallError, installerErrorCode: 1603);

        Assert.False(result.Success);
        Assert.False(result.NotStarted);
        Assert.Equal(
            "The installer stopped with an error. Try again. If it fails again, restart the PC and try again.",
            result.Message);
    }

    [Fact]
    public void InstallError_with_installer_code_3010_is_success_with_restart()
    {
        ActionResult result = Map(ComInstallStatus.InstallError, installerErrorCode: 3010);

        Assert.True(result.Success);
        Assert.True(result.RestartNeeded);
        Assert.Equal("Updated. Restart the PC to finish.", result.Message);
    }

    [Fact]
    public void InstallError_with_zero_installer_code_is_not_success()
    {
        ActionResult result = Map(ComInstallStatus.InstallError);

        Assert.False(result.Success);
        Assert.Equal("The installer stopped with an error. Try again.", result.Message);
    }

    [Theory]
    [InlineData(ComInstallStatus.BlockedByPolicy, "A Windows policy on this PC blocks this installation.", false)]
    [InlineData(ComInstallStatus.CatalogError, "The app list could not be reached. Try again later.", true)]
    [InlineData(ComInstallStatus.InternalError, "The update stopped with an internal error. Try again later.", false)]
    [InlineData(ComInstallStatus.InvalidOptions, "The update could not start. Try again later.", true)]
    [InlineData(ComInstallStatus.DownloadError, "The download failed. Check your internet connection, then try again.", false)]
    [InlineData(ComInstallStatus.ManifestError, "The app's install details could not be read. Try again later.", false)]
    [InlineData(ComInstallStatus.NoApplicableInstallers, "There is no installer for this version of Windows. Check the app maker's website.", false)]
    [InlineData(ComInstallStatus.PackageAgreementsNotAccepted, "The app's license terms were not accepted. Try again.", false)]
    public void Each_failure_status_gives_its_plain_text(ComInstallStatus status, string message, bool notStarted)
    {
        ActionResult result = Map(status);

        Assert.False(result.Success);
        Assert.False(result.RestartNeeded);
        Assert.Equal(notStarted, result.NotStarted);
        Assert.Equal(message, result.Message);
        Assert.Null(result.StillRunning);
    }

    [Fact]
    public void Extended_error_code_gives_table_text_before_the_status_text()
    {
        ActionResult result = Map(ComInstallStatus.BlockedByPolicy, extendedErrorCode: unchecked((int)0x8A15010F));

        Assert.False(result.Success);
        Assert.Equal("A Windows policy on this PC blocks this installation. Ask the person who manages this PC.", result.Message);
    }

    [Fact]
    public void Cancel_that_ended_gives_cancelled_text()
    {
        ActionResult result = Map(ComInstallStatus.InstallError, cancelled: true, endedInTime: true);

        Assert.False(result.Success);
        Assert.False(result.NotStarted);
        Assert.Equal("The update was cancelled.", result.Message);
        Assert.Null(result.StillRunning);
    }

    [Fact]
    public void Cancel_that_did_not_end_keeps_the_still_running_task()
    {
        Task running = new TaskCompletionSource().Task;

        ActionResult result = Map(ComInstallStatus.InstallError, cancelled: true, endedInTime: false, stillRunning: running);

        Assert.False(result.Success);
        Assert.Equal("DeskVerve could not stop the installer. The next action starts when it ends.", result.Message);
        Assert.Same(running, result.StillRunning);
    }

    [Fact]
    public void NoPackage_is_a_failure_after_the_start()
    {
        ActionResult result = ComResultMap.NoPackage();

        Assert.False(result.Success);
        Assert.False(result.RestartNeeded);
        Assert.False(result.NotStarted);
        Assert.Equal("This app is no longer in the Microsoft catalog.", result.Message);
        Assert.Null(result.StillRunning);
    }

    [Fact]
    public void Finished_update_wins_over_a_late_cancel()
    {
        ActionResult result = Map(ComInstallStatus.Ok, cancelled: true, endedInTime: true);

        Assert.True(result.Success);
        Assert.Equal("Updated.", result.Message);
    }
}
