using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace QuantumUpdater.Services;

/// <summary>Headline PC facts. No passwords, recovery keys, or file contents.</summary>
public sealed record MachineSummary(string Name, string Windows, string Cpu, string Memory, string Disk)
{
    public static MachineSummary Read()
    {
        return new MachineSummary(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            ReadCpu(),
            ReadMemory(),
            ReadDisk());
    }

    /// <summary>Fields the account service is allowed to store.</summary>
    public object Telemetry() => new
    {
        windows = Windows,
        cpu = Cpu,
        memory = Memory,
        disk = Disk,
    };

    private static string ReadCpu()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var name = key?.GetValue("ProcessorNameString") as string;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }
        catch
        {
            // Registry can be unreadable. Fall through to a count.
        }

        return $"{Environment.ProcessorCount} processors";
    }

    private static string ReadMemory()
    {
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
        {
            return "Unavailable";
        }

        var totalGb = status.ullTotalPhys / 1024d / 1024d / 1024d;
        return $"{totalGb:0.#} GB";
    }

    private static string ReadDisk()
    {
        long total = 0;
        long free = 0;
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                {
                    continue;
                }

                total += drive.TotalSize;
                free += drive.AvailableFreeSpace;
            }
            catch
            {
                // One volume can fail. Keep the others.
            }
        }

        if (total == 0)
        {
            return "Unavailable";
        }

        return $"{free / 1024d / 1024d / 1024d:0.#} GB free of {total / 1024d / 1024d / 1024d:0.#} GB";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
}
