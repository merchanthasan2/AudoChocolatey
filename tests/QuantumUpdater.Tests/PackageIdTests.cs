using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public class PackageIdTests
{
    public static TheoryData<string?> GoodCatalogIds => new()
    {
        "Mozilla.Firefox",
        "Microsoft.VisualStudioCode",
        "7zip.7zip",
        "Git.Git",
        "Google.Chrome.EXE",
        "Notepad++.Notepad++",
        "Microsoft.PowerToys",
        "VideoLAN.VLC",
        "Python.Python.3.12",
        "9NKSQGP7F2NH",
        "OpenJS.NodeJS.LTS",
        "a",
        "A_b-c+d.e",
        new string('a', 181),
    };

    public static TheoryData<string?> BadCatalogIds => new()
    {
        "",
        null,
        "-x",
        "_x",
        ".x",
        "a b",
        " Git.Git",
        "Git.Git ",
        "ARP\\Machine\\X64\\Foo",
        "MSIX\\Foo_1.0",
        "Foo…",
        "Foo...",
        "a;b",
        "a\"b",
        "a/b",
        "a|b",
        "a<b>",
        "a\u0001b",
        new string('a', 182),
    };

    public static TheoryData<string?> GoodLocalIds => new()
    {
        "ARP\\Machine\\X64\\Foo",
        "ARP\\User\\X86\\Bar_1.0",
        "MSIX\\Foo_1.0.0.0_x64__abc",
        "arp\\lowercase",
        "msix\\lowercase",
        "ARP\\Some Name With Spaces",
        "ARP\\Machine\\X64\\Foo+Bar",
        "ARP\\Machine\\X64\\Ünïcode",
        "ARP\\Machine\\X64\\{6A1B2C3D-0000-1111-2222-333344445555}",
        "ARP\\Machine\\X64\\Foo(1)",
        "ARP\\" + new string('a', 396),
        "MSIX\\" + new string('b', 395),
    };

    public static TheoryData<string?> BadLocalIds => new()
    {
        null,
        "",
        "Mozilla.Firefox",
        "ARP/Machine/X64/Foo",
        "XARP\\Foo",
        "ARP\\Foo\"x",
        "ARP\\a<b",
        "ARP\\a>b",
        "ARP\\a|b",
        "ARP\\a\tb",
        "ARP\\a\nb",
        "ARP\\a\u0007b",
        "ARP\\" + new string('a', 397),
        "MSIX\\" + new string('a', 400),
    };

    public static TheoryData<string?> CutIds => new()
    {
        "Foo…",
        "Foo...",
        "ARP\\Machine\\X64\\Some long na…",
        "MSIX\\Foo_1.0...",
    };

    [Theory]
    [MemberData(nameof(GoodCatalogIds))]
    public void IsCatalogId_accepts_good_ids(string? id)
    {
        Assert.True(PackageId.IsCatalogId(id));
    }

    [Theory]
    [MemberData(nameof(BadCatalogIds))]
    public void IsCatalogId_rejects_bad_ids(string? id)
    {
        Assert.False(PackageId.IsCatalogId(id));
    }

    [Theory]
    [MemberData(nameof(GoodLocalIds))]
    public void IsLocalId_accepts_good_ids(string? id)
    {
        Assert.True(PackageId.IsLocalId(id));
    }

    [Theory]
    [MemberData(nameof(BadLocalIds))]
    public void IsLocalId_rejects_bad_ids(string? id)
    {
        Assert.False(PackageId.IsLocalId(id));
    }

    [Theory]
    [MemberData(nameof(GoodLocalIds))]
    public void Local_ids_never_pass_IsCatalogId(string? id)
    {
        Assert.False(PackageId.IsCatalogId(id));
    }

    [Theory]
    [MemberData(nameof(CutIds))]
    public void Cut_ids_never_pass_IsCatalogId(string? id)
    {
        Assert.False(PackageId.IsCatalogId(id));
    }

    [Theory]
    [MemberData(nameof(CutIds))]
    public void IsCut_detects_cut_ids(string? id)
    {
        Assert.True(PackageId.IsCut(id));
    }

    [Theory]
    [InlineData("Mozilla.Firefox")]
    [InlineData("Foo..")]
    [InlineData("Foo.")]
    [InlineData("Foo…x")]
    [InlineData("a...b")]
    [InlineData("")]
    [InlineData(null)]
    public void IsCut_rejects_whole_ids(string? id)
    {
        Assert.False(PackageId.IsCut(id));
    }

    [Fact]
    public void IsCatalogId_rejects_trailing_newline()
    {
        Assert.False(PackageId.IsCatalogId("Foo\n"));
    }
}
