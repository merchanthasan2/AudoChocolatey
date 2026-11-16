using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using QuantumUpdater.Services;
using QuantumUpdater.Views;

namespace QuantumUpdater;

public sealed class MainWindow : Window
{
    private readonly AppServices _services = new();
    private readonly StackPanel _nav = new() { Spacing = 4, Margin = new Thickness(8) };
    private readonly Grid _host = new() { Margin = new Thickness(24) };
    private readonly Dictionary<string, Button> _navButtons = new();
    private string _current = "";
    private bool _readyAdvanced;
    private readonly StackPanel _accountFooter = new() { Spacing = 4, Margin = new Thickness(16, 8, 16, 16) };

    public MainWindow()
    {
        Title = "DeskVerve";
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();

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
        var titleLogo = BrandAssets.Emblem(24);
        AutomationProperties.SetAccessibilityView(titleLogo, AccessibilityView.Raw);
        titleContent.Children.Add(titleLogo);
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
        SetTitleBar(titleBar);

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

        Show("Getting ready");
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
            _ => ReadyPage(),
        };
        _host.Children.Add(page);
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
