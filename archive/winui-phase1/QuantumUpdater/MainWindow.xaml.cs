using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Models;
using QuantumUpdater.Services;
using QuantumUpdater.Views;

namespace QuantumUpdater;

/// <summary>
/// Phase 44.1 product shell — NavigationView with code-built pages (no page XAML).
/// </summary>
public sealed class MainWindow : Window
{
    private readonly AppServices _services = new();
    private readonly NavigationView _navigationView;
    private readonly Grid _contentHost;
    private UpdatesView? _updatesView;
    private GettingReadyView? _gettingReadyView;
    private InstalledView? _installedView;
    private MarketplaceView? _marketplaceView;
    private AboutView? _aboutView;
    private bool _autoNavigatedToUpdates;
    private string _currentTag = "GettingReady";

    public MainWindow()
    {
        Title = "Quantum Updater";

        _contentHost = new Grid
        {
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _navigationView = new NavigationView
        {
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            SelectionFollowsFocus = NavigationViewSelectionFollowsFocus.Disabled,
            Content = _contentHost,
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
        };

        _navigationView.MenuItems.Add(MakeNavItem("Getting ready", "GettingReady", Symbol.Home));
        _navigationView.MenuItems.Add(MakeNavItem("Updates", "Updates", Symbol.Refresh));
        _navigationView.MenuItems.Add(MakeNavItem("Marketplace", "Marketplace", Symbol.Shop));
        _navigationView.MenuItems.Add(MakeNavItem("Installed", "Installed", Symbol.List));
        _navigationView.MenuItems.Add(MakeNavItem("About", "About", Symbol.Help));

        _navigationView.MenuItems.Add(new NavigationViewItemHeader { Content = "Phase B" });
        _navigationView.MenuItems.Add(MakeNavItem("Cleanup", "Cleanup", Symbol.Delete));
        _navigationView.MenuItems.Add(MakeNavItem("Settings", "Settings", Symbol.Setting));

        _navigationView.ItemInvoked += OnNavItemInvoked;

        var header = new TextBlock
        {
            Text = $"Quantum Updater {AppVersion.Label}",
            FontSize = 14,
            Opacity = 0.85,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _navigationView.PaneHeader = header;

        Content = _navigationView;

        if (_navigationView.MenuItems[0] is NavigationViewItem first)
        {
            first.IsSelected = true;
        }

        ShowPage("GettingReady");
    }

    private static NavigationViewItem MakeNavItem(string label, string tag, Symbol icon)
    {
        return new NavigationViewItem
        {
            Content = label,
            Tag = tag,
            Icon = new SymbolIcon(icon),
        };
    }

    private void OnNavItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            ShowPage(tag);
        }
    }

    private void ShowPage(string tag)
    {
        if (tag == _currentTag && tag != "Updates")
        {
            return;
        }

        if (_currentTag == "Updates")
        {
            _updatesView?.OnNavigatedAway();
        }

        if (_currentTag == "Installed")
        {
            _installedView?.OnNavigatedAway();
        }

        _currentTag = tag;
        _contentHost.Children.Clear();

        if (tag == "Updates")
        {
            _updatesView ??= new UpdatesView(_services);
            _contentHost.Children.Add(_updatesView);
            return;
        }

        if (tag == "GettingReady")
        {
            if (_gettingReadyView is null)
            {
                _gettingReadyView = new GettingReadyView(_services);
                _gettingReadyView.BothScansSettled += OnGettingReadyScansSettled;
            }

            _contentHost.Children.Add(_gettingReadyView);
            return;
        }

        if (tag == "Installed")
        {
            _installedView ??= new InstalledView(_services);
            _contentHost.Children.Add(_installedView);
            return;
        }

        if (tag == "Marketplace")
        {
            _marketplaceView ??= new MarketplaceView(_services);
            _contentHost.Children.Add(_marketplaceView);
            return;
        }

        if (tag == "About")
        {
            _aboutView ??= new AboutView(_services);
            _contentHost.Children.Add(_aboutView);
            return;
        }

        var page = tag switch
        {
            "Cleanup" or "Settings" => BuildPhaseBPage(tag),
            _ => BuildSimplePage(tag, "Unknown section."),
        };

        _contentHost.Children.Add(page);
    }

    private static StackPanel BuildSimplePage(string title, string status)
    {
        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 24,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                },
                new TextBlock
                {
                    Text = status,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.9,
                },
            },
        };
    }

    private void OnGettingReadyScansSettled()
    {
        // Scans may finish off the UI thread — marshal before touching NavigationView.
        var dq = DispatcherQueue;
        if (dq is not null && !dq.HasThreadAccess)
        {
            _ = dq.TryEnqueue(OnGettingReadyScansSettled);
            return;
        }

        if (_autoNavigatedToUpdates)
        {
            return;
        }

        _autoNavigatedToUpdates = true;
        SelectNavItem("Updates");
        ShowPage("Updates");
    }

    private void SelectNavItem(string tag)
    {
        foreach (var item in _navigationView.MenuItems)
        {
            if (item is NavigationViewItem nav && tag.Equals(nav.Tag as string, StringComparison.Ordinal))
            {
                nav.IsSelected = true;
                return;
            }
        }
    }

    private static StackPanel BuildPhaseBPage(string title)
    {
        return BuildSimplePage(title, "Phase B — coming");
    }
}