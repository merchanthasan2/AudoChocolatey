using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

public sealed class InstalledPage : Grid
{
    private readonly AppServices _services;
    private readonly CheckBox _confirm;
    private readonly TextBlock _status;
    private readonly StackPanel _list;
    private readonly Button _refresh;

    public InstalledPage(AppServices services)
    {
        _services = services;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(Chrome.Title("Installed"));
        _status = Chrome.Line("Loading installed packages…");
        header.Children.Add(_status);
        _confirm = Chrome.Confirm("I confirm this uninstall");
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
            if (force || _services.Catalog?.Installed is null || (_services.Catalog.Installed.Count == 0 && _services.Catalog.InstalledError is null && force))
            {
                // A missing catalog, or an explicit check again, runs winget list.
            }

            var haveList = _services.Catalog is { InstalledError: not null } || (_services.Catalog?.Installed.Count > 0);
            if (force || !haveList)
            {
                _status.Text = "Loading installed packages…";
                _status.Foreground = Theme.Muted;
                var packages = await _services.Installed.ListInstalledAsync().ConfigureAwait(true);
                var previous = _services.Catalog;
                _services.Catalog = new CatalogSnapshot
                {
                    Upgrades = previous?.Upgrades ?? Array.Empty<WingetPackage>(),
                    UpgradesError = previous?.UpgradesError,
                    Installed = packages,
                    InstalledError = _services.Installed.LastError,
                };
            }

            var catalog = _services.Catalog!;
            if (!string.IsNullOrEmpty(catalog.InstalledError) && catalog.Installed.Count == 0)
            {
                _status.Text = catalog.InstalledError;
                _status.Foreground = Theme.Critical;
                return;
            }

            _status.Text = $"{catalog.Installed.Count} installed package(s).";
            _status.Foreground = Theme.Text;
            foreach (var package in catalog.Installed.Take(80))
            {
                _list.Children.Add(Row(package));
            }
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.Foreground = Theme.Critical;
        }
        finally
        {
            _refresh.IsEnabled = true;
        }
    }

    private UIElement Row(InstalledPackage package)
    {
        var body = new Grid { ColumnSpacing = 12 };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.Children.Add(Chrome.Line($"{package.Name}  ·  {package.Id}\n{package.Version}", Theme.Text));
        var button = Chrome.Action("Uninstall");
        button.Tag = package;
        button.Click += async (_, _) => await UninstallAsync(button).ConfigureAwait(true);
        Grid.SetColumn(button, 1);
        body.Children.Add(button);
        return Chrome.Card(body);
    }

    private async Task UninstallAsync(Button button)
    {
        if (_confirm.IsChecked != true)
        {
            _status.Text = "Check confirm before uninstalling.";
            _status.Foreground = Theme.Warning;
            return;
        }

        if (button.Tag is not InstalledPackage package)
        {
            return;
        }

        button.IsEnabled = false;
        _status.Text = $"Uninstalling {package.Id}…";
        try
        {
            var result = await _services.Installed.UninstallAsync(package.Id).ConfigureAwait(true);
            _status.Text = result.Message;
            _status.Foreground = result.Success ? Theme.Healthy : Theme.Critical;
            await _services.Account.ReportActivityAsync(_services.Session, "remove", package.Id, result.Success).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.Foreground = Theme.Critical;
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
