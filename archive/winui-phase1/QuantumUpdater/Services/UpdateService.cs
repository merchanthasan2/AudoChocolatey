using System.Net.Http.Headers;
using System.Text.Json;
using QuantumUpdater.Models;

namespace QuantumUpdater.Services;

/// <summary>Result of a GitHub release check (Phase 44.3 — check only).</summary>
public sealed record UpdateCheckResult(
    bool HasUpdate,
    string? LatestTag,
    string? ZipUrl,
    string Message);

/// <summary>Self-update check against GitHub Releases (apply in Phase 44.4).</summary>
public sealed class UpdateService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/merchanthasan2/AudoChocolatey/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("QuantumUpdater"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        return client;
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await HttpClient
                .GetAsync(LatestReleaseUrl, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    false,
                    null,
                    null,
                    $"Update check failed (HTTP {(int)response.StatusCode}).");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var root = doc.RootElement;
            var tagName = root.TryGetProperty("tag_name", out var tagEl)
                ? tagEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(tagName))
            {
                return new UpdateCheckResult(false, null, null, "Release response missing tag_name.");
            }

            string? zipUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameEl))
                    {
                        continue;
                    }

                    var name = nameEl.GetString();
                    if (name is not null &&
                        name.Contains("QuantumUpdater-Windows.zip", StringComparison.OrdinalIgnoreCase) &&
                        asset.TryGetProperty("browser_download_url", out var urlEl))
                    {
                        zipUrl = urlEl.GetString();
                        break;
                    }
                }
            }

            var latestVersion = ParseTagVersion(tagName);
            var current = AppVersion.Version;
            var hasUpdate = latestVersion > current;

            var message = hasUpdate
                ? zipUrl is not null
                    ? $"Update available: {tagName}. Download apply is planned for Phase 44.4."
                    : $"Update available: {tagName}, but QuantumUpdater-Windows.zip was not found on the release."
                : $"You are on the latest release ({AppVersion.Label}).";

            return new UpdateCheckResult(hasUpdate, tagName, zipUrl, message);
        }
        catch (OperationCanceledException)
        {
            return new UpdateCheckResult(false, null, null, "Cancelled.");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, null, null, ex.Message);
        }
    }

    /// <summary>Download and apply update — not implemented until Phase 44.4.</summary>
    public Task<string> ApplyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("Phase 44.4");
    }

    private static Version ParseTagVersion(string tag)
    {
        var cleaned = tag.Trim().TrimStart('v', 'V');
        if (Version.TryParse(cleaned, out var parsed))
        {
            return parsed;
        }

        var parts = new List<int>();
        foreach (var segment in cleaned.Split('.'))
        {
            var digits = "";
            foreach (var ch in segment)
            {
                if (char.IsDigit(ch))
                {
                    digits += ch;
                }
                else
                {
                    break;
                }
            }

            parts.Add(digits.Length > 0 ? int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) : 0);
        }

        while (parts.Count < 3)
        {
            parts.Add(0);
        }

        return new Version(parts[0], parts[1], parts[2]);
    }
}