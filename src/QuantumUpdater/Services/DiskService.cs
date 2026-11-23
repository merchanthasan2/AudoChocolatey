using System.Diagnostics;
using System.Text.Json;

namespace QuantumUpdater.Services;

public sealed record VolumeInfo(string Name, long Total, long Free);

public sealed record PhysicalDisk(string Name, string Media, string Health, long Size);

public sealed record LargeFile(string Path, long Size);

/// <summary>Read-only disk facts. This service does not delete or repair anything.</summary>
public sealed class DiskService
{
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
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady || drive.TotalSize <= 0)
                {
                    continue;
                }

                list.Add(new VolumeInfo(drive.Name, drive.TotalSize, drive.AvailableFreeSpace));
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
        // Off the UI thread, see WingetRunner (start crash 0xC000027B).
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
        if (!Directory.Exists(root))
        {
            return Array.Empty<LargeFile>();
        }

        var found = new List<LargeFile>();
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        var seen = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", options))
            {
                seen++;
                if (seen > 4000)
                {
                    break;
                }

                try
                {
                    var length = new FileInfo(file).Length;
                    if (length >= 100L * 1024 * 1024)
                    {
                        found.Add(new LargeFile(file, length));
                    }
                }
                catch
                {
                    // Skip a file we cannot size.
                }
            }
        }
        catch
        {
            return Array.Empty<LargeFile>();
        }

        return found.OrderByDescending(file => file.Size).Take(take).ToList();
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
