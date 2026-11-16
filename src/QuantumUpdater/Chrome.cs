using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Views.Controls;

namespace QuantumUpdater;

/// <summary>Colour kinds for <see cref="Chrome.Chip"/>.</summary>
public enum ChipKind { Neutral, Accent, Ok, Warn, Bad }

static class Chrome
{
    public static readonly FontFamily Font = new("Segoe UI Variable");

    /// <summary>Icon font with a fallback for older Windows versions.</summary>
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static TextBlock Title(string text) => new()
    {
        Text = text,
        Foreground = Theme.Text,
        FontFamily = Font,
        FontSize = 28,
        FontWeight = FontWeights.SemiBold,
    };

    public static TextBlock Line(string text, SolidColorBrush? color = null) => new()
    {
        Text = text,
        Foreground = color ?? Theme.Muted,
        FontFamily = Font,
        FontSize = 14,
        TextWrapping = TextWrapping.Wrap,
    };

    public static Button Action(string label)
    {
        return new Button
        {
            Content = label,
            Background = Theme.Accent,
            Foreground = Theme.Text,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14, 8, 14, 8),
            FontFamily = Font,
        };
    }

    public static Border Card(UIElement child) => new()
    {
        Background = Theme.Card,
        BorderBrush = Theme.Stroke,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16),
        Margin = new Thickness(0, 0, 0, 8),
        Child = child,
    };

    public static CheckBox Confirm(string label) => new()
    {
        Content = label,
        IsChecked = false,
        Foreground = Theme.Text,
        FontFamily = Font,
    };

    /// <summary>Secondary button: card fill, 1 px stroke border, white text. Mockup: 01-updates.html .btn2.</summary>
    public static Button SecondaryButton(string text) => new()
    {
        Content = text,
        Background = Theme.Card,
        Foreground = Theme.Text,
        BorderBrush = Theme.Stroke,
        BorderThickness = new Thickness(1),
        CornerRadius = Theme.RadiusSmall,
        Padding = new Thickness(14, 6, 14, 6),
        FontFamily = Font,
    };

    /// <summary>Ghost button: no fill, no border, accent text. Mockup: 01-updates.html .ghost.</summary>
    public static Button GhostButton(string text) => new()
    {
        Content = text,
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        Foreground = Theme.AccentText,
        BorderThickness = new Thickness(0),
        CornerRadius = Theme.RadiusSmall,
        Padding = new Thickness(8, 6, 8, 6),
        FontFamily = Font,
    };

    /// <summary>Danger button: red fill, white text. Mockup has no red button; padding and weight follow .btn.</summary>
    public static Button DangerButton(string text) => new()
    {
        Content = text,
        Background = Theme.Critical,
        Foreground = Theme.Text,
        BorderThickness = new Thickness(0),
        CornerRadius = Theme.RadiusSmall,
        Padding = new Thickness(14, 7, 14, 7),
        FontFamily = Font,
        FontWeight = FontWeights.SemiBold,
    };

    /// <summary>Small status label with a colour kind. Mockup: 01-updates.html .chip.</summary>
    public static Border Chip(string text, ChipKind kind)
    {
        var (background, foreground) = kind switch
        {
            ChipKind.Accent => (Theme.AccentChip, Theme.AccentText),
            ChipKind.Ok => (Theme.OkChip, Theme.HealthyText),
            ChipKind.Warn => (Theme.WarnChip, Theme.WarningText),
            ChipKind.Bad => (Theme.BadChip, Theme.CriticalText),
            _ => (Theme.Chip, Theme.Muted), // Mockup .chip uses --muted (#D1D1D1).
        };

        return new Border
        {
            Background = background,
            CornerRadius = Theme.RadiusSmall,
            Padding = new Thickness(8, 1, 8, 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontFamily = Font,
                FontSize = Theme.CaptionSize,
            },
        };
    }

    /// <summary>Small secondary text. Mockup: 01-updates.html .cap.</summary>
    public static TextBlock Caption(string text) => new()
    {
        Text = text,
        Foreground = Theme.TextSecondary,
        FontFamily = Font,
        FontSize = Theme.CaptionSize,
    };

    /// <summary>Group header with a chevron (down when expanded, right when collapsed), bold text and a count chip. Mockup: 01-updates.html .group.</summary>
    public static StackPanel SectionHeader(string text, int count, bool expanded)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Theme.Gap8,
        };

        panel.Children.Add(new FontIcon
        {
            Glyph = expanded ? "\uE70D" : "\uE76C",
            FontFamily = IconFont,
            FontSize = 12,
            Foreground = Theme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
        });

        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            FontFamily = Font,
            FontSize = Theme.BodySize,
            Foreground = Theme.Text,
            VerticalAlignment = VerticalAlignment.Center,
        });

        panel.Children.Add(Chip(count.ToString(), ChipKind.Neutral));
        return panel;
    }

    /// <summary>Search box, 240 px wide, with a search icon. Mockup: 01-updates.html .search.</summary>
    public static AutoSuggestBox SearchBox(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        Width = 240,
        Background = Theme.Card,
        BorderBrush = Theme.Stroke,
        BorderThickness = new Thickness(1),
        CornerRadius = Theme.RadiusSmall,
        FontFamily = Font,
        QueryIcon = new SymbolIcon(Symbol.Find),
    };

    /// <summary>App letter tile: one letter on a colour from the palette. Size 32 (radius 6) or 48 (radius 8). Mockup: 01-updates.html .ic.</summary>
    public static Border LetterTile(string name, double size)
    {
        var isLarge = size >= 48;
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(isLarge ? 8 : 6),
            Background = BrushFromHex(LetterTileRules.PaletteHex[LetterTileRules.ColourIndex(name)]),
            Child = new TextBlock
            {
                Text = LetterTileRules.Letter(name).ToString(),
                Foreground = Theme.Text,
                FontFamily = Font,
                FontWeight = FontWeights.Bold,
                FontSize = isLarge ? Theme.SectionSize : 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    /// <summary>Converts a #RRGGBB text to a solid brush. Used for the letter tile palette only.</summary>
    private static SolidColorBrush BrushFromHex(string hex)
    {
        var r = Convert.ToByte(hex.Substring(1, 2), 16);
        var g = Convert.ToByte(hex.Substring(3, 2), 16);
        var b = Convert.ToByte(hex.Substring(5, 2), 16);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }
}
