using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Catalog;

namespace QuantumUpdater.Views.Controls;

/// <summary>
/// Marketplace home card matching mockup 03-marketplace-home.html .card (46.6.6).
/// Supports Install, Update, Installed, Waiting, and Working (with inline progress) states.
/// </summary>
public sealed class AppCard : UserControl
{
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly SolidColorBrush TrackBrush = new(Windows.UI.Color.FromArgb(255, 0x3A, 0x3A, 0x3A));

    private readonly Border _frame;
    private readonly AppIcon _icon = new(48);
    private readonly TextBlock _name;
    private readonly TextBlock _publisher;
    private readonly TextBlock _summary;
    private readonly TextBlock _footInfo;
    private readonly Button _action;
    private readonly ProgressBar _bar;
    private readonly TextBlock _progText;
    private readonly StackPanel _installedPill;

    private MarketplaceItem? _item;

    public event Action<MarketplaceItem>? InstallRequested;
    public event Action<MarketplaceItem>? Selected;
    public event Action<Guid>? CancelRequested;

    private Guid? _activeActionId;

    public AppCard()
    {
        _frame = new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Padding = new Thickness(16),
            MinHeight = 172,
        };

        PointerEntered += (_, _) => _frame.Background = Theme.CardHover;
        PointerExited += (_, _) => _frame.Background = Theme.Card;
        Tapped += (_, e) =>
        {
            if (e.OriginalSource is not Button && _item is not null)
            {
                Selected?.Invoke(_item);
            }
        };

        var root = new Grid { RowSpacing = 10 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header: 48 px icon + name + publisher
        var top = new Grid { ColumnSpacing = 12 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_icon, 0);
        top.Children.Add(_icon);

        _name = new TextBlock
        {
            FontFamily = Chrome.Font,
            FontSize = Theme.BodySize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        _publisher = Chrome.Caption("");
        _publisher.TextTrimming = TextTrimming.CharacterEllipsis;
        _publisher.TextWrapping = TextWrapping.NoWrap;

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(_name);
        titleStack.Children.Add(_publisher);
        Grid.SetColumn(titleStack, 1);
        top.Children.Add(titleStack);
        Grid.SetRow(top, 0);
        root.Children.Add(top);

        // Summary
        _summary = new TextBlock
        {
            FontFamily = Chrome.Font,
            FontSize = 13,
            Foreground = Theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetRow(_summary, 1);
        root.Children.Add(_summary);

        // Footer
        var foot = new Grid { ColumnSpacing = 8 };
        foot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _footInfo = Chrome.Caption("");
        _footInfo.VerticalAlignment = VerticalAlignment.Center;
        _footInfo.TextTrimming = TextTrimming.CharacterEllipsis;

        _bar = new ProgressBar
        {
            Height = 4,
            Minimum = 0,
            Maximum = 100,
            Background = TrackBrush,
            Foreground = Theme.Accent,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _progText = new TextBlock
        {
            FontFamily = Chrome.Font,
            FontSize = Theme.CaptionSize,
            Foreground = Theme.AccentText,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        var leftFoot = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        leftFoot.Children.Add(_footInfo);
        leftFoot.Children.Add(_bar);
        Grid.SetColumn(leftFoot, 0);
        foot.Children.Add(leftFoot);

        _installedPill = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        _installedPill.Children.Add(new FontIcon
        {
            Glyph = "\uE73E",
            FontFamily = IconFont,
            FontSize = 12,
            Foreground = Theme.HealthyText,
        });
        _installedPill.Children.Add(new TextBlock
        {
            Text = "Installed",
            FontFamily = Chrome.Font,
            FontSize = 13,
            Foreground = Theme.HealthyText,
            FontWeight = FontWeights.SemiBold,
        });

        _action = Chrome.Action("Install");
        _action.Padding = new Thickness(14, 5, 14, 5);
        _action.Click += (_, _) =>
        {
            if (_activeActionId is Guid id)
            {
                CancelRequested?.Invoke(id);
            }
            else if (_item is not null)
            {
                InstallRequested?.Invoke(_item);
            }
        };

        var rightFoot = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        rightFoot.Children.Add(_progText);
        rightFoot.Children.Add(_installedPill);
        rightFoot.Children.Add(_action);
        Grid.SetColumn(rightFoot, 1);
        foot.Children.Add(rightFoot);

        Grid.SetRow(foot, 2);
        root.Children.Add(foot);

        _frame.Child = root;
        Content = _frame;
    }

    public AppIcon Icon => _icon;

    public void Apply(MarketplaceItem item, QueuedAction? queueAction = null)
    {
        _item = item;
        _name.Text = item.Name;
        _publisher.Text = item.Publisher;
        _summary.Text = string.IsNullOrWhiteSpace(item.Summary) ? item.CatalogId : item.Summary;
        _icon.Show("catalog:" + item.CatalogId, item.Name);

        _activeActionId = null;
        _bar.Visibility = Visibility.Collapsed;
        _progText.Visibility = Visibility.Collapsed;
        _installedPill.Visibility = Visibility.Collapsed;
        _action.Visibility = Visibility.Visible;
        _footInfo.Visibility = Visibility.Visible;

        if (queueAction is not null && queueAction.State is ActionState.Waiting or ActionState.Working)
        {
            _activeActionId = queueAction.Id;
            _footInfo.Visibility = Visibility.Collapsed;
            _bar.Visibility = Visibility.Visible;
            _progText.Visibility = Visibility.Visible;
            _action.Content = "Cancel";
            _action.Background = Theme.CardHover;

            if (queueAction.State == ActionState.Waiting)
            {
                _bar.IsIndeterminate = false;
                _bar.Value = 0;
                _progText.Text = "Waiting";
            }
            else if (queueAction.Percent is int pct)
            {
                _bar.IsIndeterminate = false;
                _bar.Value = pct;
                _progText.Text = $"{queueAction.Phase ?? "Working"} · {pct}%";
            }
            else
            {
                _bar.IsIndeterminate = true;
                _progText.Text = queueAction.Phase ?? "Installing";
            }

            return;
        }

        _action.Background = Theme.Accent;
        switch (item.State)
        {
            case MarketplaceItemState.Installed:
                _action.Visibility = Visibility.Collapsed;
                _installedPill.Visibility = Visibility.Visible;
                _footInfo.Text = string.IsNullOrWhiteSpace(item.InstalledVersion)
                    ? item.SourceLabel
                    : $"Version {item.InstalledVersion}";
                break;

            case MarketplaceItemState.UpdateAvailable:
                _action.Content = "Update";
                _footInfo.Text = string.IsNullOrWhiteSpace(item.InstalledVersion)
                    ? "Update available"
                    : $"Installed · {item.InstalledVersion}";
                break;

            default:
                _action.Content = "Install";
                _footInfo.Text = string.IsNullOrWhiteSpace(item.Version)
                    ? item.SourceLabel
                    : $"Version {item.Version}";
                break;
        }

        AutomationProperties.SetName(_action, $"{_action.Content} {item.Name}");
    }
}
