using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace QuantumUpdater.Services;

/// <summary>
/// Abstraction over Task Scheduler operations so tests do not create real scheduled tasks.
/// </summary>
public interface ITaskSchedulerClient
{
    bool IsTaskEnabled(string taskName);
    void RegisterTask(string taskName, string xmlDefinition);
    void DeleteTask(string taskName);
}

/// <summary>
/// Abstraction over file system and path security operations so tests can verify
/// security rules without needing administrative access to Program Files.
/// </summary>
public interface IPathSecurityChecker
{
    bool FileExists(string path);
    bool HasReparsePoint(string path);
    string GetFinalPath(string path);
    bool IsUnderProgramFiles(string path);
}

/// <summary>
/// Default production implementation using Win32 file APIs and Program Files inspection.
/// </summary>
public class Win32PathSecurityChecker : IPathSecurityChecker
{
    public virtual bool FileExists(string path) => File.Exists(path);

    public virtual bool HasReparsePoint(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }
            }

            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(dir))
                {
                    var di = new DirectoryInfo(dir);
                    if (di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        return true;
                    }
                }
                dir = Path.GetDirectoryName(dir);
            }

            return false;
        }
        catch
        {
            return true; // Fail closed if attributes cannot be determined
        }
    }

    public virtual string GetFinalPath(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return Win32GetFinalPath(path);
        }

        return Path.GetFullPath(path);
    }

    public virtual bool IsUnderProgramFiles(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(pf) && IsUnderRoot(path, pf))
        {
            return true;
        }

        var pfX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(pfX86) && IsUnderRoot(path, pfX86))
        {
            return true;
        }

        var pf64 = Environment.GetEnvironmentVariable("ProgramW6432");
        if (!string.IsNullOrEmpty(pf64) && IsUnderRoot(path, pf64))
        {
            return true;
        }

        return false;
    }

    public static bool IsUnderRoot(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        try
        {
            if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(root))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            var rootPrefix = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string Win32GetFinalPath(string path)
    {
        const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        using var handle = CreateFile(
            path,
            0,
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"Failed to open '{path}' for canonical path resolution (Win32 error {error}).");
        }

        var sb = new StringBuilder(1024);
        uint length = GetFinalPathNameByHandle(handle, sb, (uint)sb.Capacity, 0);
        if (length == 0)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"Failed to resolve canonical path for '{path}' (Win32 error {error}).");
        }

        string result = sb.ToString();
        if (result.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            result = @"\\" + result.Substring(8);
        }
        else if (result.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Substring(4);
        }

        return result;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);
}

/// <summary>
/// Default production implementation using Windows schtasks.exe with XML definitions.
/// </summary>
public class SchtasksSchedulerClient : ITaskSchedulerClient
{
    private static readonly string SchtasksExe = Path.Combine(Environment.SystemDirectory, "schtasks.exe");

    public bool IsTaskEnabled(string taskName)
    {
        var result = RunSchtasks($"/Query /TN \"{taskName}\" /XML");
        if (result.ExitCode != 0)
        {
            return false;
        }

        var output = result.StandardOutput;
        if (output.Contains("<Enabled>false</Enabled>", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public void RegisterTask(string taskName, string xmlDefinition)
    {
        var tempXmlPath = Path.Combine(Path.GetTempPath(), $"DeskVerveTask_{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(tempXmlPath, xmlDefinition, Encoding.Unicode);
            var result = RunSchtasks($"/Create /TN \"{taskName}\" /XML \"{tempXmlPath}\" /F");
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to register scheduled task '{taskName}'. Exit code: {result.ExitCode}. Output: {result.StandardError.Trim()} {result.StandardOutput.Trim()}");
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempXmlPath))
                {
                    File.Delete(tempXmlPath);
                }
            }
            catch
            {
                // Best effort cleanup of temp file
            }
        }
    }

    public void DeleteTask(string taskName)
    {
        var result = RunSchtasks($"/Delete /TN \"{taskName}\" /F");
        if (result.ExitCode != 0)
        {
            var combined = (result.StandardError + " " + result.StandardOutput).ToLowerInvariant();
            if (combined.Contains("cannot find") || combined.Contains("does not exist"))
            {
                // Already absent, ignore "not found"
                return;
            }

            throw new InvalidOperationException(
                $"Failed to delete scheduled task '{taskName}'. Exit code: {result.ExitCode}. Output: {result.StandardError.Trim()}");
        }
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunSchtasks(string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = SchtasksExe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(10_000);

        return (process.ExitCode, stdout, stderr);
    }
}

/// <summary>
/// Manages the "Start with Windows" scheduled task for DeskVerve (Tracker 49.1).
/// Resolves and enforces that the target executable is strictly a DeskVerve.exe installed under %ProgramFiles%.
/// Refuses reparse points, relative paths, user-writable locations and portable zip runs to prevent privilege escalation.
/// </summary>
public sealed class StartWithWindowsService
{
    public const string TaskName = @"\Quantum Step\DeskVerve Start";
    public const string BackgroundArgument = "--background";
    public const string RequiredExecutableName = "DeskVerve.exe";

