using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Models;
using QuantumUpdater.Services;
using QuantumUpdater.Views.Controls;
using QuantumUpdater.Views.Updates;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace QuantumUpdater.Views;

/// <summary>
/// Redesigned Updates &amp; Health page matching mockups 01-updates.html and 05-updates-states.html.
/// Uses UpdatesCatalog, UpdateActions, VirtualList, AppRow, ConfirmDialog, and real IconService icons.
/// </summary>
public sealed class UpdatesPage : Grid
{
    private readonly AppServices _services;
    private readonly TextBlock _checkedCaption;
    private readonly ProgressRing _checkingSpinner;
    private readonly TextBlock _checkingText;
    private readonly Button _checkAgain;
    private readonly Button _updateAll;
    private readonly TextBlock _tileReadyNum;
    private readonly TextBlock _tileReadySub;
    private readonly TextBlock _tileHealthyNum;
    private readonly TextBlock _tileHealthySub;
    private readonly TextBlock _tileTodayNum;
    private readonly TextBlock _tileTodaySub;
    private readonly CheckBox _selectAll;
    private readonly TextBlock _selectedCountText;
    private readonly Button _updateSelected;
    private readonly AutoSuggestBox _filterBox;
    private readonly ComboBox _sortBox;
    private readonly Border _stateBanner;
    private readonly TextBlock _stateBannerText;
    private readonly VirtualList _list;

    private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedKeys = new(StringComparer.OrdinalIgnoreCase);
    private UpdatesSort _sort = UpdatesSort.Name;
    private UpdatesViewResult? _latestView;

    public UpdatesPage(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bento tiles
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Banner / Store note
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // VirtualList

        // 1. Header
        var headerGrid = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 0, 0, 18) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _checkedCaption = Chrome.Caption("Last checked just now");
        _checkingSpinner = new ProgressRing
        {
            Width = 12,
            Height = 12,
            MinWidth = 0,
            MinHeight = 0,
            IsActive = false,
            Foreground = Theme.Accent,
            Visibility = Visibility.Collapsed,
        };
        _checkingText = Chrome.Caption("Checking for new updates");
        _checkingText.Visibility = Visibility.Collapsed;

        var subRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        subRow.Children.Add(_checkedCaption);
        subRow.Children.Add(_checkingSpinner);
        subRow.Children.Add(_checkingText);

        var leftHead = new StackPanel { Spacing = 4 };
        leftHead.Children.Add(Chrome.Title("Updates & Health"));
        leftHead.Children.Add(subRow);
        Grid.SetColumn(leftHead, 0);
        headerGrid.Children.Add(leftHead);

        _checkAgain = Chrome.SecondaryButton("Check again");
        _checkAgain.Click += async (_, _) => await _services.Updates.RefreshAsync().ConfigureAwait(true);

        _updateAll = Chrome.Action("Update all (0)");
        _updateAll.FontWeight = FontWeights.SemiBold;
        _updateAll.Visibility = Visibility.Collapsed;
        _updateAll.Click += async (_, _) => await HandleUpdateAllAsync().ConfigureAwait(true);

        var headActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        headActions.Children.Add(_checkAgain);
        headActions.Children.Add(_updateAll);
        Grid.SetColumn(headActions, 1);
        headerGrid.Children.Add(headActions);
        Grid.SetRow(headerGrid, 0);
        Children.Add(headerGrid);

