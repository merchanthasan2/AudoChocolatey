namespace QuantumUpdater.Services.Updates;

/// <summary>Reads available updates using the CLI backup (<see cref="WingetService"/>).</summary>
public sealed class CliUpdatesSource : IUpdatesSource
{
    private readonly WingetService _winget;

    public CliUpdatesSource(WingetService winget)
    {
        _winget = winget ?? throw new ArgumentNullException(nameof(winget));
    }

    public async Task<IReadOnlyList<UpdateCandidate>> ReadAsync(IProgress<int> step, CancellationToken token)
    {
        step.Report(1);
        var packages = await _winget.ListUpgradesAsync(token).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(_winget.LastError))
        {
            throw new InvalidOperationException(_winget.LastError);
        }

        step.Report(2);
        return packages.Select(p => new UpdateCandidate(
            CatalogId: p.Id,
            Name: p.Name,
            Publisher: null,
            InstalledVersion: p.Version,
            AvailableVersion: p.Available,
            SourceLabel: "Microsoft catalog")).ToList();
    }
}
