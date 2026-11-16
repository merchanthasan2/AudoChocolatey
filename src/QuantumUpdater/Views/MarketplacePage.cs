using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

public sealed class MarketplacePage : Grid
{
    private readonly AppServices _services;
    private readonly TextBox _query;
    private readonly CheckBox _confirm;
    private readonly TextBlock _status;
    private readonly StackPanel _list;
    private readonly Button _search;

    public MarketplacePage(AppServices services)
    {
        _services = services;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(Chrome.Title("App Marketplace"));
        _status = Chrome.Line("Search winget, then confirm one install.");
        header.Children.Add(_status);
        _query = new TextBox
        {
            PlaceholderText = "Package name",
            Background = Theme.Card,
            Foreground = Theme.Text,
            BorderBrush = Theme.Stroke,
        };
        header.Children.Add(_query);
        _confirm = Chrome.Confirm("I confirm this install");
        header.Children.Add(_confirm);
        _search = Chrome.Action("Search");
        _search.HorizontalAlignment = HorizontalAlignment.Left;
        _search.Click += async (_, _) => await SearchAsync().ConfigureAwait(true);
        header.Children.Add(_search);
        Children.Add(header);

        _list = new StackPanel { Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
        var scroll = new ScrollViewer { Content = _list };
        Grid.SetRow(scroll, 1);
        Children.Add(scroll);
    }

    private async Task SearchAsync()
    {
        _search.IsEnabled = false;
        _list.Children.Clear();
        _status.Text = "Searching…";
        _status.Foreground = Theme.Muted;
        try
        {
            var packages = await _services.Marketplace.SearchAsync(_query.Text).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(_services.Marketplace.LastError))
            {
                _status.Text = _services.Marketplace.LastError;
                _status.Foreground = Theme.Critical;
                return;
            }

            _status.Text = packages.Count == 0 ? "No packages matched." : $"{packages.Count} package(s).";
            _status.Foreground = Theme.Text;
            foreach (var package in packages.Take(40))
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
            _search.IsEnabled = true;
        }
    }

    private UIElement Row(MarketplacePackage package)
    {
        var body = new Grid { ColumnSpacing = 12 };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.Children.Add(Chrome.Line($"{package.Name}  ·  {package.Id}\n{package.Version}", Theme.Text));
        var button = Chrome.Action("Install");
        button.Tag = package;
        button.Click += async (_, _) => await InstallAsync(button).ConfigureAwait(true);
        Grid.SetColumn(button, 1);
        body.Children.Add(button);
        return Chrome.Card(body);
    }

    private async Task InstallAsync(Button button)
    {
        if (_confirm.IsChecked != true)
        {
            _status.Text = "Check confirm before installing.";
            _status.Foreground = Theme.Warning;
            return;
        }

        if (button.Tag is not MarketplacePackage package)
        {
            return;
        }

        button.IsEnabled = false;
        _status.Text = $"Installing {package.Id}…";
        try
        {
            var result = await _services.Marketplace.InstallAsync(package.Id).ConfigureAwait(true);
            _status.Text = result.Message;
            _status.Foreground = result.Success ? Theme.Healthy : Theme.Critical;
            await _services.Account.ReportActivityAsync(_services.Session, "install", package.Id, result.Success).ConfigureAwait(true);
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
