using System;
using System.Collections.Generic;
using System.Linq;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Updates;

/// <summary>
/// Turns the update candidates and today's updates into the rows of the Updates page.
/// Pure rules only. No WinUI and no COM types.
/// </summary>
public static class UpdatesGrouping
{
    /// <summary>Builds the rows. The rows are sorted by name, then by catalog id.</summary>
    public static IReadOnlyList<UpgradeEntry> Build(
        IReadOnlyList<UpdateCandidate> candidates,
        IReadOnlyList<UpdatedRecord> updatedToday,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(updatedToday);

        DateTime today = now.ToLocalTime().Date;
        Dictionary<string, UpdateCandidate> chosen = ChooseOnePerId(candidates);

        var rows = new List<UpgradeEntry>();
        foreach (UpdateCandidate candidate in chosen.Values)
        {
            rows.Add(ToCandidateRow(candidate));
        }

        // One "updated today" row per id: the newest record wins.
        IEnumerable<UpdatedRecord> latestToday = updatedToday
            .Where(record => record is not null
                && PackageId.IsCatalogId(record.CatalogId)
                && record.UpdatedAt.ToLocalTime().Date == today)
            .GroupBy(record => record.CatalogId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(record => record.UpdatedAt).First());

        foreach (UpdatedRecord record in latestToday)
        {
            rows.Add(new UpgradeEntry(
                record.CatalogId,
                record.Name,
                record.Publisher,
                InstalledVersion: null,
                AvailableVersion: null,
                SourceLabel: null,
                Group: UpgradeGroup.UpdatedToday,
                UpdatedAt: record.UpdatedAt,
                UpdatedVersion: record.Version));
        }

        List<UpgradeEntry> doneToday = rows
            .Where(row => row.Group == UpgradeGroup.UpdatedToday)
            .ToList();

        rows.RemoveAll(row => row.Group != UpgradeGroup.UpdatedToday && IsDone(row, doneToday));

        return rows
            .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.CatalogId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// One candidate per id. Skips ids that fail PackageId.IsCatalogId.
    /// When an id comes twice, keeps the one with a known installed version.
    /// When both or neither have one, keeps the first.
    /// </summary>
    private static Dictionary<string, UpdateCandidate> ChooseOnePerId(IReadOnlyList<UpdateCandidate> candidates)
    {
        var chosen = new Dictionary<string, UpdateCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (UpdateCandidate candidate in candidates)
        {
            if (candidate is null || !PackageId.IsCatalogId(candidate.CatalogId))
            {
                continue;
            }

            if (!chosen.TryGetValue(candidate.CatalogId, out UpdateCandidate? kept))
            {
                chosen[candidate.CatalogId] = candidate;
            }
            else if (!HasKnownVersion(kept.InstalledVersion) && HasKnownVersion(candidate.InstalledVersion))
            {
                chosen[candidate.CatalogId] = candidate;
            }
        }

        return chosen;
    }

    /// <summary>An unknown installed version with an available version is VersionNotKnown. Else Ready.</summary>
    private static UpgradeEntry ToCandidateRow(UpdateCandidate candidate)
    {
        bool notKnown = !HasKnownVersion(candidate.InstalledVersion)
            && !string.IsNullOrWhiteSpace(candidate.AvailableVersion);

        return new UpgradeEntry(
            candidate.CatalogId,
            candidate.Name,
            candidate.Publisher,
            // "Unknown" or empty never reaches the screen as text; null shows "Not known".
            InstalledVersion: HasKnownVersion(candidate.InstalledVersion) ? candidate.InstalledVersion!.Trim() : null,
            AvailableVersion: candidate.AvailableVersion,
            SourceLabel: candidate.SourceLabel,
            Group: notKnown ? UpgradeGroup.VersionNotKnown : UpgradeGroup.Ready,
            UpdatedAt: null,
            UpdatedVersion: null);
    }

    /// <summary>True when DeskVerve already updated this id today to the same version.</summary>
    private static bool IsDone(UpgradeEntry row, List<UpgradeEntry> doneToday)
    {
        if (row.AvailableVersion is null)
        {
            return false;
        }

        return doneToday.Any(done =>
            done.UpdatedVersion is not null
            && string.Equals(done.CatalogId, row.CatalogId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(done.UpdatedVersion, row.AvailableVersion, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A version is known when it is not empty, not white space and not "Unknown".</summary>
    private static bool HasKnownVersion(string? version)
    {
        return !string.IsNullOrWhiteSpace(version)
            && !string.Equals(version.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase);
    }
}
