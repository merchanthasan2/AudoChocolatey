using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Net;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services;

/// <summary>
/// Resolves original application icons for installed apps and Marketplace apps (section 5.6, 46.5.20).
/// Caches extracted/fetched icon files in %LOCALAPPDATA%\QuantumUpdater\icons\ (30 days valid, 7 days miss)
/// and memory-caches up to 500 resolved paths for instant UI display.
/// </summary>
public sealed class IconService : IDisposable
{
    private const int MaxMemoryEntries = 500;
    private static readonly TimeSpan HitTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan MissTtl = TimeSpan.FromDays(7);

    private static readonly string[] SkipExecutables = { "unins", "setup", "update", "crash", "helper" };

    private static readonly Regex LinkTagRegex = new(
        @"<link\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttrRegex = new(
        @"([a-zA-Z-]+)\s*=\s*[""']([^""']*)[""']",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string _cacheDir;
    private readonly SafeIconFetcher _fetcher = new();
    private readonly WingetReader _reader;
    private readonly object _gate = new();
    private readonly Dictionary<string, string?> _memory = new(StringComparer.OrdinalIgnoreCase);

    public IconService(WingetReader reader, string? cacheDir = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuantumUpdater",
            "icons");
    }

    /// <summary>Returns a cached local icon file path synchronously if already known.</summary>
    public string? TryGetCachedPath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        lock (_gate)
        {
            if (_memory.TryGetValue(key, out var path))
            {
                return path is not null && File.Exists(path) ? path : null;
            }
        }

        string hash = HashKey(key);
        foreach (var ext in new[] { ".png", ".ico", ".jpg" })
        {
            string candidate = Path.Combine(_cacheDir, hash + ext);
            if (File.Exists(candidate) && DateTime.UtcNow - File.GetLastWriteTimeUtc(candidate) < HitTtl)
            {
                Remember(key, candidate);
                return candidate;
            }
        }

        string miss = Path.Combine(_cacheDir, hash + ".miss");
        if (File.Exists(miss) && DateTime.UtcNow - File.GetLastWriteTimeUtc(miss) < MissTtl)
        {
            Remember(key, null);
        }

