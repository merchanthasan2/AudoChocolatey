using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

/// <summary>Marketplace — winget search and gated install (Phase 44.3).</summary>
public sealed class MarketplaceView : Grid
{
    private readonly AppServices _services;
    private readonly TextBox _queryBox;
    private readonly Button _searchButton;
    private readonly TextBlock _statusText;
    private readonly StackPanel _listPanel;
    private CancellationTokenSource? _pageCts;

    public MarketplaceView(AppServices services)
    {
        _services = services;
        Padding = new Thickness(0);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var searchRow = new Grid { ColumnSpacing = 8 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _queryBox = new TextBox
        {
            PlaceholderText = "Search winget catalog…",
        };
        Grid.SetColumn(_queryBox, 0);
        searchRow.Children.Add(_queryBox);

        _searchButton = new Button { Content = "Search" };
        _searchButton.Click += OnSearchClick;
        _queryBox.KeyDown += (_, e) =>
        {
            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                OnSearchClick(_searchButton, new RoutedEventArgs());
            }
        };
        Grid.SetColumn(_searchButton, 1);
        searchRow.Children.Add(_searchButton);

        Grid.SetRow(searchRow, 0);
        Children.Add(searchRow);

        _statusText = new TextBlock
        {
            Text = "Enter a search term and click Search.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.9,
            Margin = new Thickness(0, 12, 0, 0),
        };
        Grid.SetRow(_statusText, 1);
        Children.Add(_statusText);

        var hint = new TextBlock
        {
            Text = "Install requires confirmation on each row.",
            FontSize = 12,
            Opacity = 0.75,
            Margin = new Thickness(0, 4, 0, 0),
        };
        Grid.SetRow(hint, 2);
        Children.Add(hint);

        _listPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        var listScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _listPanel,
        };
        Grid.SetRow(listScroll, 3);
        Children.Add(listScroll);
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        _pageCts?.Cancel();
        _pageCts = new CancellationTokenSource();
        var token = _pageCts.Token;

        var query = _queryBox.Text;
        _searchButton.IsEnabled = false;
        _statusText.Text = "Searching winget catalog…";
        _listPanel.Children.Clear();

        try
        {
            var packages = await _services.Marketplace.SearchAsync(query, token).ConfigureAwait(true);

            _listPanel.Children.Clear();
            if (packages.Count == 0)
            {
                var detail = string.IsNullOrEmpty(_services.Marketplace.LastError)
                    ? "No packages found."
                    : _services.Marketplace.LastError;
                _statusText.Text = detail;
                return;
            }

            _statusText.Text = $"Found {packages.Count} package(s).";
            foreach (var pkg in packages)
            {
                _listPanel.Children.Add(BuildRow(pkg, token));
            }
        }
        catch (Exception ex)
        {
            _statusText.Text = ex.Message;
        }
        finally
        {
            _searchButton.IsEnabled = true;
        }
    }

    private UIElement BuildRow(MarketplacePackage pkg, CancellationToken pageToken)
    {
        var row = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = $"{pkg.Name}\n{pkg.Id}",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var version = new TextBlock
        {
            Text = pkg.Version,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.85,
        };
        Grid.SetColumn(version, 1);
        row.Children.Add(version);

        var confirm = new CheckBox
        {
            Content = "Confirm",
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(confirm, 2);
        row.Children.Add(confirm);

        var installButton = new Button
        {
            Content = "Install",
            Tag = (pkg, confirm),
            VerticalAlignment = VerticalAlignment.Center,
        };
        installButton.Click += async (_, _) =>
            await OnInstallClickAsync(installButton, pageToken).ConfigureAwait(true);
        Grid.SetColumn(installButton, 3);
        row.Children.Add(installButton);

        return row;
    }

    private async Task OnInstallClickAsync(Button installButton, CancellationToken pageToken)
    {
        if (installButton.Tag is not (MarketplacePackage pkg, CheckBox confirm))
        {
            return;
        }

        if (confirm.IsChecked != true)
        {
            _statusText.Text = $"Install {pkg.Id} requires confirmation — check Confirm on that row first.";
            return;
        }

        installButton.IsEnabled = false;
        _statusText.Text = $"Installing {pkg.Id}…";

        try
        {
            var result = await _services.Marketplace.InstallAsync(pkg.Id, pageToken).ConfigureAwait(true);
            _statusText.Text = result.Success
                ? result.Message
                : (_services.Marketplace.LastError ?? result.Message);
        }
        catch (Exception ex)
        {
            _statusText.Text = ex.Message;
        }
        finally
        {
            installButton.IsEnabled = true;
        }
    }
}