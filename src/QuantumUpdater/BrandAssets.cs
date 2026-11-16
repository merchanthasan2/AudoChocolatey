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
    private static readonly string EmblemPath = Path.Combine(
        AppContext.BaseDirectory, "Assets", "Brand", "desk-verve-emblem.png");

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
            image.Source = new BitmapImage(new Uri(EmblemPath, UriKind.Absolute));
        }
        catch
        {
            image.Source = null;
        }

        return image;
    }
}