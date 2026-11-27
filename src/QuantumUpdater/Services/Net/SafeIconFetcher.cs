using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace QuantumUpdater.Services.Net;

public sealed record FetchedBytes(byte[] Data, string ContentType, string Extension);

/// <summary>
/// Safe HTTPS-only downloader for Marketplace icons and maker homepages (section 5.6, 46.6.12, W24).
/// Enforces: HTTPS only, max 3 HTTPS redirects, no cookies, 5 s timeout, size cap,
/// optional maker-domain lock, PNG/ICO/JPEG magic-byte validation (no SVG), and optional SHA-256 check.
/// </summary>
public sealed class SafeIconFetcher : IDisposable
{
    public const int MaxImageBytes = 256 * 1024;
    public const int MaxPageBytes = 512 * 1024;
    private const int MaxRedirects = 3;

    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public SafeIconFetcher()
        : this(CreateDefaultClient(), ownsClient: true)
    {
    }

    public SafeIconFetcher(HttpMessageHandler handler)
        : this(CreateClient(handler), ownsClient: true)
    {
    }

    private SafeIconFetcher(HttpClient client, bool ownsClient)
    {
        _client = client;
        _ownsClient = ownsClient;
    }

    public async Task<FetchedBytes?> FetchImageAsync(
        string url,
        string? makerSiteUrl = null,
        string? expectedSha256Hex = null,
        CancellationToken cancellationToken = default)
    {
        var raw = await FetchInternalAsync(url, makerSiteUrl, MaxImageBytes, cancellationToken).ConfigureAwait(false);
        if (raw is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256Hex))
        {
            string actual = Convert.ToHexString(SHA256.HashData(raw.Data));
            if (!string.Equals(actual, expectedSha256Hex.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        string? ext = DetectImageExtension(raw.Data);
        if (ext is null)
        {
            return null;
        }

        return new FetchedBytes(raw.Data, raw.ContentType, ext);
    }

    public async Task<string?> FetchHtmlAsync(string url, CancellationToken cancellationToken = default)
    {
        var raw = await FetchInternalAsync(url, makerSiteUrl: url, MaxPageBytes, cancellationToken).ConfigureAwait(false);
        if (raw is null)
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(raw.Data);
    }

    /// <summary>True when <paramref name="candidate"/> shares the host or registered domain of <paramref name="makerUrl"/>.</summary>
    public static bool IsSameMakerDomain(Uri candidate, Uri makerUrl)
    {
        string cHost = candidate.Host.ToLowerInvariant();
        string mHost = makerUrl.Host.ToLowerInvariant();
        if (cHost == mHost)
        {
            return true;
        }

        string cReg = RegisteredDomain(cHost);
        string mReg = RegisteredDomain(mHost);
        return !string.IsNullOrEmpty(cReg) && cReg == mReg;
    }

    /// <summary>Validates PNG, ICO, or JPEG binary headers. Refuses SVG or unknown formats.</summary>
    public static string? DetectImageExtension(byte[] data)
    {
        if (data is null || data.Length < 4)
        {
            return null;
        }

        // PNG: 89 50 4E 47
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return ".png";
        }

        // ICO: 00 00 01 00
        if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00)
        {
            return ".ico";
        }

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ".jpg";
        }

        return null;
    }

    private async Task<FetchedBytes?> FetchInternalAsync(
        string url,
        string? makerSiteUrl,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? current) || !IsAllowedHttps(current))
        {
            return null;
        }

        Uri? makerUri = null;
        if (!string.IsNullOrWhiteSpace(makerSiteUrl))
        {
            if (!Uri.TryCreate(makerSiteUrl, UriKind.Absolute, out makerUri) || !IsAllowedHttps(makerUri))
            {
                return null;
            }
        }

        for (int hop = 0; hop <= MaxRedirects; hop++)
        {
            if (makerUri is not null && !IsSameMakerDomain(current, makerUri))
            {
                return null;
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, current);
            using var res = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (IsRedirect(res.StatusCode) && res.Headers.Location is not null)
            {
                if (hop == MaxRedirects)
                {
                    return null;
                }

                Uri next = res.Headers.Location.IsAbsoluteUri
                    ? res.Headers.Location
                    : new Uri(current, res.Headers.Location);
                if (!IsAllowedHttps(next))
                {
                    return null;
                }

                current = next;
                continue;
            }

            if (!res.IsSuccessStatusCode)
            {
                return null;
            }

            if (res.Content.Headers.ContentLength is long len && len > maxBytes)
            {
                return null;
            }

            string contentType = res.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await using var stream = await res.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            int total = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > maxBytes)
                {
                    return null;
                }

                ms.Write(buffer, 0, read);
            }

            return new FetchedBytes(ms.ToArray(), contentType, string.Empty);
        }

        return null;
    }

    private static bool IsAllowedHttps(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && !uri.IsLoopback && !IPAddress.TryParse(uri.Host, out _);

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static string RegisteredDomain(string host)
    {
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return host;
        }

        return parts[^2] + "." + parts[^1];
    }

    private static HttpClient CreateDefaultClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        };
        return CreateClient(handler);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DeskVerve", "3.0"));
        return client;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
