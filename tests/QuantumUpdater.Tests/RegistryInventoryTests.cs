using System;
using System.Collections.Generic;
using Microsoft.Win32;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Inventory;
using Xunit;

namespace QuantumUpdater.Tests;

public class RegistryInventoryTests
{
    private const string PlainKey = "SomeApp_is1";
    private const string GuidKey = "{8A2B1C3D-4E5F-4A6B-9C7D-0E1F2A3B4C5D}";

    [Fact]
    public void Plain_entry_is_kept_with_its_fields()
    {
        var values = Values(
            ("DisplayName", "Sample Tool"),
            ("DisplayVersion", "1.2.3"),
            ("Publisher", "Sample Publisher"));

        var entry = Map(PlainKey, values);

        Assert.NotNull(entry);
        Assert.Equal("Sample Tool", entry!.Name);
        Assert.Equal("1.2.3", entry.Version);
        Assert.Equal("Sample Publisher", entry.Publisher);
        Assert.Equal(AppSource.InstalledDirectly, entry.Source);
        Assert.Equal(MatchKind.None, entry.Match);
        Assert.Equal(UninstallRoute.None, entry.Route);
        Assert.False(entry.IsProtected);
        Assert.NotNull(entry.RegistryRef);
        Assert.Equal(RegistryHive.LocalMachine, entry.RegistryRef!.Hive);
        Assert.Equal(RegistryView.Registry64, entry.RegistryRef.View);
        Assert.Equal(PlainKey, entry.RegistryRef.KeyName);
    }

    [Fact]
    public void Entry_with_empty_display_name_is_skipped()
    {
        var values = Values(("DisplayName", "   "));

        Assert.Null(Map(PlainKey, values));
    }

    [Fact]
    public void Entry_without_display_name_is_skipped()
    {
        var values = Values(("DisplayVersion", "1.0"));

        Assert.Null(Map(PlainKey, values));
    }

    [Fact]
    public void System_component_entry_is_skipped()
    {
        var values = Values(("DisplayName", "Hidden Part"), ("SystemComponent", 1));

        Assert.Null(Map(PlainKey, values));
    }

    [Fact]
    public void System_component_zero_is_kept()
    {
        var values = Values(("DisplayName", "Visible Part"), ("SystemComponent", 0));

        Assert.NotNull(Map(PlainKey, values));
    }

    [Fact]
    public void Child_entry_with_parent_key_name_is_skipped()
    {
        var values = Values(("DisplayName", "Child Patch"), ("ParentKeyName", "ParentApp"));

        Assert.Null(Map(PlainKey, values));
    }

    [Theory]
    [InlineData("Update")]
    [InlineData("Hotfix")]
    [InlineData("Security Update")]
    [InlineData("security update")]
    public void Update_release_types_are_skipped(string releaseType)
    {
        var values = Values(("DisplayName", "Some Patch"), ("ReleaseType", releaseType));

        Assert.Null(Map(PlainKey, values));
    }

    [Fact]
    public void Other_release_type_is_kept()
    {
        var values = Values(("DisplayName", "Real App"), ("ReleaseType", "Application"));

        Assert.NotNull(Map(PlainKey, values));
    }

    [Theory]
    [InlineData("KB5001234")]
    [InlineData("KB12")]
    public void KB_names_are_skipped(string name)
    {
        var values = Values(("DisplayName", name));

        Assert.Null(Map(PlainKey, values));
    }

    [Theory]
    [InlineData("KB")]
    [InlineData("KBX123")]
    [InlineData("My KB5001234")]
    public void Names_that_are_not_KB_numbers_are_kept(string name)
    {
        var values = Values(("DisplayName", name));

        Assert.NotNull(Map(PlainKey, values));
    }

    [Theory]
    [InlineData("Update for Windows")]
    [InlineData("Security Update for Office")]
    [InlineData("Hotfix for Something")]
    public void Update_name_prefixes_are_skipped(string name)
    {
        var values = Values(("DisplayName", name));

        Assert.Null(Map(PlainKey, values));
    }

    [Fact]
    public void No_remove_entry_is_kept_and_marked_protected()
    {
        var values = Values(("DisplayName", "Locked App"), ("NoRemove", 1));

        var entry = Map(PlainKey, values);

        Assert.NotNull(entry);
        Assert.True(entry!.IsProtected);
    }

    [Fact]
    public void Size_in_kilobytes_becomes_bytes()
    {
        var values = Values(("DisplayName", "Sized App"), ("EstimatedSize", 1024));

        var entry = Map(PlainKey, values);

        Assert.Equal(1024L * 1024L, entry!.SizeBytes);
    }

    [Fact]
    public void Missing_or_zero_size_is_null()
    {
        Assert.Null(Map(PlainKey, Values(("DisplayName", "A")))!.SizeBytes);
        Assert.Null(Map(PlainKey, Values(("DisplayName", "A"), ("EstimatedSize", 0)))!.SizeBytes);
    }

