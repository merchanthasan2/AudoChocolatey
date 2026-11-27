using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Services;
using QuantumUpdater.Views;
using QuantumUpdater.Views.Controls;
using Windows.System;

namespace QuantumUpdater;

/// <summary>
/// DeskVerve main window shell (46.7.9, D1, D2, D11).
/// 40 px title bar, 240 px sidebar with icons, 3 px active indicator, update badge,
/// sidebar lock during initial Getting ready scan, persistent ActivityBar, and close guard.
/// </summary>
public sealed class MainWindow : Window
{
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly SolidColorBrush LockedTextBrush = new(Windows.UI.Color.FromArgb(255, 0x6E, 0x6E, 0x6E));

    private readonly AppServices _services;
    private readonly StackPanel _nav = new() { Spacing = 2, Margin = new Thickness(8) };
    private readonly Grid _host = new() { Padding = new Thickness(32, 24, 32, 24) };
    private readonly ActivityBar _activityBar;
    private readonly Dictionary<string, NavEntry> _navItems = new(StringComparer.Ordinal);
    private readonly StackPanel _accountFooter = new() { Spacing = 4, Padding = new Thickness(12) };
    private readonly TextBlock _lockHint;

    private string _current = "";
    private UIElement? _currentPage;
    private bool _sidebarLocked = true;
    private bool _readyAdvanced;
    private bool _signInPending;
    private bool _allowClose;

    private sealed record NavEntry(Button Button, Border AccentMark, TextBlock Label, Border? BadgeHost, TextBlock? BadgeText, FontIcon LockIcon);

    public MainWindow()
    {
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _services = new AppServices(a => dispatcher.TryEnqueue(() => a()));
        Title = "DeskVerve";

        ExtendsContentIntoTitleBar = true;

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
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        titleBar.Children.Add(titleContent);

        // Sidebar nav items (Getting ready is not in the sidebar per 46.7.9)
        var defs = new (string Name, string Glyph, bool HasBadge)[]
        {
            ("Updates & Health", "\uE895", true),
            ("App Marketplace", "\uE719", false),
            ("Installed", "\uE71D", false),
            ("Disk & Storage", "\uEDA2", false),
            ("System Cleanup", "\uE74D", false),
            ("About This PC", "\uE7F4", false),
        };

        foreach (var (name, glyph, hasBadge) in defs)
        {
            var entry = CreateNavItem(name, glyph, hasBadge);
            _navItems[name] = entry;
            _nav.Children.Add(entry.Button);
        }

        _lockHint = new TextBlock
        {
            Text = "The menu opens when the scan is done.",
            Foreground = Theme.TextSecondary,
            FontFamily = Chrome.Font,
            FontSize = Theme.CaptionSize,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 8, 12, 0),
        };
        _nav.Children.Add(_lockHint);

        var footerWrap = new Border
        {
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _accountFooter,
        };

