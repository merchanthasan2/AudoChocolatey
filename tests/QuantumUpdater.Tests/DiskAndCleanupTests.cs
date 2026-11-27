using QuantumUpdater.Services;
using QuantumUpdater.Services.Cleanup;
using Xunit;

namespace QuantumUpdater.Tests;

public sealed class DiskAndCleanupTests
{
    [Fact]
    public void LargeFile_FormatBytes_ReturnsExpectedUnits()
    {
        Assert.Equal("500 B", LargeFile.FormatBytes(500));
        Assert.Equal("1.5 KB", LargeFile.FormatBytes(1536));
        Assert.Equal("10.0 MB", LargeFile.FormatBytes(10 * 1024 * 1024));
        Assert.Equal("4.50 GB", LargeFile.FormatBytes((long)(4.5 * 1024 * 1024 * 1024)));
    }

    [Fact]
    public void DiskService_Volumes_ReturnsAtLeastOneSystemDrive()
    {
        var svc = new DiskService();
        var vols = svc.Volumes();

        Assert.NotEmpty(vols);
        var sysDrive = vols.FirstOrDefault(v => v.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(sysDrive);
        Assert.True(sysDrive.Total > 0);
        Assert.True(sysDrive.Free > 0);
        Assert.False(sysDrive.IsExternal);
        Assert.Equal("Healthy", sysDrive.Health);
    }

    [Fact]
    public void DiskService_ScanLargeFiles_FindsCreatedTestFiles()
    {
        var svc = new DiskService();
        var testDir = Path.Combine(Path.GetTempPath(), "DiskScanTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        try
        {
            var largePath = Path.Combine(testDir, "big_dummy.bin");
            var smallPath = Path.Combine(testDir, "small.txt");

            // Write 5 MB dummy file
            using (var fs = new FileStream(largePath, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(5 * 1024 * 1024);
            }
            File.WriteAllText(smallPath, "tiny");

            // Scan with minSize 1MB
            var results = svc.ScanLargeFiles(testDir, minSizeBytes: 1024 * 1024);

            Assert.Single(results);
            Assert.Equal("big_dummy.bin", results[0].FileName);
            Assert.Equal(5 * 1024 * 1024, results[0].Size);
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [Fact]
    public void CleanupService_ActivityHistory_RecordsAndRetrievesItems()
    {
        var svc = new CleanupService();
        string testTarget = "TestApp_" + Guid.NewGuid().ToString("N");

        svc.RecordActivity("TestRun", testTarget, true, "Testing activity log recording");

        var history = svc.GetHistory();
        Assert.NotEmpty(history);

        var found = history.FirstOrDefault(h => h.Target == testTarget);
        Assert.NotNull(found);
        Assert.Equal("TestRun", found.ActionType);
        Assert.True(found.Success);
        Assert.Equal("Testing activity log recording", found.Details);
    }

    [Fact]
    public void CleanupService_ScanTempFiles_DoesNotThrowAndReturnsCount()
    {
        var svc = new CleanupService();
        var summary = svc.ScanTempFiles();

        Assert.NotNull(summary);
        Assert.True(summary.FileCount >= 0);
        Assert.True(summary.TotalBytes >= 0);
    }

    [Fact]
    public void CleanupService_ListStartupApps_ReturnsWithoutThrowing()
    {
        var svc = new CleanupService();
        var apps = svc.ListStartupApps();

        Assert.NotNull(apps);
        foreach (var app in apps)
        {
            Assert.False(string.IsNullOrWhiteSpace(app.Name));
            Assert.False(string.IsNullOrWhiteSpace(app.Source));
        }
    }

    [Fact]
    public void CleanupService_ScanLeftoverFolders_FiltersKnownApps()
    {
        var svc = new CleanupService();
        var known = new[] { "Windows", "Temp", "Common" };
        var leftovers = svc.ScanLeftoverFolders(known);

        Assert.NotNull(leftovers);
        foreach (var item in leftovers)
        {
            Assert.DoesNotContain(item.Name, known, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CleanupService_ToggleStartupApp_UndoSafelyHandlesMissingKeys()
    {
        var svc = new CleanupService();
        var dummyItem = new StartupAppItem(
            Id: "HKCU_NonExistentApp99",
            Name: "NonExistentApp99",
            Command: "C:\\Fake\\app.exe",
            Source: "User Account",
            IsEnabled: true,
            RegistryHive: "HKCU",
            RegistryPath: @"Software\Microsoft\Windows\CurrentVersion\Run");

        // Should disable and backup safely without throwing
        bool disabled = svc.ToggleStartupApp(dummyItem, enable: false);
        Assert.True(disabled);

        // Should undo/restore safely
        bool restored = svc.ToggleStartupApp(dummyItem, enable: true);
        Assert.True(restored);

        // Clean up from Run key
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            key?.DeleteValue("NonExistentApp99", throwOnMissingValue: false);
        }
        catch { }
    }
}
