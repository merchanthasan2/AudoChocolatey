using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

/// <summary>Installed tab — refresh and gated per-package winget uninstall (Phase 44.2).</summary>
public sealed class InstalledView : Grid
{
    private readonly AppServices _services;
    private readonly Button _refreshButton;
    private readonly CheckBox _confirmUninstallCheckBox;
    private readonly TextBlock _statusText;
    private readonly StackPanel _listPanel;
    private CancellationTokenSource? _pageCts;

    public InstalledView(AppServices services)
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
        _refreshButton.Click += OnRefreshClick;
        Grid.SetRow(_refreshButton, 0);
        Children.Add(_refreshButton);

        _confirmUninstallCheckBox = new CheckBox
        {
            Content = "I confirm I want to run winget uninstall for the selected package",
            IsChecked = false,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(_confirmUninstallCheckBox, 1);
        Children.Add(_confirmUninstallCheckBox);

        _statusText = new TextBlock
        {
            Text = "Click Refresh to load installed packages.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.9,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(_statusText, 2);
        Children.Add(_statusText);

        _listPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        var listScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _listPanel,
        };
        Grid.SetRow(listScroll, 3);
        Children.Add(listScroll);
    }

    public void OnNavigatedAway()
    {
        _pageCts?.Cancel();
        _pageCts = null;
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _pageCts?.Cancel();
        _pageCts = new CancellationTokenSource();
        var token = _pageCts.Token;

        _refreshButton.IsEnabled = false;
        _statusText.Text = "Loading installed packages from winget…";
        _listPanel.Children.Clear();

        try
        {
            var packages = await _services.Installed.ListInstalledAsync(token).ConfigureAwait(true);

            _listPanel.Children.Clear();
            if (packages.Count == 0)
            {
                var detail = string.IsNullOrEmpty(_services.Installed.LastError)
                    ? "No installed packages found."
                    : _services.Installed.LastError;
                _statusText.Text = detail;
                return;
            }

            _statusText.Text = $"Found {packages.Count} installed package(s).";
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
            _refreshButton.IsEnabled = true;
        }
    }

    private UIElement BuildRow(InstalledPackage pkg, CancellationToken pageToken)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = $"{pkg.Name} ({pkg.Id})\n{pkg.Version}",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var uninstallButton = new Button
        {
            Content = "Uninstall",
            Tag = pkg,
            VerticalAlignment = VerticalAlignment.Center,
        };
        uninstallButton.Click += async (_, _) => await OnUninstallClickAsync(uninstallButton, pageToken).ConfigureAwait(true);
        Grid.SetColumn(uninstallButton, 2);
        row.Children.Add(uninstallButton);

        return row;
    }

    private async Task OnUninstallClickAsync(Button uninstallButton, CancellationToken pageToken)
    {
        if (_confirmUninstallCheckBox.IsChecked != true)
        {
            _statusText.Text = "Uninstall requires confirmation — check the confirmation box first.";
            return;
        }

        if (uninstallButton.Tag is not InstalledPackage pkg)
        {
            return;
        }

        uninstallButton.IsEnabled = false;
        _statusText.Text = $"Uninstalling {pkg.Id}…";

        try
        {
            var result = await _services.Installed.UninstallAsync(pkg.Id, pageToken).ConfigureAwait(true);
            _statusText.Text = result.Success
                ? result.Message
                : (_services.Installed.LastError ?? result.Message);
        }
        catch (Exception ex)
        {
            _statusText.Text = ex.Message;
        }
        finally
        {
            uninstallButton.IsEnabled = true;
        }
    }
}