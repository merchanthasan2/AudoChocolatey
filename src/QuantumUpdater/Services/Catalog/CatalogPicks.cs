using System.Text.Json;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Catalog;

public sealed record CatalogCategory(string Id, string Title, string Tag, string Query);

public sealed record CatalogPick(
    string Id,
    string Name,
    string Publisher,
    string Category,
    bool Popular,
    string Summary,
    string Source = "winget",
    string? Bucket = null,
    string? BucketUrl = null,
    string? Homepage = null,
    string? License = null,
    string? Version = null);

public sealed record PicksCatalog(
    IReadOnlyList<CatalogCategory> Categories,
    IReadOnlyList<CatalogPick> Picks);

/// <summary>
/// Loads curated Marketplace picks and categories from Assets/catalog-picks.json (46.6.3).
/// Supports Winget, Microsoft Store, Scoop (including custom HTTPS buckets), and Chocolatey.
/// </summary>
public static class CatalogPicks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static PicksCatalog LoadDefault()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "catalog-picks.json");
        return LoadFromFile(path);
    }

    public static PicksCatalog LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new PicksCatalog(Array.Empty<CatalogCategory>(), Array.Empty<CatalogPick>());
        }

        try
        {
            var json = File.ReadAllText(path);
            return Parse(json);
        }
        catch
        {
            return new PicksCatalog(Array.Empty<CatalogCategory>(), Array.Empty<CatalogPick>());
        }
    }

    public static PicksCatalog Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PicksCatalog(Array.Empty<CatalogCategory>(), Array.Empty<CatalogPick>());
        }

        try
        {
            var dto = JsonSerializer.Deserialize<PicksDto>(json, JsonOptions);
            if (dto is null)
            {
                return new PicksCatalog(Array.Empty<CatalogCategory>(), Array.Empty<CatalogPick>());
            }

            var categories = (dto.Categories ?? new List<CategoryDto>())
                .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Title))
                .Select(c => new CatalogCategory(
                    c.Id!.Trim(),
                    c.Title!.Trim(),
                    string.IsNullOrWhiteSpace(c.Tag) ? c.Id!.Trim() : c.Tag!.Trim(),
                    string.IsNullOrWhiteSpace(c.Query) ? c.Title!.Trim() : c.Query!.Trim()))
                .ToList();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var picks = new List<CatalogPick>();
            foreach (var p in dto.Picks ?? new List<PickDto>())
            {
                if (string.IsNullOrWhiteSpace(p.Id))
                {
                    continue;
                }

                var id = p.Id.Trim();
                var source = string.IsNullOrWhiteSpace(p.Source) ? "winget" : p.Source.Trim().ToLowerInvariant();
                if (!PackageId.IsCatalogId(id) || !seen.Add($"{source}:{id}"))
                {
                    continue;
                }

                picks.Add(new CatalogPick(
                    id,
                    string.IsNullOrWhiteSpace(p.Name) ? id : p.Name.Trim(),
                    string.IsNullOrWhiteSpace(p.Publisher) ? "Community" : p.Publisher.Trim(),
                    string.IsNullOrWhiteSpace(p.Category) ? "utilities" : p.Category.Trim(),
                    p.Popular,
                    p.Summary?.Trim() ?? string.Empty,
                    source,
                    string.IsNullOrWhiteSpace(p.Bucket) ? null : p.Bucket.Trim(),
                    string.IsNullOrWhiteSpace(p.BucketUrl) ? null : p.BucketUrl.Trim(),
                    string.IsNullOrWhiteSpace(p.Homepage) ? null : p.Homepage.Trim(),
                    string.IsNullOrWhiteSpace(p.License) ? null : p.License.Trim(),
                    string.IsNullOrWhiteSpace(p.Version) ? null : p.Version.Trim()));
            }

            return new PicksCatalog(categories, picks);
        }
        catch
        {
            return new PicksCatalog(Array.Empty<CatalogCategory>(), Array.Empty<CatalogPick>());
        }
    }

    private sealed record PicksDto(List<CategoryDto>? Categories, List<PickDto>? Picks);

    private sealed record CategoryDto(string? Id, string? Title, string? Tag, string? Query);

    private sealed record PickDto(
        string? Id,
        string? Name,
        string? Publisher,
        string? Category,
        bool Popular,
        string? Summary,
        string? Source,
        string? Bucket,
        string? BucketUrl,
        string? Homepage,
        string? License,
        string? Version);
}
