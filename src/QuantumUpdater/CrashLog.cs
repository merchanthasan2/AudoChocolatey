using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>Writes one entry to the given folder. Does nothing when the folder or the log is a junction or link. Never throws.</summary>
    internal static void Write(string folder, string where, Exception? ex)
    {
        try
        {
            // A junction or link could send the log to another folder. Do not write then.
            if (IsReparsePoint(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);

            string logPath = Path.Combine(folder, LogFileName);
            if (IsReparsePoint(logPath))
            {
                return;
            }

            RotateIfLarge(folder, logPath);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            File.AppendAllText(logPath, Format(where, ex, userProfile));
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

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

    // Moves the log aside when it is over 1 MB. The old copy is replaced.
    private static void RotateIfLarge(string folder, string logPath)
    {
        if (!File.Exists(logPath) || new FileInfo(logPath).Length <= MaxLogBytes)
        {
            return;
        }

        File.Move(logPath, Path.Combine(folder, OldLogFileName), overwrite: true);
    }

    private static bool IsReparsePoint(string path)
    {
        if (!Path.Exists(path))
        {
            return false;
        }

        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }
}
