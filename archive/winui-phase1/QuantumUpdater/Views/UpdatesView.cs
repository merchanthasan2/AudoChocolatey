using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

/// <summary>Updates tab — refresh, confirm, and per-package winget upgrade (Phase 43.4 behaviour).</summary>
public sealed class UpdatesView : Grid
{
    private readonly AppServices _services;
    private readonly Button _refreshButton;
    private readonly CheckBox _confirmUpgradeCheckBox;
    private readonly TextBlock _updatesStatusText;
    private readonly StackPanel _updatesListPanel;
    private CancellationTokenSource? _updatesCts;

    public UpdatesView(AppServices services)
    {
        _services = services;
        Padding = new Thickness(0);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _refreshButton = new Button
        {
            Content = "Refresh",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _refreshButton.Click += OnRefreshUpdatesClick;
        Grid.SetRow(_refreshButton, 0);
        Children.Add(_refreshButton);

        _confirmUpgradeCheckBox = new CheckBox
        {
            Content = "I confirm I want to run winget upgrade for the selected package",
            IsChecked = false,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(_confirmUpgradeCheckBox, 1);
        Children.Add(_confirmUpgradeCheckBox);

        _updatesStatusText = new TextBlock
        {
            Text = "Click Refresh to load available upgrades.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.9,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(_updatesStatusText, 2);
        Children.Add(_updatesStatusText);

        _updatesListPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        var listScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _updatesListPanel,
        };
        Grid.SetRow(listScroll, 3);
        Children.Add(listScroll);
    }

    public void OnNavigatedAway()
    {
        _updatesCts?.Cancel();
        _updatesCts = null;
    }

    private async void OnRefreshUpdatesClick(object sender, RoutedEventArgs e)
    {
        _updatesCts?.Cancel();
        _updatesCts = new CancellationTokenSource();
        var token = _updatesCts.Token;

        _refreshButton.IsEnabled = false;
        _updatesStatusText.Text = "Loading upgrades from winget…";
        _updatesListPanel.Children.Clear();

        try
        {
            var packages = await _services.Winget.ListUpgradesAsync(token).ConfigureAwait(true);

            _updatesListPanel.Children.Clear();
            if (packages.Count == 0)
            {
                var detail = string.IsNullOrEmpty(_services.Winget.LastError)
                    ? "No upgrades found."
                    : _services.Winget.LastError;
                _updatesStatusText.Text = detail;
                return;
            }

            _updatesStatusText.Text = $"Found {packages.Count} upgrade(s).";
            foreach (var pkg in packages)
            {
                _updatesListPanel.Children.Add(BuildUpgradeRow(pkg, token));
            }
        }
        catch (Exception ex)
        {
            _updatesStatusText.Text = ex.Message;
        }
        finally
        {
            _refreshButton.IsEnabled = true;
        }
    }

    private UIElement BuildUpgradeRow(WingetPackage pkg, CancellationToken pageToken)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = $"{pkg.Name} ({pkg.Id})\n{pkg.Version} → {pkg.Available}",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var upgradeButton = new Button
        {
            Content = "Upgrade",
            Tag = pkg,
            VerticalAlignment = VerticalAlignment.Center,
        };
        upgradeButton.Click += async (_, _) => await OnUpgradeRowClickAsync(upgradeButton, pageToken).ConfigureAwait(true);
        Grid.SetColumn(upgradeButton, 2);
        row.Children.Add(upgradeButton);

        return row;
    }

    private async Task OnUpgradeRowClickAsync(Button upgradeButton, CancellationToken pageToken)
    {
        if (_confirmUpgradeCheckBox.IsChecked != true)
        {
            _updatesStatusText.Text = "Upgrade requires confirm in Phase 2 — check the confirmation box first.";
            return;
        }

        if (upgradeButton.Tag is not WingetPackage pkg)
        {
            return;
        }

        upgradeButton.IsEnabled = false;
        _updatesStatusText.Text = $"Upgrading {pkg.Id}…";

        try
        {
            var result = await _services.Winget.UpgradeAsync(pkg.Id, pageToken).ConfigureAwait(true);
            _updatesStatusText.Text = result.Success
                ? result.Message
                : (_services.Winget.LastError ?? result.Message);
        }
        catch (Exception ex)
        {
            _updatesStatusText.Text = ex.Message;
        }
        finally
        {
            upgradeButton.IsEnabled = true;
        }
    }
}