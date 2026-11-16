using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace QuantumUpdater;

/// <summary>DeskVerve design tokens. Values from docs/plans/2026-11-16-phase46-apps-first.md section 11.</summary>
public static class Theme
{
    // Existing brushes
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

    // New brushes
    /// <summary>Row and card hover background.</summary>
    public static SolidColorBrush CardHover { get; } = Brush(0x33, 0x33, 0x33);

    /// <summary>List header and toolbar background.</summary>
    public static SolidColorBrush Subtle { get; } = Brush(0x1C, 0x1C, 0x1C);

    /// <summary>Chip background.</summary>
    public static SolidColorBrush Chip { get; } = Brush(0x38, 0x38, 0x38);

    /// <summary>Publisher names and captions.</summary>
    public static SolidColorBrush TextSecondary { get; } = Brush(0x9E, 0x9E, 0x9E);

    /// <summary>Accent button hover background.</summary>
    public static SolidColorBrush AccentHover { get; } = Brush(0x1A, 0x86, 0xD9);

    /// <summary>Accent button pressed background.</summary>
    public static SolidColorBrush AccentPressed { get; } = Brush(0x00, 0x6C, 0xBE);

    /// <summary>Links and new version text on dark backgrounds.</summary>
    public static SolidColorBrush AccentText { get; } = Brush(0x60, 0xB0, 0xF0);

    /// <summary>Error text and Remove button text on dark backgrounds.</summary>
    public static SolidColorBrush CriticalText { get; } = Brush(0xFF, 0x99, 0xA4);

    /// <summary>"Done" chip text and "Updated to" text.</summary>
    public static SolidColorBrush HealthyText { get; } = Brush(0x6F, 0xD3, 0x9A);

    /// <summary>"Waiting" and "Stopping" chip text.</summary>
    public static SolidColorBrush WarningText { get; } = Brush(0xF0, 0xC6, 0x4A);

    // Chip brushes
    /// <summary>Accent chip background (22 percent alpha).</summary>
    public static SolidColorBrush AccentChip { get; } = Brush(0x38, 0x00, 0x78, 0xD4);

    /// <summary>Success chip background (18 percent alpha).</summary>
    public static SolidColorBrush OkChip { get; } = Brush(0x2E, 0x27, 0xAC, 0x62);

    /// <summary>Warning chip background (18 percent alpha).</summary>
    public static SolidColorBrush WarnChip { get; } = Brush(0x2E, 0xD8, 0xA0, 0x00);

    /// <summary>Error chip background (18 percent alpha).</summary>
    public static SolidColorBrush BadChip { get; } = Brush(0x2E, 0xE8, 0x11, 0x23);

    // Spacing
    public const double Gap4 = 4;
    public const double Gap8 = 8;
    public const double Gap12 = 12;
    public const double Gap16 = 16;
    public const double Gap24 = 24;
    public const double Gap32 = 32;

    // Corner radius
    /// <summary>Buttons and chips.</summary>
    public static Microsoft.UI.Xaml.CornerRadius RadiusSmall { get; } = new(4);

    /// <summary>Cards and dialogs.</summary>
    public static Microsoft.UI.Xaml.CornerRadius RadiusLarge { get; } = new(8);

    // Type sizes
    public const double TitleSize = 28;
    public const double SectionSize = 20;
    public const double BodySize = 14;
    public const double CaptionSize = 12;

    // Helpers
    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Windows.UI.Color.FromArgb(255, r, g, b));

    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b) =>
        new(Windows.UI.Color.FromArgb(a, r, g, b));
}
