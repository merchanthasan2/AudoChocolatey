using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Updates;

/// <summary>Reads available updates via the winget COM composite catalog (46.7.67).</summary>
public sealed class ComUpdatesSource : IUpdatesSource
{
    private readonly WingetReader _reader;

    public ComUpdatesSource(WingetReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<IReadOnlyList<UpdateCandidate>> ReadAsync(IProgress<int> step, CancellationToken token)
    {
        step.Report(1);
        var installed = await _reader.ListInstalledAsync(token).ConfigureAwait(false);
        step.Report(2);

        var candidates = new List<UpdateCandidate>();
        foreach (var app in installed)
        {
            if (string.IsNullOrEmpty(app.CatalogId) || !PackageId.IsCatalogId(app.CatalogId))
            {
                continue;
            }

            var hasUnknownVersion = (string.IsNullOrWhiteSpace(app.InstalledVersion)
                    || string.Equals(app.InstalledVersion.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(app.AvailableVersion);

            if (!app.IsUpdateAvailable && !hasUnknownVersion)
            {
                continue;
            }

            candidates.Add(new UpdateCandidate(
                CatalogId: app.CatalogId,
                Name: string.IsNullOrWhiteSpace(app.Name) ? app.CatalogId : app.Name,
                Publisher: app.Publisher,
                InstalledVersion: app.InstalledVersion,
                AvailableVersion: app.AvailableVersion,
                SourceLabel: app.SourceName ?? "Microsoft catalog"));
        }

        return candidates;
    }
}
