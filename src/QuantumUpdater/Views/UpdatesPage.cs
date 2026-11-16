using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

public sealed class UpdatesPage : Grid
{
    private readonly AppServices _services;
    private readonly CheckBox _confirm;
    private readonly TextBlock _status;
    private readonly StackPanel _list;
    private readonly Button _refresh;

    public UpdatesPage(AppServices services)
    {
        _services = services;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(Chrome.Title("Updates & Health"));
        _status = Chrome.Line("Checking winget for upgrades…");
        header.Children.Add(_status);
        _confirm = Chrome.Confirm("I confirm this upgrade");
        header.Children.Add(_confirm);
        _refresh = Chrome.Action("Check again");
        _refresh.HorizontalAlignment = HorizontalAlignment.Left;
        _refresh.Click += async (_, _) => await LoadAsync(force: true).ConfigureAwait(true);
        header.Children.Add(_refresh);
        Children.Add(header);

        _list = new StackPanel { Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
        var scroll = new ScrollViewer { Content = _list };
        Grid.SetRow(scroll, 1);
        Children.Add(scroll);

        Loaded += async (_, _) => await LoadAsync(force: false).ConfigureAwait(true);
    }

    private async Task LoadAsync(bool force)
    {
        _refresh.IsEnabled = false;
        _list.Children.Clear();
        try
        {
            if (force || _services.Catalog is null)
            {
                _status.Text = "Checking winget for upgrades…";
                _status.Foreground = Theme.Muted;
                var packages = await _services.Winget.ListUpgradesAsync().ConfigureAwait(true);
                var previous = _services.Catalog;
                _services.Catalog = new CatalogSnapshot
                {
                    Upgrades = packages,
                    UpgradesError = _services.Winget.LastError,
                    Installed = previous?.Installed ?? Array.Empty<InstalledPackage>(),
                    InstalledError = previous?.InstalledError,
                };
            }

            var catalog = _services.Catalog!;
            if (!string.IsNullOrEmpty(catalog.UpgradesError))
            {
                SetStatus("Failed", Theme.Critical, catalog.UpgradesError);
                return;
            }

            if (catalog.Upgrades.Count == 0)
            {
                SetStatus("Healthy", Theme.Healthy, "No upgrades are waiting.");
                return;
            }

            SetStatus("Pending", Theme.Warning, $"{catalog.Upgrades.Count} upgrade(s) waiting.");
            foreach (var package in catalog.Upgrades)
            {
                _list.Children.Add(Row(package));
            }
        }
        catch (Exception ex)
        {
            SetStatus("Failed", Theme.Critical, ex.Message);
        }
        finally
        {
            _refresh.IsEnabled = true;
        }
    }

    private UIElement Row(WingetPackage package)
    {
        var body = new Grid { ColumnSpacing = 12 };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Chrome.Line($"{package.Name}  ·  {package.Id}\n{package.Version} → {package.Available}", Theme.Text);
        body.Children.Add(label);
        var button = Chrome.Action("Upgrade");
        button.Tag = package;
        button.Click += async (_, _) => await UpgradeAsync(button).ConfigureAwait(true);
        Grid.SetColumn(button, 1);
        body.Children.Add(button);
        return Chrome.Card(body);
    }

    private async Task UpgradeAsync(Button button)
    {
        if (_confirm.IsChecked != true)
        {
            SetStatus("Pending", Theme.Warning, "Check confirm before upgrading.");
            return;
        }

        if (button.Tag is not WingetPackage package)
        {
            return;
        }

        button.IsEnabled = false;
        SetStatus("Pending", Theme.Warning, $"Upgrading {package.Id}…");
        try
        {
            var result = await _services.Winget.UpgradeAsync(package.Id).ConfigureAwait(true);
            SetStatus(result.Success ? "Healthy" : "Failed", result.Success ? Theme.Healthy : Theme.Critical, result.Message);
            await _services.Account.ReportActivityAsync(_services.Session, "upgrade", package.Id, result.Success).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatus("Failed", Theme.Critical, ex.Message);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void SetStatus(string state, Microsoft.UI.Xaml.Media.SolidColorBrush color, string detail)
    {
        _status.Text = $"{state}. {detail}";
        _status.Foreground = color;
    }
}
