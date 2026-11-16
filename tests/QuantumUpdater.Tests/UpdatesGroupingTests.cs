using System;
using System.Collections.Generic;
using System.Linq;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Updates;
using Xunit;

namespace QuantumUpdater.Tests;

public class UpdatesGroupingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Now;

    private static UpdateCandidate Candidate(
        string id,
        string name = "App",
        string? installed = "1.0",
        string? available = "2.0",
        string? source = "Microsoft catalog")
    {
        return new UpdateCandidate(id, name, "Publisher", installed, available, source);
    }

    private static UpdatedRecord Updated(string id, string version, DateTimeOffset at, string name = "App")
    {
        return new UpdatedRecord(id, name, "Publisher", version, at);
    }

    private static IReadOnlyList<UpdateCandidate> Candidates(params UpdateCandidate[] items) => items;

    private static IReadOnlyList<UpdatedRecord> Records(params UpdatedRecord[] items) => items;

    [Theory]
    [InlineData("bad id")]
    [InlineData("-leading.dash")]
    [InlineData("Ends.With...")]
    [InlineData("")]
    public void BadCatalogId_IsSkipped(string id)
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(Candidate(id), Candidate("Git.Git", name: "Git")),
            Records(),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal("Git.Git", row.CatalogId);
    }

    [Fact]
    public void DuplicateId_IsKeptOnce_AndTheRowWithAVersionWins()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(
                Candidate("Git.Git", installed: null, available: "2.0"),
                Candidate("git.git", installed: "1.5", available: "2.0")),
            Records(),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal(UpgradeGroup.Ready, row.Group);
        Assert.Equal("1.5", row.InstalledVersion);
    }

    [Fact]
    public void DuplicateId_WithVersionFirst_KeepsTheFirst()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(
                Candidate("Git.Git", installed: "1.5", available: "2.0"),
                Candidate("Git.Git", installed: "1.6", available: "2.1")),
            Records(),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal("1.5", row.InstalledVersion);
        Assert.Equal("2.0", row.AvailableVersion);
    }

    [Fact]
    public void DuplicateId_WithNoVersionEither_KeepsTheFirst()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(
                Candidate("Git.Git", installed: null, available: "2.0"),
                Candidate("Git.Git", installed: "Unknown", available: "2.1")),
            Records(),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal("2.0", row.AvailableVersion);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UnknownInstalledVersion_WithAvailable_IsVersionNotKnown(string? installed)
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(Candidate("Git.Git", installed: installed, available: "2.0")),
            Records(),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal(UpgradeGroup.VersionNotKnown, row.Group);
        Assert.Null(row.InstalledVersion);
        Assert.Equal("2.0", row.AvailableVersion);
    }

    [Fact]
    public void KnownInstalledVersion_IsReady()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(Candidate("Git.Git", installed: "1.0", available: "2.0")),
            Records(),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal(UpgradeGroup.Ready, row.Group);
        Assert.Equal("1.0", row.InstalledVersion);
    }

    [Fact]
    public void UpdatedToday_FromToday_IsIncluded()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(),
            Records(Updated("Python.Python.3.12", "3.12.7", Now, name: "Python 3.12")),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal(UpgradeGroup.UpdatedToday, row.Group);
        Assert.Null(row.InstalledVersion);
        Assert.Null(row.AvailableVersion);
        Assert.Equal("3.12.7", row.UpdatedVersion);
        Assert.Equal(Now, row.UpdatedAt);
    }

    [Fact]
    public void UpdatedToday_FromYesterday_IsExcluded()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(),
            Records(Updated("Python.Python.3.12", "3.12.7", Now.AddDays(-1))),
            Now);

        Assert.Empty(rows);
    }

    [Fact]
    public void ReadyRow_WithSameVersionUpdatedToday_IsDropped()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(Candidate("Git.Git", installed: "1.0", available: "2.0")),
            Records(Updated("Git.Git", "2.0", Now)),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal(UpgradeGroup.UpdatedToday, row.Group);
    }

    [Fact]
    public void VersionNotKnownRow_WithSameVersionUpdatedToday_IsDropped()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(Candidate("Git.Git", installed: "Unknown", available: "2.0")),
            Records(Updated("Git.Git", "2.0", Now)),
            Now);

        UpgradeEntry row = Assert.Single(rows);
        Assert.Equal(UpgradeGroup.UpdatedToday, row.Group);
    }

    [Fact]
    public void ReadyRow_WithDifferentVersionUpdatedToday_IsKept()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(Candidate("Git.Git", installed: "1.0", available: "2.1")),
            Records(Updated("Git.Git", "2.0", Now)),
            Now);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Group == UpgradeGroup.Ready && r.AvailableVersion == "2.1");
        Assert.Contains(rows, r => r.Group == UpgradeGroup.UpdatedToday && r.UpdatedVersion == "2.0");
    }

    [Fact]
    public void Rows_AreSortedByName_ThenCatalogId()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(
                Candidate("Zed.Zed", name: "zeta"),
                Candidate("Mozilla.Firefox", name: "Beta"),
                Candidate("Git.Git", name: "alpha"),
                Candidate("Adobe.Reader", name: "Beta")),
            Records(),
            Now);

        Assert.Equal(
            new[] { "Git.Git", "Adobe.Reader", "Mozilla.Firefox", "Zed.Zed" },
            rows.Select(r => r.CatalogId).ToArray());
    }

    [Fact]
    public void Rows_KeepTheGroupsMixed()
    {
        IReadOnlyList<UpgradeEntry> rows = UpdatesGrouping.Build(
            Candidates(
                Candidate("Zed.Zed", name: "Zed", installed: "Unknown"),
                Candidate("Git.Git", name: "Git")),
            Records(Updated("Abc.Abc", "1.0", Now, name: "Abc")),
            Now);

        Assert.Equal(
            new[] { UpgradeGroup.UpdatedToday, UpgradeGroup.Ready, UpgradeGroup.VersionNotKnown },
            rows.Select(r => r.Group).ToArray());
    }

    [Fact]
    public void UpdatedToday_TwiceForOneId_KeepsOnlyTheNewest()
    {
        var rows = UpdatesGrouping.Build(
            Candidates(),
            Records(
                Updated("Mozilla.Firefox", "129.0", Now.AddMinutes(-30)),
                Updated("Mozilla.Firefox", "129.0.1", Now.AddMinutes(-5))),
            Now);

        var row = Assert.Single(rows);
        Assert.Equal("129.0.1", row.UpdatedVersion);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    public void UnknownInstalledVersion_WithNoAvailable_StaysReady_WithNoVersionText(string installed)
    {
        var rows = UpdatesGrouping.Build(
            Candidates(Candidate("Mozilla.Firefox", installed: installed, available: null)),
            Records(),
            Now);

        var row = Assert.Single(rows);
        Assert.Equal(UpgradeGroup.Ready, row.Group);
        Assert.Null(row.InstalledVersion);
    }
}
