using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace QuantumUpdater;

/// <summary>Colors from the Stitch project Modern Windows System Toolkit.</summary>
public static class Theme
{
    public static SolidColorBrush Canvas { get; } = Brush(0x13, 0x13, 0x13);
    public static SolidColorBrush Sidebar { get; } = Brush(0x27, 0x27, 0x27);
    public static SolidColorBrush Card { get; } = Brush(0x2C, 0x2C, 0x2C);
    public static SolidColorBrush Stroke { get; } = Brush(0x38, 0x38, 0x38);
    public static SolidColorBrush Accent { get; } = Brush(0x00, 0x78, 0xD4);
    public static SolidColorBrush Text { get; } = new(Colors.White);
    public static SolidColorBrush Muted { get; } = Brush(0xD1, 0xD1, 0xD1);
    public static SolidColorBrush Healthy { get; } = Brush(0x27, 0xAC, 0x62);
    public static SolidColorBrush Warning { get; } = Brush(0xD8, 0xA0, 0x00);
    public static SolidColorBrush Critical { get; } = Brush(0xE8, 0x11, 0x23);

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Windows.UI.Color.FromArgb(255, r, g, b));
}
