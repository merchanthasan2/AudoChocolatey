namespace QuantumUpdater.Services.Winget;

/// <summary>
/// Plain text for an install result. Reason says what happened.
/// WhatToDo says what the owner can do next.
/// IsSuccess = the update finished. RestartNeeded = a PC restart is needed to finish.
/// </summary>
public sealed record ErrorText(string Reason, string WhatToDo, bool IsSuccess = false, bool RestartNeeded = false);

/// <summary>
/// Maps an install exit code to plain text.
/// Codes are compared as unsigned 32-bit numbers.
/// Numbers come only from docs/reference/winget-error-codes.md.
/// </summary>
public static class WingetErrorText
{
    private static readonly Dictionary<uint, ErrorText> Table = new()
    {
        // APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE
        [0x8A15002B] = new ErrorText("There is no newer version for this PC.", "No action is needed."),

        // APPINSTALLER_CLI_ERROR_INSTALLER_HASH_MISMATCH
        [0x8A150011] = new ErrorText("The download did not match the file the app maker published.", "Try again later. The app maker may be updating the file."),

        // APPINSTALLER_CLI_ERROR_NO_APPLICABLE_INSTALLER
        [0x8A150010] = new ErrorText("There is no installer for this version of Windows.", "Check the app maker's website."),

        // APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE
        [0x8A150101] = new ErrorText("The app is open.", "Close the app, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE_BY_APPLICATION
        [0x8A150111] = new ErrorText("The app is open.", "Close the app, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_FILE_IN_USE
        [0x8A150103] = new ErrorText("The app is open.", "Close the app, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_INSTALL_IN_PROGRESS
        [0x8A150102] = new ErrorText("Another installation is running.", "Wait for it to finish, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_DISK_FULL
        [0x8A150105] = new ErrorText("The disk is full.", "Free some space, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_INSUFFICIENT_MEMORY
        [0x8A150106] = new ErrorText("There is not enough memory.", "Close some apps, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_NO_NETWORK
        [0x8A150107] = new ErrorText("The download failed.", "Check your internet connection, then try again."),

        // APPINSTALLER_CLI_ERROR_DOWNLOAD_FAILED
        [0x8A150008] = new ErrorText("The download failed.", "Check your internet connection, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_REQUIRED_TO_FINISH
        [0x8A150109] = new ErrorText("Updated. Restart the PC to finish.", "Restart when it suits you.", IsSuccess: true, RestartNeeded: true),

        // APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_REQUIRED_FOR_INSTALL
        [0x8A15010A] = new ErrorText("Windows must restart before this update can install.", "Restart the PC, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_INITIATED
        [0x8A15010B] = new ErrorText("The installer is restarting the PC.", "Save your work.", IsSuccess: true, RestartNeeded: true),

        // APPINSTALLER_CLI_ERROR_INSTALL_CANCELLED_BY_USER
        [0x8A15010C] = new ErrorText("The update was cancelled.", "Try again when you are ready."),

        // APPINSTALLER_CLI_ERROR_INSTALL_ALREADY_INSTALLED
        [0x8A15010D] = new ErrorText("This version is already installed.", "No action is needed.", IsSuccess: true),

        // APPINSTALLER_CLI_ERROR_INSTALL_DOWNGRADE
        [0x8A15010E] = new ErrorText("A newer version is already installed.", "No action is needed."),

        // APPINSTALLER_CLI_ERROR_INSTALL_BLOCKED_BY_POLICY
        [0x8A15010F] = new ErrorText("A Windows policy on this PC blocks this installation.", "Ask the person who manages this PC."),

        // APPINSTALLER_CLI_ERROR_INSTALL_DEPENDENCIES
        [0x8A150110] = new ErrorText("A part this app needs is missing.", "Try again. If it fails again, install the app from its website."),

        // APPINSTALLER_CLI_ERROR_INSTALL_MISSING_DEPENDENCY
        [0x8A150104] = new ErrorText("A part this app needs is missing.", "Try again. If it fails again, install the app from its website."),

        // APPINSTALLER_CLI_ERROR_INSTALL_CONTACT_SUPPORT
        [0x8A150108] = new ErrorText("The installer stopped with an error.", "Contact the app maker's support."),

        // APPINSTALLER_CLI_ERROR_PACKAGE_IS_PINNED
        [0x8A150068] = new ErrorText("This app is set to stay on its current version.", "Remove the hold, then try again."),

        // APPINSTALLER_CLI_ERROR_INSTALL_INVALID_PARAMETER
        [0x8A150112] = new ErrorText("The installer stopped with an error.", "Try again. If it fails again, update the app from its website."),

        // APPINSTALLER_CLI_ERROR_INSTALL_SYSTEM_NOT_SUPPORTED
        [0x8A150113] = new ErrorText("This update cannot run on this PC.", "Check the app maker's website."),

        // APPINSTALLER_CLI_ERROR_INSTALL_UPGRADE_NOT_SUPPORTED
        [0x8A150114] = new ErrorText("This app cannot update itself this way.", "Update the app from its website."),

        // APPINSTALLER_CLI_ERROR_INSTALL_CUSTOM_ERROR
        [0x8A150115] = new ErrorText("The installer stopped with an error.", "Contact the app maker's support."),

        // Windows Installer code 1602 (decimal): the user cancelled
        [1602] = new ErrorText("The update was cancelled.", "Try again when you are ready."),

        // Windows Installer code 1603 (decimal): fatal error
        [1603] = new ErrorText("The installer stopped with an error.", "Try again. If it fails again, restart the PC and try again."),

        // Windows Installer code 1618 (decimal): another installation running (same as APPINSTALLER_CLI_ERROR_INSTALL_INSTALL_IN_PROGRESS)
        [1618] = new ErrorText("Another installation is running.", "Wait for it to finish, then try again."),

        // Windows Installer code 1641 (decimal): installer started a restart, success
        [1641] = new ErrorText("The installer is restarting the PC.", "Save your work.", IsSuccess: true, RestartNeeded: true),

        // Windows Installer code 3010 (decimal): restart required, success
        [3010] = new ErrorText("Updated. Restart the PC to finish.", "Restart when it suits you.", IsSuccess: true, RestartNeeded: true),
    };

    /// <summary>Returns the plain text for an install exit code.</summary>
    public static ErrorText For(int exitCode)
    {
        uint code = unchecked((uint)exitCode);

        // Exit code 0 means success. It is not in the reference table.
        if (code == 0)
        {
            return new ErrorText("Done.", "", IsSuccess: true);
        }

        if (Table.TryGetValue(code, out ErrorText? known))
        {
            return known;
        }

        string shown = code >= 0x80000000u
            ? $"0x{code:X8}"
            : code.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new ErrorText(
            $"The update stopped (code {shown}).",
            "Try again. If it fails again, update the app from its website.");
    }
}