        // 2. Three Bento Summary Tiles
        var tiles = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 16) };
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var t1 = BuildTile(Theme.Warning, "Updates ready", out _tileReadyNum, out _tileReadySub);
        var t2 = BuildTile(Theme.Healthy, "Apps up to date", out _tileHealthyNum, out _tileHealthySub);
        var t3 = BuildTile(Theme.Accent, "Updated today", out _tileTodayNum, out _tileTodaySub);
        Grid.SetColumn(t1, 0);
        Grid.SetColumn(t2, 1);
        Grid.SetColumn(t3, 2);
        tiles.Children.Add(t1);
        tiles.Children.Add(t2);
        tiles.Children.Add(t3);
        Grid.SetRow(tiles, 1);
        Children.Add(tiles);

        // 3. Toolbar
        _selectAll = new CheckBox
        {
            Content = "Select all",
            Foreground = Theme.Text,
            FontFamily = Chrome.Font,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 0,
        };
        _selectAll.Click += (_, _) => ToggleSelectAll();

        _selectedCountText = Chrome.Caption("0 selected");
        _selectedCountText.VerticalAlignment = VerticalAlignment.Center;

        _updateSelected = Chrome.SecondaryButton("Update selected (0)");
        _updateSelected.Visibility = Visibility.Collapsed;
        _updateSelected.Click += async (_, _) => await HandleUpdateSelectedAsync().ConfigureAwait(true);

        _filterBox = Chrome.SearchBox("Filter updates");
        _filterBox.TextChanged += (_, _) => RebuildList();

        _sortBox = new ComboBox
        {
            Background = Theme.Card,
            Foreground = Theme.Muted,
            BorderBrush = Theme.Stroke,
            SelectedIndex = 0,
            MinWidth = 140,
        };
        _sortBox.Items.Add("Sort: Name");
        _sortBox.Items.Add("Sort: Source");
        _sortBox.SelectionChanged += (_, _) =>
        {
            _sort = _sortBox.SelectedIndex == 1 ? UpdatesSort.Source : UpdatesSort.Name;
            RebuildList();
        };

        var toolbarGrid = new Grid { ColumnSpacing = 12 };
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(_selectAll, 0);
        Grid.SetColumn(_selectedCountText, 1);
        Grid.SetColumn(_updateSelected, 2);
        Grid.SetColumn(_filterBox, 4);
        Grid.SetColumn(_sortBox, 5);
        toolbarGrid.Children.Add(_selectAll);
        toolbarGrid.Children.Add(_selectedCountText);
        toolbarGrid.Children.Add(_updateSelected);
        toolbarGrid.Children.Add(_filterBox);
        toolbarGrid.Children.Add(_sortBox);

        var toolbarCard = new Border
        {
            Background = Theme.Subtle,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Child = toolbarGrid,
        };
        Grid.SetRow(toolbarCard, 2);
        Children.Add(toolbarCard);

        // 4. State banner + Microsoft Store strip (D5)
        _stateBannerText = Chrome.Line("", Theme.Muted);
        var storeBtn = Chrome.GhostButton("Open Microsoft Store");
        storeBtn.Click += async (_, _) => await Launcher.LaunchUriAsync(new Uri("ms-windows-store://downloadsAndUpdates"));

        var bannerGrid = new Grid { ColumnSpacing = 12 };
        bannerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bannerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_stateBannerText, 0);
        Grid.SetColumn(storeBtn, 1);
        bannerGrid.Children.Add(_stateBannerText);
        bannerGrid.Children.Add(storeBtn);

        _stateBanner = new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = bannerGrid,
        };
        Grid.SetRow(_stateBanner, 3);
        Children.Add(_stateBanner);

        // 5. VirtualList
        _list = new VirtualList();
        _list.GroupToggled += groupId =>
        {
            if (!_collapsedGroups.Add(groupId))
            {
                _collapsedGroups.Remove(groupId);
            }
            RebuildList();
        };
        _list.RowAction += async (key, action) => await OnRowActionAsync(key, action).ConfigureAwait(true);
        _list.RowPrepared += BindRowIcon;

        var listFrame = new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Child = _list,
        };
        Grid.SetRow(listFrame, 4);
        Children.Add(listFrame);

        Loaded += (_, _) =>
        {
            _services.Updates.Changed += OnCatalogOrQueueChanged;
            _services.Queue.Changed += OnQueueItemChanged;
            _services.InstalledApps.Changed += OnCatalogOrQueueChanged;
            RebuildList();
        };
        Unloaded += (_, _) =>
        {
            _services.Updates.Changed -= OnCatalogOrQueueChanged;
            _services.Queue.Changed -= OnQueueItemChanged;
            _services.InstalledApps.Changed -= OnCatalogOrQueueChanged;
        };
    }

    public void FocusSearch() => _filterBox.Focus(FocusState.Programmatic);

    public Task RefreshAsync() => _services.Updates.RefreshAsync();

    private void OnCatalogOrQueueChanged() => RebuildList();

    private void OnQueueItemChanged(QueuedAction _) => RebuildList();

    private void RebuildList()
    {
        var entries = _services.Updates.Entries;
        var view = UpdatesListModel.Build(
            entries,
            _services.Queue.CurrentBatch,
            _services.Queue.IsHeld,
            _filterBox.Text,
            _sort,
            _collapsedGroups,
            _selectedKeys);
        _latestView = view;

        // Header status
        bool checking = _services.Updates.IsChecking;
        _checkAgain.IsEnabled = !checking;
        _checkingSpinner.IsActive = checking;
        _checkingSpinner.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
        _checkingText.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;

        _checkedCaption.Text = _services.Updates.LastChecked is DateTimeOffset dt
            ? $"Last checked {dt.ToLocalTime():HH:mm}"
            : "Not checked yet";

        _updateAll.Content = view.UpdateAllLabel;
        _updateAll.Visibility = view.TotalActionableCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Bento tiles
        int activeCount = _services.Queue.CurrentBatch.Count(i => i.State == ActionState.Working);
        int waitCount = _services.Queue.CurrentBatch.Count(i => i.State == ActionState.Waiting);
        _tileReadyNum.Text = view.TotalActionableCount.ToString();
        _tileReadySub.Text = activeCount > 0
            ? $"{activeCount} updating now, {waitCount} waiting"
            : view.TotalActionableCount == 0 ? "Everything is current" : "Ready to install";

        int totalInstalled = _services.InstalledApps.Apps.Count;
        int upToDate = Math.Max(0, totalInstalled - view.TotalActionableCount);
        _tileHealthyNum.Text = upToDate.ToString();
        _tileHealthySub.Text = totalInstalled > 0 ? $"Of {totalInstalled} apps on this PC" : "Healthy apps";

        _tileTodayNum.Text = view.UpdatedTodayCount.ToString();
        _tileTodaySub.Text = view.UpdatedTodayCount == 0
            ? "No updates run today"
            : string.Join(", ", entries.Where(e => e.Group == UpgradeGroup.UpdatedToday).Take(2).Select(e => e.Name));

        // Toolbar
        _selectedCountText.Text = $"{view.SelectedCount} selected";
        _updateSelected.Content = view.UpdateSelectedLabel;
        _updateSelected.Visibility = view.SelectedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        _selectAll.IsChecked = view.TotalActionableCount > 0 && view.SelectedCount == view.TotalActionableCount;

        // Banner
        if (!string.IsNullOrEmpty(_services.Updates.Error))
        {
            _stateBannerText.Text = _services.Updates.Error + (_services.Updates.SavedAt is DateTimeOffset s
                ? $" (Showing saved scan from {s.ToLocalTime():HH:mm})"
                : "");
            _stateBannerText.Foreground = Theme.CriticalText;
        }
        else if (view.TotalActionableCount == 0 && view.UpdatedTodayCount == 0)
        {
            _stateBannerText.Text = "All catalog apps are up to date. Microsoft Store apps update in the Microsoft Store.";
            _stateBannerText.Foreground = Theme.HealthyText;
        }
        else
        {
            _stateBannerText.Text = "Microsoft Store apps get their updates from the Microsoft Store.";
            _stateBannerText.Foreground = Theme.TextSecondary;
        }

        _list.SetItems(view.Items);
    }

    private void ToggleSelectAll()
    {
        if (_latestView is null) return;
        bool check = _selectAll.IsChecked == true;
        _selectedKeys.Clear();
        if (check)
        {
            foreach (var entry in _latestView.AllActionable)
            {
                _selectedKeys.Add(entry.CatalogId);
            }
        }

        RebuildList();
    }

    private async Task HandleUpdateAllAsync()
    {
        if (_latestView is null || _latestView.AllActionable.Count == 0) return;

        var list = _latestView.AllActionable;
        if (list.Count == 1)
        {
            _services.UpdateActions.Update(list[0]);
            return;
        }

        var ok = await ConfirmDialog.ShowAsync(XamlRoot, new ConfirmRequest(
            Title: $"Update {list.Count} apps?",
            Body: "DeskVerve updates these apps one at a time. You can keep using DeskVerve while they update.",
            Apps: list.Select(e => new ConfirmApp(e.Name, e.AvailableVersion)).ToList(),
            Note: "Some apps close while they update. Save your work in these apps first.",
            MainButton: "Update all",
            Danger: false)).ConfigureAwait(true);

        if (ok)
        {
            _services.UpdateActions.UpdateMany(list);
        }
    }

    private async Task HandleUpdateSelectedAsync()
    {
        if (_latestView is null || _latestView.SelectedEntries.Count == 0) return;
        var list = _latestView.SelectedEntries;
        if (list.Count == 1)
        {
            _services.UpdateActions.Update(list[0]);
            _selectedKeys.Clear();
            return;
        }

        var ok = await ConfirmDialog.ShowAsync(XamlRoot, new ConfirmRequest(
            Title: $"Update {list.Count} selected apps?",
            Body: "DeskVerve updates these apps sequentially to prevent Windows Installer conflicts.",
            Apps: list.Select(e => new ConfirmApp(e.Name, e.AvailableVersion)).ToList(),
            Note: "Some apps close while they update. Save your work first.",
            MainButton: "Update selected",
            Danger: false)).ConfigureAwait(true);

        if (ok)
        {
            _services.UpdateActions.UpdateMany(list);
            _selectedKeys.Clear();
        }
    }

    private async Task OnRowActionAsync(string key, RowAction action)
    {
        switch (action)
        {
            case RowAction.Select:
                if (!_selectedKeys.Add(key))
                {
                    _selectedKeys.Remove(key);
                }
                RebuildList();
                break;

            case RowAction.Update:
            case RowAction.Retry:
                var entry = _services.Updates.Entries.FirstOrDefault(e =>
                    string.Equals(e.CatalogId, key, StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    _services.UpdateActions.Update(entry);
                }
                break;

            case RowAction.Cancel:
                var item = _services.Queue.CurrentBatch.FirstOrDefault(i =>
                    string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)
                    && i.State is ActionState.Waiting or ActionState.Working);
                if (item is not null)
                {
                    _services.Queue.Cancel(item.Id);
                }
                break;

            case RowAction.Details:
                await ShowFailureDetailsAsync(key).ConfigureAwait(true);
                break;
        }
    }

    private async Task ShowFailureDetailsAsync(string key)
    {
        var item = _services.Queue.CurrentBatch.FirstOrDefault(i =>
            string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
        var msg = item?.Message ?? "The update failed.";
        var ok = await ConfirmDialog.ShowAsync(XamlRoot, new ConfirmRequest(
            Title: $"Update details · {item?.Title ?? key}",
            Body: msg,
            Apps: null,
            Note: $"Package ID: {key}",
            MainButton: "Copy details",
            Danger: false,
            TileName: item?.Title ?? key)).ConfigureAwait(true);

        if (ok)
        {
            var dp = new DataPackage();
            dp.SetText($"App: {item?.Title ?? key}\nPackage ID: {key}\nError: {msg}");
            Clipboard.SetContent(dp);
        }
    }

    private void BindRowIcon(AppRow row, AppRowModel model)
    {
        var installed = _services.InstalledApps.FindByCatalogId(model.Key);
        string iconKey = installed?.Key ?? ("catalog:" + model.Key);
        int ticket = row.Icon.CurrentTicket;

        var cached = _services.Icons.TryGetCachedPath(iconKey)
            ?? _services.Icons.TryGetCachedPath("catalog:" + model.Key);
        if (cached is not null)
        {
            var img = IconService.LoadImageSource(cached, 32);
            if (img is not null)
            {
                row.Icon.SetImage(ticket, img);
                return;
            }
        }

        _ = Task.Run(async () =>
        {
            string? resolved = installed is not null
                ? await _services.Icons.ResolveInstalledAsync(installed, CancellationToken.None).ConfigureAwait(false)
                : await _services.Icons.ResolveCatalogAsync(model.Key, CancellationToken.None).ConfigureAwait(false);
            if (resolved is null) return;

            _services.Post(() =>
            {
                var img = IconService.LoadImageSource(resolved, 32);
                if (img is not null)
                {
                    row.Icon.SetImage(ticket, img);
                }
            });
        });
    }

    private static Border BuildTile(
        SolidColorBrush dotBrush,
        string title,
        out TextBlock numberBlock,
        out TextBlock subBlock)
    {
        var dot = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = dotBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };
        numberBlock = new TextBlock
        {
            Text = "0",
            FontFamily = Chrome.Font,
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var titleBlock = new TextBlock
        {
            Text = title,
            FontFamily = Chrome.Font,
            FontSize = Theme.BodySize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        };
        subBlock = Chrome.Caption("");
        subBlock.TextTrimming = TextTrimming.CharacterEllipsis;

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(titleBlock);
        right.Children.Add(subBlock);

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(dot, 0);
        Grid.SetColumn(numberBlock, 1);
        Grid.SetColumn(right, 2);
        grid.Children.Add(dot);
        grid.Children.Add(numberBlock);
        grid.Children.Add(right);

        return new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Padding = new Thickness(16, 14, 16, 14),
            Child = grid,
        };
    }
}