    [Fact]
    public void Install_date_parses_yyyyMMdd()
    {
        var values = Values(("DisplayName", "Dated App"), ("InstallDate", "20240315"));

        var entry = Map(PlainKey, values);

        Assert.Equal(new DateTime(2024, 3, 15), entry!.InstallDate);
    }

    [Theory]
    [InlineData("20241340")]
    [InlineData("2024-03-15")]
    [InlineData("")]
    public void Bad_install_date_is_null(string text)
    {
        var values = Values(("DisplayName", "Dated App"), ("InstallDate", text));

        Assert.Null(Map(PlainKey, values)!.InstallDate);
    }

    [Fact]
    public void Guid_key_with_windows_installer_is_product_code()
    {
        var values = Values(("DisplayName", "MSI App"), ("WindowsInstaller", 1));

        var entry = Map(GuidKey, values);

        Assert.Equal(GuidKey, entry!.ProductCode);
    }

    [Fact]
    public void Guid_key_without_windows_installer_is_not_product_code()
    {
        var values = Values(("DisplayName", "Not MSI"), ("WindowsInstaller", 0));

        Assert.Null(Map(GuidKey, values)!.ProductCode);
    }

    [Fact]
    public void Plain_key_with_windows_installer_is_not_product_code()
    {
        var values = Values(("DisplayName", "Not GUID"), ("WindowsInstaller", 1));

        Assert.Null(Map(PlainKey, values)!.ProductCode);
    }

    [Fact]
    public void Is_product_code_accepts_only_braced_guids()
    {
        Assert.True(RegistryInventory.IsProductCode(GuidKey));
        Assert.False(RegistryInventory.IsProductCode("8A2B1C3D-4E5F-4A6B-9C7D-0E1F2A3B4C5D"));
        Assert.False(RegistryInventory.IsProductCode(PlainKey));
    }

    [Fact]
    public void Uninstall_strings_are_kept_raw()
    {
        var raw = @"%LOCALAPPDATA%\Sample\uninstall.exe /S";
        var quiet = @"%LOCALAPPDATA%\Sample\uninstall.exe /quiet";
        var values = Values(
            ("DisplayName", "Raw App"),
            ("UninstallString", raw),
            ("QuietUninstallString", quiet),
            ("DisplayIcon", @"C:\Sample\app.exe,0"),
            ("InstallLocation", @"C:\Sample"));

        var entry = Map(PlainKey, values);

        Assert.Equal(raw, entry!.UninstallString);
        Assert.Equal(quiet, entry.QuietUninstallString);
        Assert.Equal(@"C:\Sample\app.exe,0", entry.DisplayIcon);
        Assert.Equal(@"C:\Sample", entry.InstallLocation);
    }

    [Fact]
    public void Value_names_are_case_insensitive()
    {
        var values = Values(("displayname", "Lower Case App"), ("systemcomponent", 1));

        Assert.Null(Map(PlainKey, values));
    }

    [Fact]
    public void Same_app_in_two_views_becomes_one_row_and_first_is_kept()
    {
        var first = Map(PlainKey, Values(("DisplayName", "Twin App"), ("DisplayVersion", "2.0"), ("Publisher", "Pub")));
        var second = Map(PlainKey, Values(("DisplayName", "Twin App"), ("DisplayVersion", "2.0"), ("Publisher", "Pub")))! with
        {
            RegistryRef = new RegistryRef(RegistryHive.LocalMachine, RegistryView.Registry32, PlainKey),
        };

        var rows = RegistryInventory.RemoveDuplicates(new[] { first!, second });

        Assert.Single(rows);
        Assert.Equal(RegistryView.Registry64, rows[0].RegistryRef!.View);
    }

    [Fact]
    public void Same_name_with_different_version_is_kept_twice()
    {
        var first = Map(PlainKey, Values(("DisplayName", "Twin App"), ("DisplayVersion", "2.0"), ("Publisher", "Pub")));
        var second = Map(PlainKey, Values(("DisplayName", "Twin App"), ("DisplayVersion", "3.0"), ("Publisher", "Pub")));

        var rows = RegistryInventory.RemoveDuplicates(new[] { first!, second! });

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Same_name_and_version_with_different_publisher_is_kept_twice()
    {
        var first = Map(PlainKey, Values(("DisplayName", "Twin App"), ("DisplayVersion", "2.0"), ("Publisher", "Pub A")));
        var second = Map(PlainKey, Values(("DisplayName", "Twin App"), ("DisplayVersion", "2.0"), ("Publisher", "Pub B")));

        var rows = RegistryInventory.RemoveDuplicates(new[] { first!, second! });

        Assert.Equal(2, rows.Count);
    }

    private static AppEntry? Map(string keyName, IReadOnlyDictionary<string, object?> values)
    {
        var reference = new RegistryRef(RegistryHive.LocalMachine, RegistryView.Registry64, keyName);
        return RegistryInventory.MapEntry(reference, values);
    }

    private static IReadOnlyDictionary<string, object?> Values(params (string Name, object Value)[] pairs)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in pairs)
        {
            values[name] = value;
        }

        return values;
    }
}