        return null;
    }

    /// <summary>Loads or extracts the original icon for an installed app.</summary>
    public async Task<string?> ResolveInstalledAsync(AppEntry app, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(app);
        var cached = TryGetCachedPath(app.Key);
        if (cached is not null)
        {
            return cached;
        }

        if (IsKnownMiss(app.Key))
        {
            return null;
        }

        string? path = await Task.Run(() => ExtractLocalIcon(app), token).ConfigureAwait(false);
        if (path is null && !string.IsNullOrEmpty(app.CatalogId))
        {
            path = await ResolveCatalogAsync(app.CatalogId, token).ConfigureAwait(false);
        }

        if (path is null)
        {
            MarkMiss(app.Key);
            return null;
        }

        Remember(app.Key, path);
        return path;
    }

    /// <summary>Loads or fetches the original icon for a Marketplace catalog app.</summary>
    public Task<string?> ResolveCatalogAsync(string catalogId, CancellationToken token)
        => ResolveCatalogAsync(catalogId, fallbackHomepage: null, token);

    /// <summary>Loads or fetches the original icon for a Marketplace catalog app, with optional maker homepage fallback.</summary>
    public async Task<string?> ResolveCatalogAsync(string catalogId, string? fallbackHomepage, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            return null;
        }

        var cacheKey = "catalog:" + catalogId;
        var cached = TryGetCachedPath(cacheKey);
        if (cached is not null)
        {
            return cached;
        }

        if (IsKnownMiss(cacheKey))
        {
            return null;
        }

        try
        {
            CatalogDetails? details = null;
            if (string.IsNullOrWhiteSpace(fallbackHomepage))
            {
                details = await _reader.DetailsAsync(catalogId, token).ConfigureAwait(false);
            }

            if (details is not null)
            {
                // 1. Winget manifest Icons
                foreach (var icon in details.Icons.OrderByDescending(i => string.Equals(i.FileType, "Png", StringComparison.OrdinalIgnoreCase)))
                {
                    var fetched = await _fetcher.FetchImageAsync(icon.Url, makerSiteUrl: null, icon.Sha256, token).ConfigureAwait(false);
                    if (fetched is not null)
                    {
                        var saved = SaveBytes(cacheKey, fetched.Data, fetched.Extension);
                        if (saved is not null)
                        {
                            Remember(cacheKey, saved);
                            return saved;
                        }
                    }
                }
            }

            // 2. Maker homepage
            string? homepage = details?.Homepage ?? fallbackHomepage;
            if (!string.IsNullOrWhiteSpace(homepage))
            {
                string? saved = await TryFetchFromHomepageAsync(cacheKey, homepage, token).ConfigureAwait(false);
                if (saved is not null)
                {
                    Remember(cacheKey, saved);
                    return saved;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Fall through to miss cache.
        }

        MarkMiss(cacheKey);
        return null;
    }

    /// <summary>Creates a WinUI <see cref="ImageSource"/> from a local cached icon file path on the UI thread.</summary>
    public static ImageSource? LoadImageSource(string? filePath, int decodeWidth = 48)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            return new BitmapImage(new Uri(filePath, UriKind.Absolute))
            {
                DecodePixelType = DecodePixelType.Logical,
                DecodePixelWidth = decodeWidth,
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryFetchFromHomepageAsync(string cacheKey, string homepage, CancellationToken token)
    {
        if (!Uri.TryCreate(homepage, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var html = await _fetcher.FetchHtmlAsync(homepage, token).ConfigureAwait(false);
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(html))
        {
            candidates.AddRange(ExtractIconLinks(html, baseUri));
        }

        candidates.Add(new Uri(baseUri, "/favicon.ico").AbsoluteUri);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(5))
        {
            var fetched = await _fetcher.FetchImageAsync(candidate, makerSiteUrl: homepage, expectedSha256Hex: null, token).ConfigureAwait(false);
            if (fetched is not null)
            {
                var saved = SaveBytes(cacheKey, fetched.Data, fetched.Extension);
                if (saved is not null)
                {
                    return saved;
                }
            }
        }

        return null;
    }

    internal static IReadOnlyList<string> ExtractIconLinks(string html, Uri baseUri)
    {
        var apple = new List<string>();
        var sized = new List<(int Size, string Url)>();
        var other = new List<string>();

        foreach (Match match in LinkTagRegex.Matches(html))
        {
            string? rel = null;
            string? href = null;
            string? sizes = null;

            foreach (Match attr in AttrRegex.Matches(match.Value))
            {
                string name = attr.Groups[1].Value;
                string val = attr.Groups[2].Value;
                if (string.Equals(name, "rel", StringComparison.OrdinalIgnoreCase)) rel = val;
                else if (string.Equals(name, "href", StringComparison.OrdinalIgnoreCase)) href = val;
                else if (string.Equals(name, "sizes", StringComparison.OrdinalIgnoreCase)) sizes = val;
            }

            if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, href.Trim(), out var resolved) || resolved.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            if (rel.Contains("apple-touch-icon", StringComparison.OrdinalIgnoreCase))
            {
                apple.Add(resolved.AbsoluteUri);
            }
            else if (rel.Contains("icon", StringComparison.OrdinalIgnoreCase))
            {
                int px = ParseMaxSize(sizes);
                if (px > 0) sized.Add((px, resolved.AbsoluteUri));
                else other.Add(resolved.AbsoluteUri);
            }
        }

        var ordered = new List<string>();
        ordered.AddRange(apple);
        ordered.AddRange(sized.OrderByDescending(s => s.Size).Select(s => s.Url));
        ordered.AddRange(other);
        return ordered;
    }

    private static int ParseMaxSize(string? sizes)
    {
        if (string.IsNullOrWhiteSpace(sizes)) return 0;
        var m = Regex.Match(sizes, @"(\d+)x(\d+)");
        return m.Success && int.TryParse(m.Groups[1].Value, out int n) ? n : 0;
    }

    private string? ExtractLocalIcon(AppEntry app)
    {
        // 1. Registry DisplayIcon
        if (!string.IsNullOrWhiteSpace(app.DisplayIcon))
        {
            var (path, index) = ParseDisplayIcon(app.DisplayIcon);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    var copied = SaveFileCopy(app.Key, path, ".ico");
                    if (copied is not null) return copied;
                }

                var extracted = ExtractExeIconPng(app.Key, path, index);
                if (extracted is not null) return extracted;
            }
        }

        // 2. MSIX / Store AppxManifest logo
        if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
        {
            string manifest = Path.Combine(app.InstallLocation, "AppxManifest.xml");
            if (File.Exists(manifest))
            {
                string? logoPath = FindMsixLogo(app.InstallLocation, manifest);
                if (logoPath is not null)
                {
                    var saved = SaveFileCopy(app.Key, logoPath, Path.GetExtension(logoPath));
                    if (saved is not null) return saved;
                }
            }

            // 3. Main executable in InstallLocation
            string? mainExe = FindMainExecutable(app.InstallLocation);
            if (mainExe is not null)
            {
                var extracted = ExtractExeIconPng(app.Key, mainExe, 0);
                if (extracted is not null) return extracted;
            }
        }

        return null;
    }

    private static (string Path, int Index) ParseDisplayIcon(string raw)
    {
        var expanded = Environment.ExpandEnvironmentVariables(raw.Trim());
        int index = 0;
        int comma = expanded.LastIndexOf(',');
        if (comma > 0 && int.TryParse(expanded[(comma + 1)..].Trim(), out int parsed))
        {
            index = parsed;
            expanded = expanded[..comma];
        }

        return (expanded.Trim('"', ' '), index);
    }

    private static string? FindMsixLogo(string installDir, string manifestPath)
    {
        try
        {
            var doc = XDocument.Load(manifestPath);
            string? rawLogo = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "VisualElements")
                ?.Attribute("Square44x44Logo")?.Value;
            rawLogo ??= doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Logo")
                ?.Value;

            if (string.IsNullOrWhiteSpace(rawLogo))
            {
                return null;
            }

            string rel = rawLogo.Trim().Replace('/', '\\');
            string direct = Path.Combine(installDir, rel);
            if (File.Exists(direct))
            {
                return direct;
            }

            string dir = Path.GetDirectoryName(direct) ?? installDir;
            string stem = Path.GetFileNameWithoutExtension(direct);
            string ext = Path.GetExtension(direct);
            if (!Directory.Exists(dir))
            {
                return null;
            }

            var matches = Directory.EnumerateFiles(dir, $"{stem}*{ext}").ToList();
            return matches.FirstOrDefault(f => f.Contains("targetsize-48_altform-unplated", StringComparison.OrdinalIgnoreCase))
                ?? matches.FirstOrDefault(f => f.Contains("targetsize-48", StringComparison.OrdinalIgnoreCase))
                ?? matches.FirstOrDefault(f => f.Contains("scale-200", StringComparison.OrdinalIgnoreCase))
                ?? matches.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? FindMainExecutable(string installDir)
    {
        try
        {
            foreach (var exe in Directory.EnumerateFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileNameWithoutExtension(exe);
                if (SkipExecutables.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return exe;
            }
        }
        catch
        {
            // Ignore inaccessible folders.
        }

        return null;
    }

    private string? ExtractExeIconPng(string key, string exePath, int iconIndex)
    {
        IntPtr[] large = new IntPtr[1];
        try
        {
            uint count = ExtractIconEx(exePath, iconIndex, large, null, 1);
            if (count == 0 || large[0] == IntPtr.Zero)
            {
                return null;
            }

            byte[]? pngBytes = ConvertHIconToPng(large[0]);
            return pngBytes is null ? null : SaveBytes(key, pngBytes, ".png");
        }
        catch
        {
            return null;
        }
        finally
        {
            if (large[0] != IntPtr.Zero)
            {
                DestroyIcon(large[0]);
            }
        }
    }

    private static byte[]? ConvertHIconToPng(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out var iconInfo))
        {
            return null;
        }

        try
        {
            IntPtr hBmp = iconInfo.hbmColor != IntPtr.Zero ? iconInfo.hbmColor : iconInfo.hbmMask;
            if (hBmp == IntPtr.Zero)
            {
                return null;
            }

            if (GetObject(hBmp, Marshal.SizeOf<BITMAP>(), out var bmp) == 0 || bmp.bmWidth <= 0 || bmp.bmHeight <= 0)
            {
                return null;
            }

            int width = bmp.bmWidth;
            int height = iconInfo.hbmColor != IntPtr.Zero ? bmp.bmHeight : bmp.bmHeight / 2;
            if (width <= 0 || height <= 0 || width > 512 || height > 512)
            {
                return null;
            }

            var pixels = new byte[width * height * 4];
            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // top-down BGRA
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0, // BI_RGB
                },
            };

            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                int lines = GetDIBits(hdc, hBmp, 0, (uint)height, pixels, ref bmi, 0);
                if (lines == 0)
                {
                    return null;
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }

            using var stream = new InMemoryRandomAccessStream();
            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream).AsTask().GetAwaiter().GetResult();
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)width,
                (uint)height,
                96,
                96,
                pixels);
            encoder.FlushAsync().AsTask().GetAwaiter().GetResult();

            stream.Seek(0);
            var outBytes = new byte[stream.Size];
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            reader.LoadAsync((uint)outBytes.Length).AsTask().GetAwaiter().GetResult();
            reader.ReadBytes(outBytes);
            return outBytes;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
        }
    }

    private string? SaveFileCopy(string key, string sourcePath, string ext)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(sourcePath);
            if (bytes.Length == 0 || bytes.Length > SafeIconFetcher.MaxImageBytes)
            {
                return null;
            }

            return SaveBytes(key, bytes, string.IsNullOrEmpty(ext) ? ".png" : ext);
        }
        catch
        {
            return null;
        }
    }

    private string? SaveBytes(string key, byte[] bytes, string ext)
    {
        try
        {
            Directory.CreateDirectory(_cacheDir);
            string target = Path.Combine(_cacheDir, HashKey(key) + ext.ToLowerInvariant());
            string tmp = target + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, target, overwrite: true);
            return target;
        }
        catch
        {
            return null;
        }
    }

    private bool IsKnownMiss(string key)
    {
        lock (_gate)
        {
            if (_memory.TryGetValue(key, out var val))
            {
                return val is null;
            }
        }

        string miss = Path.Combine(_cacheDir, HashKey(key) + ".miss");
        return File.Exists(miss) && DateTime.UtcNow - File.GetLastWriteTimeUtc(miss) < MissTtl;
    }

    private void MarkMiss(string key)
    {
        Remember(key, null);
        try
        {
            Directory.CreateDirectory(_cacheDir);
            File.WriteAllText(Path.Combine(_cacheDir, HashKey(key) + ".miss"), "miss");
        }
        catch
        {
            // Ignore cache write errors.
        }
    }

    private void Remember(string key, string? path)
    {
        lock (_gate)
        {
            if (_memory.Count >= MaxMemoryEntries && !_memory.ContainsKey(key))
            {
                var oldest = _memory.Keys.FirstOrDefault();
                if (oldest is not null)
                {
                    _memory.Remove(oldest);
                }
            }

            _memory[key] = path;
        }
    }

    private static string HashKey(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant())))[..24];

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    public void Dispose()
    {
        _fetcher.Dispose();
    }
}
