using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuantumUpdater.Services.Elevation;

/// <summary>
/// Starts a user-owned uninstaller from an elevated DeskVerve process with the
/// non-elevated desktop shell token (GetShellWindow -> OpenProcessToken -> DuplicateTokenEx -> CreateProcessWithTokenW).
/// Prevents HKCU privilege escalation (section 5.7, 46.5.28).
/// </summary>
public static class NormalUserLauncher
{
    private const uint TokenAssignPrimary = 0x0001;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenQuery = 0x0008;
    private const uint MaximumAllowed = 0x02000000;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint CreateNoWindow = 0x08000000;
    private const uint CreateUnicodeEnvironment = 0x00000400;

    public sealed record LaunchResult(bool Started, int? ProcessId, string? Error);

    /// <summary>Starts an executable with explicit arguments as the signed-in non-elevated desktop user.</summary>
    public static LaunchResult Launch(string fileName, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
        {
            return new LaunchResult(false, null, "Uninstaller file was not found.");
        }

        IntPtr shellWnd = GetShellWindow();
        if (shellWnd == IntPtr.Zero)
        {
            return new LaunchResult(false, null, "Desktop shell is not running.");
        }

        _ = GetWindowThreadProcessId(shellWnd, out uint shellPid);
        if (shellPid == 0)
        {
            return new LaunchResult(false, null, "Could not locate desktop shell process.");
        }

        IntPtr shellProcess = IntPtr.Zero;
        IntPtr shellToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        try
        {
            shellProcess = OpenProcess(0x0400 /* PROCESS_QUERY_INFORMATION */, false, shellPid);
            if (shellProcess == IntPtr.Zero)
            {
                return new LaunchResult(false, null, "Could not open desktop shell process.");
            }

            if (!OpenProcessToken(shellProcess, TokenDuplicate | TokenQuery | TokenAssignPrimary, out shellToken))
            {
                return new LaunchResult(false, null, "Could not read desktop shell token.");
            }

            if (!DuplicateTokenEx(
                    shellToken,
                    MaximumAllowed,
                    IntPtr.Zero,
                    SecurityImpersonation,
                    TokenPrimary,
                    out primaryToken))
            {
                return new LaunchResult(false, null, "Could not duplicate user token.");
            }

            var cmdLine = BuildCommandLine(fileName, arguments);
            var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
            var workingDir = Path.GetDirectoryName(fileName);

            if (!CreateProcessWithTokenW(
                    primaryToken,
                    0,
                    fileName,
                    cmdLine,
                    CreateNoWindow | CreateUnicodeEnvironment,
                    IntPtr.Zero,
                    string.IsNullOrEmpty(workingDir) ? null : workingDir,
                    ref si,
                    out var pi))
            {
                int err = Marshal.GetLastWin32Error();
                return new LaunchResult(false, null, new Win32Exception(err).Message);
            }

            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return new LaunchResult(true, (int)pi.dwProcessId, null);
        }
        finally
        {
            if (primaryToken != IntPtr.Zero) CloseHandle(primaryToken);
            if (shellToken != IntPtr.Zero) CloseHandle(shellToken);
            if (shellProcess != IntPtr.Zero) CloseHandle(shellProcess);
        }
    }

    internal static string BuildCommandLine(string fileName, IReadOnlyList<string> arguments)
    {
        var parts = new List<string>(arguments.Count + 1) { QuoteArg(fileName) };
        foreach (var arg in arguments)
        {
            parts.Add(QuoteArg(arg));
        }
        return string.Join(" ", parts);
    }

    private static string QuoteArg(string arg)
    {
        if (arg.Length > 0 && !arg.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            return arg;
        }

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        int ImpersonationLevel,
        int TokenType,
        out IntPtr phNewToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessWithTokenW(
        IntPtr hToken,
        uint dwLogonFlags,
        string? lpApplicationName,
        string? lpCommandLine,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }
}
