using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace QuantumUpdater;

static class Chrome
{
    public static readonly FontFamily Font = new("Segoe UI Variable");

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
}
