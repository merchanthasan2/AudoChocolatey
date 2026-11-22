using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace QuantumUpdater.Views.Controls;

/// <summary>One app in the confirm list. Version null means not known; then no version shows.</summary>
public sealed record ConfirmApp(string Name, string? Version);

/// <summary>
/// The texts of one confirm dialog. The caller sets them. Mockup examples:
/// "Update all" (01-updates.html #confirm-all): Title "Update 7 apps?", Apps = 7 apps with versions,
/// MainButton "Update all", Danger false, Note "Some apps close while they update. Save your work in these apps first."
/// "Remove" (02-installed.html #confirm): TileName "C", Title "Remove Canon IJ Scan Utility?",
/// Body "DeskVerve runs Canon's own uninstaller. A Canon window can open. Follow its steps to finish.",
/// Note "Your documents, photos and other files stay on this PC.", MainButton "Remove", Danger true.
/// TileName, when set, shows a letter tile above the title. The title then sits in the content, not in the dialog title area.
/// </summary>
public sealed record ConfirmRequest(
    string Title,
    string Body,
    IReadOnlyList<ConfirmApp>? Apps,
    string? Note,
    string MainButton,
    bool Danger,
    string? TileName = null);

/// <summary>
/// Code-only confirm dialog, 460 px wide. Esc and Cancel return false. The main button returns true.
/// Only one dialog can be open at a time: a second call returns false at once,
/// because WinUI fails when two dialogs open.
/// The app list shows the first 4 apps. More apps give one line "And N more: ..." with names only.
/// </summary>
public static class ConfirmDialog
{
    private const int MaxApps = 4;
    private const double DialogWidth = 460;

    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");
    private static readonly SolidColorBrush ListFill = new(Windows.UI.Color.FromArgb(0xFF, 0x23, 0x23, 0x23));

    private static bool _open;

    /// <summary>Shows the dialog. Returns true only when the main button is pressed.</summary>
    public static async Task<bool> ShowAsync(XamlRoot root, ConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(request);

        if (_open)
        {
            return false;
        }

        _open = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = root,
                RequestedTheme = ElementTheme.Dark,
                Content = BuildContent(request),
                PrimaryButtonText = request.MainButton,
                CloseButtonText = "Cancel",
                DefaultButton = request.Danger ? ContentDialogButton.Close : ContentDialogButton.Primary,
            };

            // Width: the dialog template reads these two resources, so the dialog is 460 px wide.
            dialog.Resources["ContentDialogMinWidth"] = DialogWidth;
            dialog.Resources["ContentDialogMaxWidth"] = DialogWidth;

            if (request.TileName is null)
            {
                dialog.Title = request.Title;
            }

            if (request.Danger)
            {
                dialog.PrimaryButtonStyle = DangerStyle();
            }

            ContentDialogResult result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        finally
        {
            _open = false;
        }
    }

    private static UIElement BuildContent(ConfirmRequest request)
    {
        var panel = new StackPanel { Spacing = Theme.Gap12 };

        if (!string.IsNullOrWhiteSpace(request.TileName))
        {
            panel.Children.Add(Chrome.LetterTile(request.TileName, 48));
            panel.Children.Add(new TextBlock
            {
                Text = request.Title,
                FontFamily = Chrome.Font,
                FontSize = Theme.SectionSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            panel.Children.Add(Chrome.Line(request.Body));
        }

        var apps = (request.Apps ?? Array.Empty<ConfirmApp>()).ToList();
        if (apps.Count > 0)
        {
            var list = new StackPanel { Spacing = 0 };
            foreach (var app in apps.Take(MaxApps))
            {
                list.Children.Add(AppRow(app));
            }

            if (apps.Count > MaxApps)
            {
                var rest = apps.Skip(MaxApps).Select(a => a.Name).ToList();
                var more = Chrome.Caption($"And {rest.Count} more: {string.Join(", ", rest)}");
                more.TextWrapping = TextWrapping.Wrap;
                more.MaxLines = 2;
                more.TextTrimming = TextTrimming.CharacterEllipsis;
                list.Children.Add(more);
            }

            panel.Children.Add(new Border
            {
                Background = ListFill,
                BorderBrush = Theme.Stroke,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Child = list,
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            panel.Children.Add(NoteLine(request.Note));
        }

        return panel;
    }

    /// <summary>One app row: name on the left, version on the right. Mockup: .dlg li, .ver.</summary>
    private static Grid AppRow(ConfirmApp app)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock
        {
            Text = app.Name,
            FontFamily = Chrome.Font,
            FontSize = 13,
            Foreground = Theme.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(name);
        Grid.SetColumn(name, 0);

        if (!string.IsNullOrWhiteSpace(app.Version))
        {
            var version = new TextBlock
            {
                Text = app.Version,
                FontFamily = MonoFont,
                FontSize = Theme.CaptionSize,
                Foreground = Theme.Muted,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            grid.Children.Add(version);
            Grid.SetColumn(version, 1);
        }

        return grid;
    }

    /// <summary>Note with an info icon before the text. Mockup: .dlg .route.</summary>
    private static Grid NoteLine(string note)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            Glyph = "",
            FontFamily = IconFont,
            FontSize = 14,
            Foreground = Theme.AccentText,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 10, 0),
        };
        grid.Children.Add(icon);
        Grid.SetColumn(icon, 0);

        var text = new TextBlock
        {
            Text = note,
            FontFamily = Chrome.Font,
            FontSize = 13,
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
        };
        grid.Children.Add(text);
        Grid.SetColumn(text, 1);

        return grid;
    }

    /// <summary>Red main button for Remove. Same colours as Chrome.DangerButton.</summary>
    private static Style DangerStyle()
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Theme.Critical));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Text));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        return style;
    }
}
