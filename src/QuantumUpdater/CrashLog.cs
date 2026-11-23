using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using QuantumUpdater.Services;

namespace QuantumUpdater;

/// <summary>
/// Writes crash details to a local log file. Writes the time, the place, the error type,
/// the HResult and the stack trace only. Never writes exception messages, because they can hold user data.
/// </summary>
internal static class CrashLog
{
    private const string FolderName = "QuantumUpdater";
    private const string LogFileName = "winui-crash.log";
    private const string OldLogFileName = "winui-crash.old.log";
    private const long MaxLogBytes = 1024 * 1024;

    /// <summary>Writes one entry to the app folder under %LOCALAPPDATA%. Never throws.</summary>
    public static void Write(string where, Exception? ex)
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Write(Path.Combine(localAppData, FolderName), where, ex);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    /// <summary>
    /// Writes one entry to the given folder. Does nothing when the folder or the log is a junction or link. Never throws.
    /// The entry is added to the old text and the whole log is written again through SafeLocalFile
    /// (temp file plus replace), so an admin write never follows a link that appears after the check.
    /// </summary>
    internal static void Write(string folder, string where, Exception? ex)
    {
        lock (Gate)
        {
            try
            {
                // A junction or link could send the log to another folder. Do not write then.
                string logPath = Path.Combine(folder, LogFileName);
                if (SafeLocalFile.IsReparsePoint(folder) || SafeLocalFile.IsReparsePoint(logPath))
                {
                    return;
                }

                RotateIfLarge(folder, logPath);
                string existing = string.Empty;
                if (File.Exists(logPath))
                {
                    // A log that cannot be read is kept as it is, and this entry is dropped.
                    string? read = SafeLocalFile.TryReadText(folder, LogFileName, MaxLogBytes * 2);
                    if (read is null)
                    {
                        return;
                    }

                    existing = read;
                }

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                SafeLocalFile.TryWriteText(folder, LogFileName, existing + Format(where, ex, userProfile));
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }

    private static readonly object Gate = new();

    /// <summary>Builds the text of one entry. The user profile path is replaced with %USERPROFILE%.</summary>
    internal static string Format(string where, Exception? ex, string userProfile)
    {
        string now = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
        var text = new StringBuilder();
        text.AppendLine($"{now} [{SafeLabel(where)}]");
        AppendDetails(text, string.Empty, ex, userProfile);
        if (ex?.InnerException is not null)
        {
            AppendDetails(text, "Inner ", ex.InnerException, userProfile);
        }

        text.AppendLine();
        return text.ToString();
    }

    /// <summary>
    /// Replaces the user profile path, in any letter case, with %USERPROFILE%.
    /// F22: any other X:\Users\name folder (another account, a moved profile, a Debug build path) loses its name too.
    /// </summary>
    internal static string Redact(string text, string userProfile)
    {
        if (!string.IsNullOrEmpty(userProfile))
        {
            text = text.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        return UsersFolder.Replace(text, "${root}%USERNAME%");
    }

    /// <summary>F22: labels are fixed text from the code, but a diagnostic label can carry an environment value. Keep it short and plain.</summary>
    internal static string SafeLabel(string where)
    {
        var label = new StringBuilder(Math.Min(where.Length, MaxLabelLength));
        foreach (char c in where)
        {
            if (label.Length == MaxLabelLength)
            {
                break;
            }

            label.Append(char.IsAsciiLetterOrDigit(c) || c is ' ' or ':' or '_' or '.' or ',' or '-' ? c : '?');
        }

        return label.ToString();
    }

    private const int MaxLabelLength = 80;

    // A drive, then Users, then one folder name. %USERNAME% is skipped so a second pass changes nothing.
    private static readonly Regex UsersFolder = new(
        @"(?<root>\b[A-Za-z]:[\\/]Users[\\/])(?!%USERNAME%)[^\\/\r\n:*?""<>|]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void AppendDetails(StringBuilder text, string prefix, Exception? ex, string userProfile)
    {
        text.AppendLine($"{prefix}Type: {ex?.GetType().FullName ?? "(none)"}");
        text.AppendLine($"{prefix}HResult: 0x{(ex?.HResult ?? 0):X8}");
        text.AppendLine($"{prefix}Stack:");
        text.AppendLine(Redact(ex?.StackTrace ?? "(none)", userProfile));
    }

    // Moves the log aside when it is over 1 MB. The old copy is replaced (the move replaces a link, never follows it).
    private static void RotateIfLarge(string folder, string logPath)
    {
        if (!File.Exists(logPath) || new FileInfo(logPath).Length <= MaxLogBytes)
        {
            return;
        }

        if (SafeLocalFile.IsReparsePoint(Path.Combine(folder, OldLogFileName)))
        {
            File.Delete(Path.Combine(folder, OldLogFileName));
        }

        File.Move(logPath, Path.Combine(folder, OldLogFileName), overwrite: true);
    }
}
