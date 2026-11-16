using System;

namespace QuantumUpdater.Models;

/// <summary>The group a row shows in on the Updates page.</summary>
public enum UpgradeGroup
{
    Ready,
    VersionNotKnown,
    UpdatedToday,
}

/// <summary>
/// One row on the Updates page.
/// InstalledVersion null means the installed version is not known.
/// </summary>
public sealed record UpgradeEntry(
    string CatalogId,
    string Name,
    string? Publisher,
    string? InstalledVersion,
    string? AvailableVersion,
    string? SourceLabel,
    UpgradeGroup Group,
    DateTimeOffset? UpdatedAt,
    string? UpdatedVersion);
