namespace QuantumUpdater.Services.Updates;

/// <summary>An app that has a new version, or may have one.</summary>
public sealed record UpdateCandidate(
    string CatalogId,
    string Name,
    string? Publisher,
    string? InstalledVersion,
    string? AvailableVersion,
    string? SourceLabel);

/// <summary>An update that DeskVerve did today.</summary>
public sealed record UpdatedRecord(
    string CatalogId,
    string Name,
    string? Publisher,
    string Version,
    DateTimeOffset UpdatedAt);

/// <summary>Reads the update candidates. Step is the step number reached (1 to 3).</summary>
public interface IUpdatesSource
{
    Task<IReadOnlyList<UpdateCandidate>> ReadAsync(IProgress<int> step, CancellationToken token);
}
