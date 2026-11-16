using System;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Inventory;
using Xunit;

namespace QuantumUpdater.Tests;

public class StoreAppRulesTests
{
    private const string SampleFamily = "Sample.App_8wekyb3d8bbwe";
    private const string SampleFull = "Sample.App_1.2.3.4_x64__8wekyb3d8bbwe";
    private const string SampleLogo = "ms-appx:///Assets/Logo.png";

    [Fact]
    public void Framework_package_is_skipped()
    {
        var fields = Fields(isFramework: true);

        Assert.Null(StoreAppRules.MapPackage(fields));
    }

    [Fact]
    public void Resource_package_is_skipped()
    {
        var fields = Fields(isResourcePackage: true);

        Assert.Null(StoreAppRules.MapPackage(fields));
    }

    [Fact]
    public void Bundle_package_is_skipped()
    {
        var fields = Fields(isBundle: true);

        Assert.Null(StoreAppRules.MapPackage(fields));
    }

    [Fact]
    public void System_signed_package_is_skipped()
    {
        var fields = Fields(signature: StoreSignature.System);

        Assert.Null(StoreAppRules.MapPackage(fields));
    }

    [Fact]
    public void Windows_Settings_is_skipped_by_name()
    {
        var fields = Fields(idName: "Microsoft.Windows.Settings", signature: StoreSignature.Store);

        Assert.Null(StoreAppRules.MapPackage(fields));
    }

    [Fact]
    public void Package_without_display_name_is_skipped()
    {
        var fields = Fields(displayName: "   ");

        Assert.Null(StoreAppRules.MapPackage(fields));
    }

    [Fact]
    public void Store_signed_package_maps_to_Microsoft_Store()
    {
        var entry = StoreAppRules.MapPackage(Fields(signature: StoreSignature.Store));

        Assert.NotNull(entry);
        Assert.Equal(AppSource.MicrosoftStore, entry!.Source);
    }

    [Fact]
    public void Developer_signed_package_maps_to_Installed_directly()
    {
        var entry = StoreAppRules.MapPackage(Fields(signature: StoreSignature.Developer));

        Assert.NotNull(entry);
        Assert.Equal(AppSource.InstalledDirectly, entry!.Source);
    }

    [Fact]
    public void Enterprise_signed_package_maps_to_Installed_directly()
    {
        var entry = StoreAppRules.MapPackage(Fields(signature: StoreSignature.Enterprise));

        Assert.NotNull(entry);
        Assert.Equal(AppSource.InstalledDirectly, entry!.Source);
    }

    [Fact]
    public void Mapped_row_has_the_Store_route_and_no_match()
    {
        var entry = StoreAppRules.MapPackage(Fields(signature: StoreSignature.Store));

        Assert.NotNull(entry);
        Assert.Equal("msix:" + SampleFamily, entry!.Key);
        Assert.Equal(UninstallRoute.Store, entry.Route);
        Assert.Equal(MatchKind.None, entry.Match);
        Assert.Equal(SampleFull, entry.StoreFullName);
        Assert.Null(entry.RegistryRef);
        Assert.False(entry.IsProtected);
    }

    [Fact]
    public void Name_publisher_version_and_install_fields_are_carried_over()
    {
        var installed = new DateTime(2026, 11, 1, 9, 30, 0);
        var entry = StoreAppRules.MapPackage(Fields(
            displayName: "Sample Calculator",
            publisher: "Sample Publisher",
            version: "10.2.3.4",
            installLocation: @"C:\Program Files\WindowsApps\Sample",
            installDate: installed));

        Assert.NotNull(entry);
        Assert.Equal("Sample Calculator", entry!.Name);
        Assert.Equal("Sample Publisher", entry.Publisher);
        Assert.Equal("10.2.3.4", entry.Version);
        Assert.Equal(@"C:\Program Files\WindowsApps\Sample", entry.InstallLocation);
        Assert.Equal(installed, entry.InstallDate);
    }

    [Fact]
    public void Logo_uri_is_carried_over_as_display_icon()
    {
        var entry = StoreAppRules.MapPackage(Fields(logoUri: SampleLogo));

        Assert.NotNull(entry);
        Assert.Equal(SampleLogo, entry!.DisplayIcon);
    }

    [Fact]
    public void Blank_optional_text_becomes_null()
    {
        var entry = StoreAppRules.MapPackage(Fields(publisher: " ", version: "", logoUri: null));

        Assert.NotNull(entry);
        Assert.Null(entry!.Publisher);
        Assert.Null(entry.Version);
        Assert.Null(entry.DisplayIcon);
    }

    private static StorePackageFields Fields(
        string idName = "Sample.App",
        bool isFramework = false,
        bool isResourcePackage = false,
        bool isBundle = false,
        StoreSignature signature = StoreSignature.Developer,
        string? displayName = "Sample App",
        string? publisher = "Sample Publisher",
        string? version = "1.2.3.4",
        string? installLocation = null,
        DateTime? installDate = null,
        string? logoUri = SampleLogo)
    {
        return new StorePackageFields(
            Flags: new StorePackageFlags(idName, isFramework, isResourcePackage, isBundle, signature),
            FamilyName: SampleFamily,
            FullName: SampleFull,
            DisplayName: displayName,
            PublisherDisplayName: publisher,
            Version: version,
            InstallLocation: installLocation,
            InstallDate: installDate,
            LogoUri: logoUri);
    }
}
