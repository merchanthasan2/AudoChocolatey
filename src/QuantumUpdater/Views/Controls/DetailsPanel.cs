using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Catalog;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Views.Controls;

/// <summary>
/// Marketplace app details panel matching mockup 04-marketplace-search.html .panel (46.6.9).
/// Loads full manifest details lazily (cached 7 days) when an app is selected.
/// </summary>
public sealed class DetailsPanel : UserControl
{
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    private readonly Border _frame;
    private readonly AppIcon _icon = new(64);
    private readonly TextBlock _name;
    private readonly TextBlock _publisher;
    private readonly StackPanel _chips;
    private readonly Button _action;
    private readonly TextBlock _note;
    private readonly TextBlock _description;
    private readonly TextBlock _factsPublisher;
    private readonly HyperlinkButton _websiteLink;
    private readonly TextBlock _factsLicense;
    private readonly TextBlock _factsSource;
    private readonly TextBlock _packageIdText;

    private MarketplaceItem? _current;

    public event Action<MarketplaceItem>? ActionClicked;

    public DetailsPanel()
    {
        _frame = new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Padding = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Top,
        };

        var stack = new StackPanel { Spacing = 14 };

        // Top: 64px icon + name + publisher
        var top = new Grid { ColumnSpacing = 14 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_icon, 0);
        top.Children.Add(_icon);

        _name = new TextBlock
        {
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            TextWrapping = TextWrapping.Wrap,
        };
        _publisher = new TextBlock
        {
            FontFamily = Chrome.Font,
            FontSize = 13,
            Foreground = Theme.AccentText,
            TextWrapping = TextWrapping.Wrap,
        };
        var titleBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        titleBox.Children.Add(_name);
        titleBox.Children.Add(_publisher);
        Grid.SetColumn(titleBox, 1);
        top.Children.Add(titleBox);
        stack.Children.Add(top);

        _chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        stack.Children.Add(_chips);

        _action = Chrome.Action("Install");
        _action.HorizontalAlignment = HorizontalAlignment.Stretch;
        _action.Padding = new Thickness(16, 10, 16, 10);
        _action.FontWeight = FontWeights.SemiBold;
        _action.Click += (_, _) =>
        {
            if (_current is not null)
            {
                ActionClicked?.Invoke(_current);
            }
        };
        stack.Children.Add(_action);

        _note = Chrome.Caption("Installs silently from the Microsoft catalog.");
        _note.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(_note);

        _description = new TextBlock
        {
            FontFamily = Chrome.Font,
            FontSize = 13,
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
        };
        stack.Children.Add(_description);

        // Facts table
        var facts = new Grid { RowSpacing = 6, ColumnSpacing = 12 };
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < 4; r++)
        {
            facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        _factsPublisher = ValueText();
        _websiteLink = new HyperlinkButton
        {
            Padding = new Thickness(0),
            Foreground = Theme.AccentText,
            FontFamily = Chrome.Font,
            FontSize = 13,
            Visibility = Visibility.Collapsed,
        };
        _factsLicense = ValueText();
        _factsSource = ValueText();

        AddFact(facts, 0, "Publisher", _factsPublisher);
        AddFact(facts, 1, "Website", _websiteLink);
        AddFact(facts, 2, "License", _factsLicense);
        AddFact(facts, 3, "Source", _factsSource);
        stack.Children.Add(facts);

        // Advanced expander
        var advDivider = new Border { Height = 1, Background = Theme.Stroke, Margin = new Thickness(0, 4, 0, 0) };
        stack.Children.Add(advDivider);

        _packageIdText = new TextBlock
        {
            FontFamily = MonoFont,
            FontSize = Theme.CaptionSize,
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        var expander = new Expander
        {
            Header = "Advanced",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = _packageIdText,
            Background = Theme.Subtle,
        };
        stack.Children.Add(expander);

        _frame.Child = stack;
        Content = _frame;
        Visibility = Visibility.Collapsed;
    }

    public AppIcon Icon => _icon;

    public void ShowItem(MarketplaceItem item, CatalogDetails? details = null)
    {
        _current = item;
        Visibility = Visibility.Visible;

        _icon.Show("catalog:" + item.CatalogId, item.Name);
        _name.Text = item.Name;
        _publisher.Text = details?.Publisher ?? item.Publisher;

        _chips.Children.Clear();
        string ver = details?.LatestVersion ?? item.UpdateVersion ?? item.InstalledVersion ?? item.Version;
        if (!string.IsNullOrWhiteSpace(ver))
        {
            _chips.Children.Add(Chrome.Chip($"Version {ver}", ChipKind.Neutral));
        }

        if (!string.IsNullOrWhiteSpace(item.Category))
        {
            _chips.Children.Add(Chrome.Chip(item.Category, ChipKind.Accent));
        }

        switch (item.State)
        {
            case MarketplaceItemState.Installed:
                _action.Content = "Installed on this PC";
                _action.IsEnabled = false;
                _note.Text = $"Installed version: {item.InstalledVersion ?? "Unknown"}";
                break;
            case MarketplaceItemState.UpdateAvailable:
                _action.Content = $"Update to {item.UpdateVersion ?? "latest"}";
                _action.IsEnabled = true;
                _note.Text = $"Currently installed: {item.InstalledVersion ?? "Unknown"}";
                break;
            default:
                _action.Content = "Install";
                _action.IsEnabled = true;
                _note.Text = "Installs quietly from the Microsoft catalog.";
                break;
        }

        _description.Text = !string.IsNullOrWhiteSpace(details?.Description)
            ? details.Description
            : !string.IsNullOrWhiteSpace(item.Summary)
                ? item.Summary
                : "Loading package details from the Microsoft catalog…";

        _factsPublisher.Text = details?.Publisher ?? item.Publisher;
        _factsLicense.Text = string.IsNullOrWhiteSpace(details?.License) ? "—" : details.License;
        _factsSource.Text = item.SourceLabel;

        if (!string.IsNullOrWhiteSpace(details?.Homepage) && Uri.TryCreate(details.Homepage, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            _websiteLink.Content = uri.Host;
            _websiteLink.NavigateUri = uri;
            _websiteLink.Visibility = Visibility.Visible;
        }
        else
        {
            _websiteLink.Visibility = Visibility.Collapsed;
        }

        _packageIdText.Text = $"Package ID: {item.CatalogId}\nSource: {item.SourceLabel}";
    }

    private static TextBlock ValueText() => new()
    {
        FontFamily = Chrome.Font,
        FontSize = 13,
        Foreground = Theme.Text,
        TextWrapping = TextWrapping.Wrap,
    };

    private static void AddFact(Grid grid, int row, string label, FrameworkElement value)
    {
        var lbl = Chrome.Caption(label);
        lbl.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(lbl, row);
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        Grid.SetRow(value, row);
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);
    }
}
