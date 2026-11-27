using Microsoft.Win32;

namespace QuantumUpdater.Models;

/// <summary>Where an installed app row comes from.</summary>
public enum AppSource
{
    MicrosoftCatalog,
    MicrosoftStore,
    InstalledDirectly,
    Scoop,
    Chocolatey,
}

/// <summary>How sure the match between a local app and a catalog entry is.</summary>
public enum MatchKind
{
    Exact,
    Likely,
    None,
}

/// <summary>The way DeskVerve removes an app.</summary>
public enum UninstallRoute
{
    Winget,
    Msi,
    OwnUninstaller,
    Store,
    Scoop,
    Chocolatey,
    None,
}

/// <summary>Points to the uninstall key that an app row was read from.</summary>
public sealed record RegistryRef(RegistryHive Hive, RegistryView View, string KeyName);

/// <summary>One row on the Installed screen.</summary>
public sealed record AppEntry(
    string Key,
    string Name,
    string? Publisher,
    string? Version,
    AppSource Source,
    string? CatalogId,
    RegistryRef? RegistryRef,
    string? StoreFullName,
    MatchKind Match,
    UninstallRoute Route,
    bool IsProtected,
    long? SizeBytes,
    DateTime? InstallDate,
    string? UpdateVersion,
    string? DisplayIcon,
    string? InstallLocation,
    string? ProductCode,
    string? UninstallString,
    string? QuietUninstallString);