    private readonly ITaskSchedulerClient _scheduler;
    private readonly IPathSecurityChecker _pathChecker;

    public StartWithWindowsService(
        ITaskSchedulerClient? scheduler = null,
        IPathSecurityChecker? pathChecker = null)
    {
        _scheduler = scheduler ?? new SchtasksSchedulerClient();
        _pathChecker = pathChecker ?? new Win32PathSecurityChecker();
    }

    public bool IsEnabled()
    {
        return _scheduler.IsTaskEnabled(TaskName);
    }

    public void Disable()
    {
        _scheduler.DeleteTask(TaskName);
    }

    public void Enable(string? executablePath = null)
    {
        var targetPath = executablePath ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException("Cannot determine executable path for DeskVerve.");
        }

        var validatedPath = ValidateExecutablePath(targetPath);
        var xml = BuildTaskXml(validatedPath);
        _scheduler.RegisterTask(TaskName, xml);
    }

    public string ValidateExecutablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Executable path must not be empty.");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: '{path}' is a relative path. An absolute path under Program Files is required.");
        }

        if (!_pathChecker.FileExists(path))
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: executable '{path}' was not found.");
        }

        var fileName = Path.GetFileName(path);
        if (!string.Equals(fileName, RequiredExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: target executable must be named '{RequiredExecutableName}', but was '{fileName}'.");
        }

        if (_pathChecker.HasReparsePoint(path))
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: '{path}' or one of its parent directories contains a reparse point (symlink or junction), which is refused for security.");
        }

        string finalPath;
        try
        {
            finalPath = _pathChecker.GetFinalPath(path);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: failed to resolve canonical path for '{path}': {ex.Message}", ex);
        }

        var finalFileName = Path.GetFileName(finalPath);
        if (!string.Equals(finalFileName, RequiredExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: resolved executable must be named '{RequiredExecutableName}', but was '{finalFileName}'.");
        }

        var normalizedInput = Path.GetFullPath(path).TrimEnd('\\');
        var normalizedFinal = Path.GetFullPath(finalPath).TrimEnd('\\');
        if (!string.Equals(normalizedInput, normalizedFinal, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: the path '{path}' resolves to a different canonical path '{finalPath}' (potential symlink or redirection), which is refused for security.");
        }

        if (_pathChecker.HasReparsePoint(finalPath))
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: resolved path '{finalPath}' contains a reparse point, which is refused for security.");
        }

        if (!_pathChecker.IsUnderProgramFiles(finalPath))
        {
            throw new InvalidOperationException(
                $"Cannot enable start with Windows: DeskVerve must be installed under Program Files. " +
                $"The path '{finalPath}' is in a user-writable or portable folder. " +
                "Starting an elevated scheduled task for a user-writable executable is refused to prevent privilege escalation.");
        }

        return finalPath;
    }

    public static string BuildTaskXml(string executablePath, string? userName = null)
    {
        var user = userName ?? GetCurrentUserName();
        var escapedPath = EscapeXml(executablePath);
        var escapedUser = EscapeXml(user);

        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>Quantum Step</Author>
    <Description>Starts DeskVerve in the background at user logon with elevated privileges.</Description>
    <URI>\Quantum Step\DeskVerve Start</URI>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{escapedUser}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{escapedUser}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{escapedPath}</Command>
      <Arguments>{BackgroundArgument}</Arguments>
    </Exec>
  </Actions>
</Task>";
    }

    public static bool HasBackgroundSwitch(string[]? args = null)
    {
        if (args != null && args.Any(a => string.Equals(a, BackgroundArgument, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        try
        {
            var cmdArgs = Environment.GetCommandLineArgs();
            return cmdArgs.Any(a => string.Equals(a, BackgroundArgument, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string GetCurrentUserName()
    {
        try
        {
            return WindowsIdentity.GetCurrent().Name;
        }
        catch
        {
            var domain = Environment.UserDomainName;
            var user = Environment.UserName;
            return string.IsNullOrEmpty(domain) ? user : $"{domain}\\{user}";
        }
    }
}
