using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public class WingetPathRulesTests : IDisposable
{
    private const string TrustedRoot = @"C:\Program Files\WindowsApps";

    private readonly string _tempRoot;

    public WingetPathRulesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "dv-winget-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch (IOException)
        {
            // Temp folder is cleaned later by Windows.
        }
        catch (UnauthorizedAccessException)
        {
            // Temp folder is cleaned later by Windows.
        }
    }

    [Fact]
    public void PathOutsideRoot_IsRejected()
    {
        Assert.False(WingetPathRules.IsUnderRoot(
            @"C:\Users\someone\AppData\Local\Microsoft\WindowsApps\winget.exe",
            TrustedRoot));
    }

    [Fact]
    public void RootPrefixTrick_IsRejected()
    {
        Assert.False(WingetPathRules.IsUnderRoot(
            @"C:\Program Files\WindowsAppsEvil\winget.exe",
            TrustedRoot));
    }

    [Fact]
    public void ParentTraversal_IsRejected()
    {
        Assert.False(WingetPathRules.IsUnderRoot(
            @"C:\Program Files\WindowsApps\..\Evil\winget.exe",
            TrustedRoot));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("winget.exe")]
    public void NullEmptyOrRelative_IsRejected(string? path)
    {
        Assert.False(WingetPathRules.IsUnderRoot(path, TrustedRoot));
    }

    [Fact]
    public void PathUnderRoot_IsAccepted()
    {
        var packageDir = Path.Combine(_tempRoot, "Microsoft.DesktopAppInstaller_1.0.0.0_x64__8wekyb3d8bbwe");
        Directory.CreateDirectory(packageDir);
        var exePath = Path.Combine(packageDir, "winget.exe");
        File.WriteAllText(exePath, string.Empty);

        Assert.True(WingetPathRules.IsUnderRoot(exePath, _tempRoot));
    }

    [Fact]
    public void PathUnderRoot_IgnoresCase()
    {
        var exePath = Path.Combine(_tempRoot, "Pkg", "winget.exe");
        Assert.True(WingetPathRules.IsUnderRoot(exePath.ToUpperInvariant(), _tempRoot.ToLowerInvariant()));
    }
}
