using System.Text.RegularExpressions;
using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public class WingetTextTableTests
{
    private const string Ellipsis = "…";

    private static string ReadData(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", fileName));
    }

    private static string Spaces(int count)
    {
        return new string(' ', count);
    }

    /// <summary>
    /// Counts the data lines between the separator line and the footer.
    /// This count is made without the parser, so it checks the parser.
    /// </summary>
    private static int CountDataLines(string text)
    {
        var lines = text.Split('\n').Select(line => line.Trim()).ToList();
        var separator = lines.FindIndex(line => line.Length > 0 && line.All(c => c == '-'));
        Assert.True(separator >= 0, "The table has no separator line.");

        var count = 0;
        for (var i = separator + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            if (Regex.IsMatch(line, @"^\d+\s+upgrades?\s+available", RegexOptions.IgnoreCase)
                || line.StartsWith("The following packages", StringComparison.Ordinal))
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static string Header()
    {
        return "Name" + Spaces(20) + "Id" + Spaces(22) + "Version" + Spaces(7) + "Available" + Spaces(3) + "Source";
    }

    private static string Separator()
    {
        return new string('-', 80);
    }

    [Fact]
    public void Sample_KeepsEveryDataRow()
    {
        var text = ReadData("winget-sample.txt");

        var rows = WingetTextTable.Parse(text);

        Assert.Equal(CountDataLines(text), rows.Count);
        Assert.Equal(9, rows.Count);
    }

    [Fact]
    public void Sample_GivesAvailableValues()
    {
        var rows = WingetTextTable.Parse(ReadData("winget-sample.txt"));

        var teams = Assert.Single(rows, row => row.Name == "Microsoft Teams");
        Assert.Equal("Microsoft.Teams", teams.Id);
        Assert.Equal("26276.513.5241.9675", teams.Version);
        Assert.Equal("26288.2004.5269.3420", teams.Available);
        Assert.Equal("winget", teams.Source);
        Assert.False(teams.IsLocal);
        Assert.False(teams.IsCut);
    }

    [Fact]
    public void RealList_KeepsEveryDataRow()
    {
        var text = ReadData("winget-list.txt");

        var rows = WingetTextTable.Parse(text);

        Assert.NotEmpty(rows);
        Assert.Equal(CountDataLines(text), rows.Count);
    }

    [Fact]
    public void RealUpgrade_KeepsEveryDataRow()
    {
        var text = ReadData("winget-upgrade.txt");

        var rows = WingetTextTable.Parse(text);

        Assert.Equal(CountDataLines(text), rows.Count);
        Assert.Single(rows);
    }

    [Fact]
    public void RealList_ArpAndMsixRows_ArePresentAndLocal()
    {
        var rows = WingetTextTable.Parse(ReadData("winget-list.txt"));

        var arp = rows.Where(row => row.Id.StartsWith(@"ARP\", StringComparison.Ordinal)).ToList();
        var msix = rows.Where(row => row.Id.StartsWith(@"MSIX\", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(arp);
        Assert.NotEmpty(msix);
        Assert.All(arp, row => Assert.True(row.IsLocal));
        Assert.All(msix, row => Assert.True(row.IsLocal));
        Assert.All(arp.Concat(msix), row => Assert.False(row.IsCut));
    }

    [Fact]
    public void RealList_LocalIdWithSpace_KeepsFullIdAndVersion()
    {
        var rows = WingetTextTable.Parse(ReadData("winget-list.txt"));

        var studio = Assert.Single(rows, row => row.Name == "Sample App 005");
        Assert.Equal(@"ARP\Machine\X64\Sample A 005", studio.Id);
        Assert.Equal("1.5.0", studio.Version);
        Assert.True(studio.IsLocal);
    }

    [Fact]
    public void RealList_MsixRowWithoutSource_HasNullSource()
    {
        var rows = WingetTextTable.Parse(ReadData("winget-list.txt"));

        var acrobat = Assert.Single(rows, row => row.Name == "Sample App 001");
        Assert.StartsWith(@"MSIX\", acrobat.Id);
        Assert.Null(acrobat.Source);
        Assert.Null(acrobat.Available);
        Assert.True(acrobat.IsLocal);
    }

    [Fact]
    public void RealUpgrade_GivesAvailableValue()
    {
        var rows = WingetTextTable.Parse(ReadData("winget-upgrade.txt"));

        var jdk = Assert.Single(rows);
        Assert.Equal("Java(TM) SE Development Kit 25.0.5.0.3 (64-bit)", jdk.Name);
        Assert.Equal("Oracle.JDK.25", jdk.Id);
        Assert.Equal("25.0.5.0", jdk.Version);
        Assert.Equal("25.0.5.0.3", jdk.Available);
        Assert.Equal("winget", jdk.Source);
        Assert.False(jdk.IsLocal);
        Assert.False(jdk.IsCut);
    }

    [Fact]
    public void IdEndingWithEllipsis_IsMarkedCut()
    {
        var text = string.Join("\r\n",
            Header(),
            Separator(),
            "Acme Tool" + Spaces(15) + "Acme.Tool.Long.Identi" + Ellipsis + Spaces(2) + "3.1" + Spaces(11) + Spaces(12) + "winget",
            "1 upgrades available.");

        var rows = WingetTextTable.Parse(text);

        var row = Assert.Single(rows);
        Assert.Equal("Acme.Tool.Long.Identi" + Ellipsis, row.Id);
        Assert.Equal("3.1", row.Version);
        Assert.True(row.IsCut);
        Assert.False(row.IsLocal);
        Assert.False(PackageId.IsCatalogId(row.Id));
    }

    [Fact]
    public void IdEndingWithThreeDots_IsMarkedCut()
    {
        var text = string.Join("\r\n",
            Header(),
            Separator(),
            "Acme Tool" + Spaces(15) + "Acme.Tool..." + Spaces(12) + "3.1" + Spaces(11) + Spaces(12) + "winget");

        var row = Assert.Single(WingetTextTable.Parse(text));

        Assert.Equal("Acme.Tool...", row.Id);
        Assert.True(row.IsCut);
    }

    [Fact]
    public void CjkName_KeepsRightIdAndVersion()
    {
        var text = string.Join("\r\n",
            Header(),
            Separator(),
            "日本語アプリ" + Spaces(12) + "Vendor.Japanese" + Spaces(9) + "1.2.3" + Spaces(9) + Spaces(12) + "winget",
            "2 upgrades available.");

        var row = Assert.Single(WingetTextTable.Parse(text));

        Assert.Equal("日本語アプリ", row.Name);
        Assert.Equal("Vendor.Japanese", row.Id);
        Assert.Equal("1.2.3", row.Version);
        Assert.Null(row.Available);
        Assert.Equal("winget", row.Source);
        Assert.False(row.IsCut);
    }

    [Fact]
    public void EmojiName_CountsTwoCells_AndKeepsIdAndVersion()
    {
        var text = string.Join("\r\n",
            Header(),
            Separator(),
            "\U0001F3AE Game" + Spaces(17) + "Vendor.Game" + Spaces(13) + "2.0" + Spaces(11) + "2.1" + Spaces(9) + "winget");

        var row = Assert.Single(WingetTextTable.Parse(text));

        Assert.Equal("\U0001F3AE Game", row.Name);
        Assert.Equal("Vendor.Game", row.Id);
        Assert.Equal("2.0", row.Version);
        Assert.Equal("2.1", row.Available);
        Assert.Equal("winget", row.Source);
    }

    [Fact]
    public void FooterAndProgressLines_AreNotRows()
    {
        var text = "-\r  \\\r  |\r\n" +
            "████ 50%\r\n" +
            string.Join("\r\n",
                Header(),
                Separator(),
                "Acme Tool" + Spaces(15) + "Acme.Tool" + Spaces(15) + "3.1" + Spaces(11) + "3.2" + Spaces(9) + "winget",
                "1 upgrades available.",
                "The following packages have an upgrade available, but cannot be upgraded:");

        var rows = WingetTextTable.Parse(text);

        var row = Assert.Single(rows);
        Assert.Equal("Acme Tool", row.Name);
        Assert.Equal("Acme.Tool", row.Id);
    }

    [Fact]
    public void ByteOrderMark_IsIgnored()
    {
        var text = "﻿" + string.Join("\r\n",
            Header(),
            Separator(),
            "Acme Tool" + Spaces(15) + "Acme.Tool" + Spaces(15) + "3.1" + Spaces(11) + Spaces(12) + "winget");

        var row = Assert.Single(WingetTextTable.Parse(text));

        Assert.Equal("Acme.Tool", row.Id);
        Assert.Equal("3.1", row.Version);
        Assert.Equal("winget", row.Source);
    }

    [Fact]
    public void EmptyText_GivesNoRows()
    {
        Assert.Empty(WingetTextTable.Parse(string.Empty));
        Assert.Empty(WingetTextTable.Parse("No installed package found matching input criteria."));
    }
}
