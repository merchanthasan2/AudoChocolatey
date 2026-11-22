using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace QuantumUpdater;

/// <summary>
/// Brand image loader. This build is unpackaged (WindowsPackageType=None with
/// PRI tooling disabled), so ms-appx:/// URIs have no resource index to resolve
/// against and crashed startup with 0xC000027B. The emblem is therefore loaded
/// from the file deployed next to the executable.
/// </summary>
public static class BrandAssets
{
    // F25: the square 256 x 256 emblem. The 449 x 301 master is not square, so a square box
    // letterboxed it, and decoding it at full size wasted memory for a 24 px title bar icon.
    private static readonly string EmblemPath = Path.Combine(
        AppContext.BaseDirectory, "Assets", "Brand", "desk-verve-emblem-256.png");

    /// <summary>
    /// Creates the DeskVerve emblem image at the given size. Never throws:
    /// when the file is missing or cannot be decoded the image stays
    /// transparent (no source) instead of crashing startup.
    /// </summary>
    public static Image Emblem(double size)
    {
        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
        };

        if (!File.Exists(EmblemPath))
        {
            return image;
        }

        try
        {
            // Logical pixels: WinUI scales the decode size by the display scale, so the icon stays sharp.
            image.Source = new BitmapImage(new Uri(EmblemPath, UriKind.Absolute))
            {
                DecodePixelType = DecodePixelType.Logical,
                DecodePixelWidth = DecodeWidth(size),
            };
        }
        catch
        {
            image.Source = null;
        }

        return image;
    }

    // Whole logical pixels, at least 1 and no more than the file holds.
    internal static int DecodeWidth(double size)
    {
        if (double.IsNaN(size) || size < 1)
        {
            return 1;
        }

        return (int)Math.Min(256, Math.Ceiling(size));
    }
}
