namespace QuantumUpdater.Services.Winget;

/// <summary>
/// One package as the winget COM API reports it.
/// LocalId is the Id the composite catalog gives. For apps known to winget it is the catalog id.
/// For apps unknown to winget it is a local id (ARP\ or MSIX\).
/// </summary>
public sealed record CatalogApp(
    string LocalId,
    string? CatalogId,
    string Name,
    string? Publisher,
    string? InstalledVersion,
    string? AvailableVersion,
    bool IsUpdateAvailable,
    IReadOnlyList<string> ProductCodes,
    IReadOnlyList<string> FamilyNames,
    string? SourceName = null);

/// <summary>One manifest icon. Sha256 is upper-case hex, or null when winget gives none.</summary>
public sealed record CatalogIcon(string Url, string? Sha256, string? FileType);

/// <summary>Details of one catalog package, from the winget catalog.</summary>
public sealed record CatalogDetails(
    string CatalogId,
    string Name,
    string? Publisher,
    string? Description,
    string? Homepage,
    string? License,
    string? LatestVersion,
    IReadOnlyList<CatalogIcon> Icons);

/// <summary>
/// Thrown when a winget COM read fails, times out or returns a bad status.
/// The caller falls back to the text reader.
/// </summary>
public sealed class WingetReaderException : Exception
{
    public WingetReaderException(string message)
        : base(message)
    {
    }

    public WingetReaderException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
