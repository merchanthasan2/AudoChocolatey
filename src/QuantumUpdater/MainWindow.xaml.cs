using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using QuantumUpdater.Services;
using QuantumUpdater.Views;
using QuantumUpdater.Views.Dev; // TEMP DIAGNOSTIC 46.7.49

namespace QuantumUpdater;

public sealed class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly StackPanel _nav = new() { Spacing = 4, Margin = new Thickness(8) };
    private readonly Grid _host = new() { Margin = new Thickness(24) };
    private readonly Dictionary<string, Button> _navButtons = new();
    private string _current = "";
    private bool _readyAdvanced;
    private readonly StackPanel _accountFooter = new() { Spacing = 4, Margin = new Thickness(16, 8, 16, 16) };

    public MainWindow()
    {
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _services = new AppServices(a => dispatcher.TryEnqueue(() => a()));
        Title = "DeskVerve";
        // TEMP DIAGNOSTIC 46.7.37: the switches are read one time (DiagSwitches is static).
        var noTitleBar = DiagSwitches.Has("notitlebar"); // TEMP DIAGNOSTIC 46.7.37
        var titleBarLate = DiagSwitches.Has("titlebarlate"); // TEMP DIAGNOSTIC 46.7.37
        var noEmblem = DiagSwitches.Has("noemblem"); // TEMP DIAGNOSTIC 46.7.37
        CrashLog.Write("Mark:Diag " + (DiagSwitches.RawValue.Length == 0 ? "none" : DiagSwitches.RawValue), null); // TEMP DIAGNOSTIC 46.7.37
        if (!noTitleBar) // TEMP DIAGNOSTIC 46.7.37
        {
            ExtendsContentIntoTitleBar = true;
        }

        if (!DiagSwitches.Has("nomica")) // TEMP DIAGNOSTIC 46.7.37
        {
            SystemBackdrop = new MicaBackdrop();
        }

        var titleBar = new Grid
        {
            Height = 40,
            Background = Theme.Sidebar,
        };
        var titleContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (!noEmblem) // TEMP DIAGNOSTIC 46.7.37
        {
            var titleLogo = BrandAssets.Emblem(24);
            AutomationProperties.SetAccessibilityView(titleLogo, AccessibilityView.Raw);
            titleContent.Children.Add(titleLogo);
        }

        titleContent.Children.Add(new TextBlock
        {
            Text = "DeskVerve",
            Foreground = Theme.Text,
            FontFamily = Chrome.Font,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        titleBar.Children.Add(titleContent);
        if (!noTitleBar && !titleBarLate) // TEMP DIAGNOSTIC 46.7.37
        {
            SetTitleBar(titleBar);
        }

        foreach (var name in new[] { "Getting ready", "Updates & Health", "App Marketplace", "Installed", "About This PC" })
        {
            var button = new Button
            {
                Content = name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Theme.Sidebar,
                Foreground = Theme.Text,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 8, 12, 8),
                FontFamily = Chrome.Font,
            };
            var captured = name;
            button.Click += (_, _) => Show(captured);
            _navButtons[name] = button;
            _nav.Children.Add(button);
        }

        var sidebarContent = new Grid();
        sidebarContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebarContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_nav, 0);
        sidebarContent.Children.Add(_nav);
        Grid.SetRow(_accountFooter, 1);
        sidebarContent.Children.Add(_accountFooter);

        var sidebar = new Border
        {
            Width = 240,
            Background = Theme.Sidebar,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = sidebarContent,
        };

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        body.Children.Add(sidebar);
        Grid.SetColumn(_host, 1);
        body.Children.Add(_host);

        var root = new Grid { Background = Theme.Canvas };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(titleBar);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Content = root;
        if (!noTitleBar && titleBarLate) // TEMP DIAGNOSTIC 46.7.37
        {
            SetTitleBar(titleBar);
        }

        // TEMP DIAGNOSTIC 46.7.49: the gallery replaces Getting ready; demoqueue adds 3 demo actions.
        Show(DiagSwitches.Has("gallery") ? "Parts gallery" : "Getting ready"); // TEMP DIAGNOSTIC 46.7.49
        if (DiagSwitches.Has("demoqueue")) // TEMP DIAGNOSTIC 46.7.49
        {
            PartsGalleryPage.AddDemoActions(_services.Queue); // TEMP DIAGNOSTIC 46.7.49
        }

        RefreshAccountFooter();
    }

    private void ShowSignIn()
    {
        var returnTo = string.IsNullOrEmpty(_current) ? "Getting ready" : _current;
        _current = "Sign in";
        foreach (var pair in _navButtons)
        {
            pair.Value.Background = Theme.Sidebar;
        }

        _host.Children.Clear();
        var page = new SignInPage(_services);
        page.SignedIn += () =>
        {
            RefreshAccountFooter();
            Show(returnTo);
        };
        page.Dismissed += () => Show(returnTo);
        _host.Children.Add(page);
    }

    private void RefreshAccountFooter()
    {
        _accountFooter.Children.Clear();
        if (_services.Session.IsSignedIn)
        {
            _accountFooter.Children.Add(new TextBlock
            {
                Text = _services.Session.Email,
                Foreground = Theme.Text,
                FontFamily = Chrome.Font,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var signOut = new Button
            {
                Content = "Sign out",
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Theme.Sidebar,
                Foreground = Theme.Accent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                FontFamily = Chrome.Font,
            };
            signOut.Click += (_, _) =>
            {
                _services.Session.Clear();
                RefreshAccountFooter();
            };
            _accountFooter.Children.Add(signOut);
            return;
        }

        _accountFooter.Children.Add(new TextBlock
        {
            Text = "Not signed in",
            Foreground = Theme.Muted,
            FontFamily = Chrome.Font,
            FontSize = 12,
        });
        var signIn = new Button
        {
            Content = "Sign in",
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Theme.Sidebar,
            Foreground = Theme.Accent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            FontFamily = Chrome.Font,
        };
        signIn.Click += (_, _) => ShowSignIn();
        _accountFooter.Children.Add(signIn);
    }

    private void Show(string name)
    {
        CrashLog.Write("Mark:Show " + name, null); // TEMP DIAGNOSTIC
        _nav.Visibility = Visibility.Visible;
        _current = name;
        foreach (var pair in _navButtons)
        {
            pair.Value.Background = pair.Key == name ? Theme.Accent : Theme.Sidebar;
        }

        _host.Children.Clear();
        UIElement page = name switch
        {
            "Updates & Health" => new UpdatesPage(_services),
            "App Marketplace" => new MarketplacePage(_services),
            "Installed" => new InstalledPage(_services),
            "About This PC" => new AboutPage(_services),
            "Parts gallery" => new PartsGalleryPage(_services), // TEMP DIAGNOSTIC 46.7.49
            _ => ReadyPage(),
        };
        _host.Children.Add(page);
        CrashLog.Write("Mark:Shown " + name, null); // TEMP DIAGNOSTIC
    }

    private GettingReadyPage ReadyPage()
    {
        var page = new GettingReadyPage(_services);
        page.Settled += () =>
        {
            if (_readyAdvanced || _current != "Getting ready")
            {
                return;
            }

            _readyAdvanced = true;
            Show("Updates & Health");
        };
        return page;
    }
}
