using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace QuantumUpdater.Services;

public sealed record VolumeInfo(
    string Name,
    long Total,
    long Free,
    string Label = "",
    bool IsExternal = false,
    string Health = "Healthy");

public sealed record PhysicalDisk(string Name, string Media, string Health, long Size);

public sealed record LargeFile(string Path, long Size, DateTime? LastModified = null)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string FormattedSize => FormatBytes(Size);

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

/// <summary>
/// Disk &amp; Storage service (ADR 048, Phase 46.1).
/// Reads physical &amp; volume disk facts, scans large files, and executes safe Recycle Bin or permanent deletion.
/// </summary>
public sealed class DiskService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperation([In] ref SHFILEOPSTRUCT lpFileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    private static readonly string PowerShell = Path.Combine(
        Environment.SystemDirectory,
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    public IReadOnlyList<VolumeInfo> Volumes()
    {
        var list = new List<VolumeInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                // Cloud volumes and network shares are never shown (ADR 048)
                if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)
                {
                    continue;
                }

                if (!drive.IsReady || drive.TotalSize <= 0)
                {
                    continue;
                }

                bool isExternal = drive.DriveType == DriveType.Removable;
                string label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? (isExternal ? "External Storage" : "Local Disk")
                    : drive.VolumeLabel;

                list.Add(new VolumeInfo(
                    drive.Name,
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    label,
                    isExternal,
                    "Healthy"));
            }
            catch
            {
                // One volume can be locked. Keep the others.
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<PhysicalDisk>> PhysicalDisksAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(PowerShell))
        {
            return Array.Empty<PhysicalDisk>();
        }

        var psi = new ProcessStartInfo
        {
            FileName = PowerShell,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("Get-PhysicalDisk | Select-Object FriendlyName, MediaType, HealthStatus, Size | ConvertTo-Json -Compress");

        using var process = new Process { StartInfo = psi };
        await Task.Run(process.Start).ConfigureAwait(false);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // The list still works from the volumes.
            }

            return Array.Empty<PhysicalDisk>();
        }

        var output = await outputTask.ConfigureAwait(false);
        return ParseDisks(output);
    }

    public IReadOnlyList<LargeFile> LargestDownloads(int take = 15)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return ScanLargeFiles(root, 100L * 1024 * 1024, take);
    }

    public IReadOnlyList<LargeFile> ScanLargeFiles(string directoryPath, long minSizeBytes = 50L * 1024 * 1024, int take = 50)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Array.Empty<LargeFile>();
        }

        var found = new List<LargeFile>();
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        var seen = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", options))
            {
                seen++;
                if (seen > 6000)
                {
                    break;
                }

                try
                {
                    var fi = new FileInfo(file);
                    if (fi.Length >= minSizeBytes)
                    {
                        found.Add(new LargeFile(file, fi.Length, fi.LastWriteTimeUtc));
                    }
                }
                catch
                {
                    // Skip inaccessible files.
                }
            }
        }
        catch
        {
            return Array.Empty<LargeFile>();
        }

        return found.OrderByDescending(f => f.Size).Take(take).ToList();
    }

    /// <summary>
    /// Safely sends a file or directory to the Windows Recycle Bin (undoable).
    /// </summary>
    public bool SendToRecycleBin(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        try
        {
            var op = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + "\0\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT,
            };

            int result = SHFileOperation(ref op);
            return result == 0 && !op.fAnyOperationsAborted;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Permanently deletes a file or directory. Requires explicit user confirmation in UI (ADR 048).
    /// </summary>
    public bool DeletePermanently(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<PhysicalDisk> ParseDisks(string json)
    {
        var text = json.Trim();
        if (text.Length == 0)
        {
            return Array.Empty<PhysicalDisk>();
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var rows = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };
            var list = new List<PhysicalDisk>();
            foreach (var row in rows)
            {
                var name = Field(row, "FriendlyName");
                if (name.Length == 0)
                {
                    continue;
                }

                long size = 0;
                if (row.TryGetProperty("Size", out var sizeEl) && sizeEl.TryGetInt64(out var parsed))
                {
                    size = parsed;
                }

                list.Add(new PhysicalDisk(name, Field(row, "MediaType"), Field(row, "HealthStatus"), size));
            }

            return list;
        }
        catch
        {
            return Array.Empty<PhysicalDisk>();
        }
    }

    private static string Field(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? "").Trim()
            : "";
}
