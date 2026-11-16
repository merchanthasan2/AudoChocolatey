using System;
using QuantumUpdater.Models;

namespace QuantumUpdater.Services.Inventory;

/// <summary>The package signature kinds that the Store app rules use.</summary>
public enum StoreSignature
{
    Unknown,
    None,
    Developer,
    Enterprise,
    System,
    Store,
}

/// <summary>The package fields that are fast to read on this PC.</summary>
public sealed record StorePackageFlags(
    string IdName,
    bool IsFramework,
    bool IsResourcePackage,
    bool IsBundle,
    StoreSignature Signature);

/// <summary>The package fields that the Store app rules and the mapping use. It has no WinRT types.</summary>
public sealed record StorePackageFields(
    StorePackageFlags Flags,
    string FamilyName,
    string FullName,
    string? DisplayName,
    string? PublisherDisplayName,
    string? Version,
    string? InstallLocation,
    DateTime? InstallDate,
    string? LogoUri);

/// <summary>
/// Filter and mapping rules for Store and MSIX apps (section 5.2).
/// This file has no WinRT types, so unit tests can use it.
/// </summary>
public static class StoreAppRules
{
    private const string SettingsPackageName = "Microsoft.Windows.Settings";

    /// <summary>True when the package is a framework, a resource, a bundle, a system part or Windows Settings.</summary>
    internal static bool IsSkipped(StorePackageFlags flags)
    {
        if (flags.IsFramework || flags.IsResourcePackage || flags.IsBundle)
        {
            return true;
        }

        if (flags.Signature == StoreSignature.System)
        {
            return true;
        }

        return string.Equals(flags.IdName, SettingsPackageName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Applies the filter and the mapping to one package.</summary>
    /// <returns>The row, or null when the filter skips the package.</returns>
    internal static AppEntry? MapPackage(StorePackageFields fields)
    {
        if (IsSkipped(fields.Flags))
        {
            return null;
        }

        var name = Clean(fields.DisplayName);
        if (name is null)
        {
            return null;
        }

        var source = fields.Flags.Signature == StoreSignature.Store
            ? AppSource.MicrosoftStore
            : AppSource.InstalledDirectly;

        return new AppEntry(
            Key: "msix:" + fields.FamilyName,
            Name: name,
            Publisher: Clean(fields.PublisherDisplayName),
            Version: Clean(fields.Version),
            Source: source,
            CatalogId: null,
            RegistryRef: null,
            StoreFullName: fields.FullName,
            Match: MatchKind.None,
            Route: UninstallRoute.Store,
            IsProtected: false,
            SizeBytes: null,
            InstallDate: fields.InstallDate,
            UpdateVersion: null,
            DisplayIcon: Clean(fields.LogoUri),
            InstallLocation: Clean(fields.InstallLocation),
            ProductCode: null,
            UninstallString: null,
            QuietUninstallString: null);
    }

    private static string? Clean(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