        var sidebarContent = new Grid();
        sidebarContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebarContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_nav, 0);
        sidebarContent.Children.Add(_nav);
        Grid.SetRow(footerWrap, 1);
        sidebarContent.Children.Add(footerWrap);

        var sidebar = new Border
        {
            Width = 240,
            Background = Theme.Sidebar,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = sidebarContent,
        };

        // Main content column: page host + persistent ActivityBar at bottom
        _activityBar = new ActivityBar(_services.Queue);
        var mainColumn = new Grid();
        mainColumn.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_host, 0);
        mainColumn.Children.Add(_host);
        Grid.SetRow(_activityBar, 1);
        mainColumn.Children.Add(_activityBar);

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        body.Children.Add(sidebar);
        Grid.SetColumn(mainColumn, 1);
        body.Children.Add(mainColumn);

        var root = new Grid { Background = Theme.Canvas };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(titleBar);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        root.KeyDown += OnRootKeyDown;
        Content = root;

        // D2: SetTitleBar only after Content is set and titleBar is in the visual tree.
        SetTitleBar(titleBar);

        // Hold bar tracking (W18)
        _services.Queue.Changed += _ => _activityBar.SetHold(_services.Queue.IsHeld, _services.Queue.HeldBy?.Message);

        // Update badge tracking (46.7.82)
        _services.Updates.Changed += RefreshUpdateBadge;
        RefreshUpdateBadge();

        // D11: warn when closing while an action is Working
        AppWindow.Closing += OnWindowClosing;

        SetSidebarLocked(true);
        Show("Getting ready");
        RefreshAccountFooter();

        _services.Account.SessionRejected += () => dispatcher.TryEnqueue(OnSessionRejected);
        _ = CheckSessionAtStartAsync();
    }

    private NavEntry CreateNavItem(string name, string glyph, bool hasBadge)
    {
        var accentMark = new Border
        {
            Width = 3,
            Height = 18,
            CornerRadius = new CornerRadius(2),
            Background = Theme.Accent,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-12, 0, 9, 0),
        };

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontFamily = IconFont,
            FontSize = 16,
            Foreground = Theme.Text,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var label = new TextBlock
        {
            Text = name,
            Foreground = Theme.Text,
            FontFamily = Chrome.Font,
            FontSize = Theme.BodySize,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Border? badgeHost = null;
        TextBlock? badgeText = null;
        if (hasBadge)
        {
            badgeText = new TextBlock
            {
                Text = "0",
                Foreground = Theme.Text,
                FontFamily = Chrome.Font,
                FontSize = Theme.CaptionSize,
                FontWeight = FontWeights.SemiBold,
            };
            badgeHost = new Border
            {
                Background = Theme.Accent,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 1, 7, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Child = badgeText,
            };
        }

        var lockIcon = new FontIcon
        {
            Glyph = "\uE72E",
            FontFamily = IconFont,
            FontSize = 12,
            Foreground = Theme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Visible,
        };

        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // accent mark
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // icon
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // label
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // badge / lock

        Grid.SetColumn(accentMark, 0);
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(label, 2);
        row.Children.Add(accentMark);
        row.Children.Add(icon);
        row.Children.Add(label);

        var rightSlot = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (badgeHost is not null) rightSlot.Children.Add(badgeHost);
        rightSlot.Children.Add(lockIcon);
        Grid.SetColumn(rightSlot, 3);
        row.Children.Add(rightSlot);

        var button = new Button
        {
            Content = row,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Theme.Sidebar,
            BorderThickness = new Thickness(0),
            CornerRadius = Theme.RadiusSmall,
            Padding = new Thickness(12, 0, 12, 0),
        };
        var captured = name;
        button.Click += (_, _) =>
        {
            if (!_sidebarLocked)
            {
                Show(captured);
            }
        };

        return new NavEntry(button, accentMark, label, badgeHost, badgeText, lockIcon);
    }

    private void SetSidebarLocked(bool locked)
    {
        _sidebarLocked = locked;
        _lockHint.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        foreach (var entry in _navItems.Values)
        {
            entry.Button.IsEnabled = !locked;
            entry.Label.Foreground = locked ? LockedTextBrush : Theme.Text;
            entry.LockIcon.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        }
        RefreshUpdateBadge();
    }

    private void RefreshUpdateBadge()
    {
        if (!_navItems.TryGetValue("Updates & Health", out var entry) || entry.BadgeHost is null || entry.BadgeText is null)
        {
            return;
        }

        int count = _services.Updates.PendingCount;
        if (_sidebarLocked || count <= 0)
        {
            entry.BadgeHost.Visibility = Visibility.Collapsed;
            return;
        }

        entry.BadgeText.Text = count.ToString();
        entry.BadgeHost.Visibility = Visibility.Visible;
    }

    private async Task CheckSessionAtStartAsync()
    {
        try
        {
            await _services.Account.CheckSessionAsync(_services.Session).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Session check", ex);
        }
    }

    private void OnSessionRejected()
    {
        RefreshAccountFooter();
        if (_current == "Sign in")
        {
            return;
        }

        if (_current == "Getting ready" && !_readyAdvanced)
        {
            _signInPending = true;
            return;
        }

        ShowSignIn();
    }

    private void ShowSignIn()
    {
        var returnTo = string.IsNullOrEmpty(_current) || _current == "Getting ready"
            ? "Updates & Health"
            : _current;
        _current = "Sign in";
        UpdateNavSelection("");

        _host.Children.Clear();
        var page = new SignInPage(_services);
        page.SignedIn += () =>
        {
            RefreshAccountFooter();
            Show(returnTo);
        };
        page.Dismissed += () => Show(returnTo);
        _currentPage = page;
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
                FontSize = Theme.CaptionSize,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var signOut = Chrome.GhostButton("Sign out");
            signOut.Padding = new Thickness(0, 2, 0, 2);
            signOut.HorizontalAlignment = HorizontalAlignment.Left;
            signOut.Click += (_, _) =>
            {
                _ = _services.Account.SignOutAsync(_services.Session);
                RefreshAccountFooter();
            };
            _accountFooter.Children.Add(signOut);
            return;
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var label = Chrome.Caption("Not signed in ·");
        label.VerticalAlignment = VerticalAlignment.Center;
        var signIn = Chrome.GhostButton("Sign in");
        signIn.Padding = new Thickness(2, 0, 2, 0);
        signIn.FontSize = Theme.CaptionSize;
        signIn.Click += (_, _) =>
        {
            if (!_sidebarLocked)
            {
                ShowSignIn();
            }
        };
        row.Children.Add(label);
        row.Children.Add(signIn);
        _accountFooter.Children.Add(row);
    }

    private void Show(string name)
    {
        _current = name;
        UpdateNavSelection(name);

        _host.Children.Clear();
        UIElement page = name switch
        {
            "Updates & Health" => new UpdatesPage(_services),
            "App Marketplace" => new MarketplacePage(_services),
            "Installed" => new InstalledPage(_services),
            "Disk & Storage" => new DiskPage(_services),
            "System Cleanup" => new CleanupPage(_services),
            "About This PC" => new AboutPage(_services),
            _ => ReadyPage(),
        };
        _currentPage = page;
        _host.Children.Add(page);
    }

    private void UpdateNavSelection(string activeName)
    {
        foreach (var pair in _navItems)
        {
            bool active = string.Equals(pair.Key, activeName, StringComparison.Ordinal);
            pair.Value.Button.Background = active ? Theme.CardHover : Theme.Sidebar;
            pair.Value.AccentMark.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private GettingReadyPage ReadyPage()
    {
        var page = new GettingReadyPage(_services);
        page.Settled += async allOk =>
        {
            if (_readyAdvanced || _current != "Getting ready")
            {
                return;
            }

            SetSidebarLocked(false);
            if (!allOk)
            {
                // Keep the Getting ready screen visible with "Continue" so the user sees any failed job,
                // unless they click Continue (which invokes Settled(true)).
                return;
            }

            await Task.Delay(700).ConfigureAwait(true);
            if (_readyAdvanced || _current != "Getting ready")
            {
                return;
            }

            _readyAdvanced = true;
            Show("Updates & Health");
            if (_signInPending)
            {
                _signInPending = false;
                ShowSignIn();
            }
        };
        return page;
    }

    private async void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        bool working = _services.Queue.CurrentBatch.Any(i => i.State == ActionState.Working);
        if (!working)
        {
            return;
        }

        args.Cancel = true;
        var closeAnyway = await ConfirmDialog.ShowAsync(_host.XamlRoot, new ConfirmRequest(
            Title: "An update is running. Close DeskVerve anyway?",
            Body: "Closing DeskVerve now will stop the active installation or update.",
            Apps: null,
            Note: "Recommended: keep DeskVerve open until the active task finishes.",
            MainButton: "Close anyway",
            Danger: true)).ConfigureAwait(true);

        if (closeAnyway)
        {
            _allowClose = true;
            Close();
        }
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        bool hasCtrl = ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (hasCtrl && e.Key == VirtualKey.F)
        {
            switch (_currentPage)
            {
                case UpdatesPage u: u.FocusSearch(); break;
                case InstalledPage i: i.FocusSearch(); break;
                case MarketplacePage m: m.FocusSearch(); break;
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.F5)
        {
            switch (_currentPage)
            {
                case UpdatesPage u: _ = u.RefreshAsync(); break;
                case InstalledPage i: _ = i.RefreshAsync(); break;
                case MarketplacePage m: _ = m.RefreshAsync(); break;
            }
            e.Handled = true;
        }
    }
}
