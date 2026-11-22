using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public class CliPhaseTests
{
    [Theory]
    [InlineData("winget-download-progress.txt", "Preparing,Downloading,Checking")]
    [InlineData("winget-download-hash-mismatch.txt", "Preparing,Downloading")]
    public void SavedOutputGivesOnlyObservedPhases(string file, string expected)
    {
        var phases = new List<string>();
        foreach (string line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Data", file)))
            if (CliPhase.TryParse(line, out string phase)) phases.Add(phase);

        Assert.Equal(expected, string.Join(",", phases));
    }

    [Theory]
    [InlineData("\r\n Found Example [Example.App] Version 1.0 \r\n", "Preparing")]
    [InlineData("Downloading https://example.org/setup.exe\r", "Downloading")]
    [InlineData("Successfully verified installer hash\r\n", "Checking")]
    [InlineData("Starting package install...\r\n", "Installing")]
    [InlineData("Starting package install", "Installing")]
    public void RecognizesPhaseLines(string line, string expected)
    {
        Assert.True(CliPhase.TryParse(line, out string phase));
        Assert.Equal(expected, phase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\r\n")]
    [InlineData("45%")]
    [InlineData("10 MB / 20 MB")]
    [InlineData("Installer hash does not match.")]
    [InlineData("Installer downloaded: C:\\Downloads\\setup.exe")]
    [InlineData("Error: Downloading failed")]
    [InlineData("Starting package installation failed")]
    [InlineData("Successfully verified installer hash failed")]
    public void IgnoresUnrecognizedOutputAndClearsPhase(string? line)
    {
        Assert.False(CliPhase.TryParse(line, out string phase));
        Assert.Equal(string.Empty, phase);
    }
}
