using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Catalog;
using QuantumUpdater.Views.Controls;
using Windows.System;

namespace QuantumUpdater.Views;

/// <summary>
/// Redesigned App Marketplace (46.6, ADR 050, mockups 03-marketplace-home.html &amp; 04-marketplace-search.html).
/// Opens immediately with curated popular picks + 10 categories, and searches the full
/// Microsoft catalog (winget + msstore) with 250ms debounce, "See all" category browsing,
/// real icons, 1-click queued installs, and a live DetailsPanel.
/// </summary>
public sealed class MarketplacePage : Grid
{
    private readonly AppServices _services;
    private readonly AutoSuggestBox _searchBox;
    private readonly Button _clearSearch;
    private readonly StackPanel _categoryPills;
    private readonly ScrollViewer _homeScroll;
    private readonly StackPanel _homeBody;
    private readonly Grid _resultsView;
    private readonly TextBlock _resultsCount;
    private readonly ProgressRing _searchSpinner;
    private readonly ComboBox _sortBox;
    private readonly StackPanel _resultsList;
    private readonly DetailsPanel _detailsPanel;

    private string _activeCategory = "all";
    private string _activeQuery = string.Empty;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _debounceCts;
    private IReadOnlyList<MarketplaceItem> _currentResults = Array.Empty<MarketplaceItem>();
    private MarketplaceItem? _selectedItem;

    public MarketplacePage(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header + search + pills
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Home or Split Results

        // Top section
        var top = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 0, 14) };
        var headStack = new StackPanel { Spacing = 4 };
        headStack.Children.Add(Chrome.Title("App Marketplace"));
        headStack.Children.Add(Chrome.Caption("Curated essentials (including NetSpeedBoss via Scoop) plus full Winget, Store, Scoop, and Chocolatey search."));
        top.Children.Add(headStack);

        // Big Search Bar
        _searchBox = new AutoSuggestBox
        {
            PlaceholderText = "Search all Winget, Store, Scoop & Chocolatey apps (e.g. netspeedboss, cursor, ollama, vlc)…",
            QueryIcon = new SymbolIcon(Symbol.Find),
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Theme.Card,
            BorderBrush = Theme.Accent,
            BorderThickness = new Thickness(1, 1, 1, 2),
            CornerRadius = new CornerRadius(6),
            FontFamily = Chrome.Font,
            FontSize = 15,
        };
        _searchBox.TextChanged += (_, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                OnSearchTextChanged(_searchBox.Text);
            }
        };
        _searchBox.QuerySubmitted += (_, args) => { var _unused = RunSearchAsync(args.QueryText); };
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape)
            {
                ResetToHome();
            }
        };

        _clearSearch = Chrome.SecondaryButton("Back to Home");
        _clearSearch.Visibility = Visibility.Collapsed;
        _clearSearch.Click += (_, _) => ResetToHome();

        var searchRow = new Grid { ColumnSpacing = 10 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_searchBox, 0);
        Grid.SetColumn(_clearSearch, 1);
        searchRow.Children.Add(_searchBox);
        searchRow.Children.Add(_clearSearch);
        top.Children.Add(searchRow);

        // Category pills
        _categoryPills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var pillScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _categoryPills,
        };
        top.Children.Add(pillScroll);
        Grid.SetRow(top, 0);
        Children.Add(top);

        // Home View
        _homeBody = new StackPanel { Spacing = 22, Padding = new Thickness(0, 0, 8, 24) };
        _homeScroll = new ScrollViewer { Content = _homeBody };
        Grid.SetRow(_homeScroll, 1);
        Children.Add(_homeScroll);

        // Split Results View
        _resultsView = new Grid { RowSpacing = 10, Visibility = Visibility.Collapsed };
        _resultsView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _resultsView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var countBar = new Grid { ColumnSpacing = 10 };
        countBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        countBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        countBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        countBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _searchSpinner = new ProgressRing
        {
            Width = 14,
            Height = 14,
            MinWidth = 0,
            MinHeight = 0,
            IsActive = false,
            Foreground = Theme.Accent,
            Visibility = Visibility.Collapsed,
        };
        _resultsCount = new TextBlock
        {
            FontFamily = Chrome.Font,
            FontSize = Theme.BodySize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _sortBox = new ComboBox
        {
            Background = Theme.Card,
            Foreground = Theme.Muted,
            BorderBrush = Theme.Stroke,
            SelectedIndex = 0,
            MinWidth = 150,
        };
        _sortBox.Items.Add("Sort: Best match");
        _sortBox.Items.Add("Sort: Name (A–Z)");
        _sortBox.SelectionChanged += (_, _) => RenderResultsList();

        Grid.SetColumn(_searchSpinner, 0);
        Grid.SetColumn(_resultsCount, 1);
        Grid.SetColumn(_sortBox, 3);
        countBar.Children.Add(_searchSpinner);
        countBar.Children.Add(_resultsCount);
        countBar.Children.Add(_sortBox);
        Grid.SetRow(countBar, 0);
        _resultsView.Children.Add(countBar);

        var split = new Grid { ColumnSpacing = 16 };
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });

        _resultsList = new StackPanel { Spacing = 0 };
        var resultsScroll = new ScrollViewer
        {
            Content = _resultsList,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var resultsFrame = new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Child = resultsScroll,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetColumn(resultsFrame, 0);
        split.Children.Add(resultsFrame);

        _detailsPanel = new DetailsPanel();
        _detailsPanel.ActionClicked += item => _services.Catalog.EnqueueInstall(item);
        var detailsScroll = new ScrollViewer
        {
            Content = _detailsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetColumn(detailsScroll, 1);
        split.Children.Add(detailsScroll);

        Grid.SetRow(split, 1);
        _resultsView.Children.Add(split);
        Grid.SetRow(_resultsView, 1);
        Children.Add(_resultsView);

        Loaded += (_, _) =>
        {
            _services.Queue.Changed += OnQueueOrCatalogChanged;
            _services.InstalledApps.Changed += OnCatalogRefreshed;
            _services.Updates.Changed += OnCatalogRefreshed;
            RebuildCategoryPills();
            RenderHome();
        };
        Unloaded += (_, _) =>
        {
            _services.Queue.Changed -= OnQueueOrCatalogChanged;
            _services.InstalledApps.Changed -= OnCatalogRefreshed;
            _services.Updates.Changed -= OnCatalogRefreshed;
            _debounceCts?.Cancel();
            _searchCts?.Cancel();
        };
    }

    public void FocusSearch() => _searchBox.Focus(FocusState.Programmatic);

    public Task RefreshAsync()
    {
        if (!string.IsNullOrWhiteSpace(_activeQuery))
        {
            return RunSearchAsync(_activeQuery);
        }

        if (_activeCategory != "all")
        {
            return BrowseCategoryAsync(_activeCategory);
        }

        RenderHome();
        return Task.CompletedTask;
    }

    private void OnQueueOrCatalogChanged(QueuedAction _)
    {
        if (_homeScroll.Visibility == Visibility.Visible)
        {
            RenderHome();
        }
        else
        {
            RenderResultsList();
        }
    }

    private void OnCatalogRefreshed()
    {
        if (_homeScroll.Visibility == Visibility.Visible)
        {
            RenderHome();
        }
        else
        {
            _currentResults = _currentResults.Select(_services.Catalog.RefreshItemState).ToList();
            RenderResultsList();
        }
    }

    private void RebuildCategoryPills()
    {
        _categoryPills.Children.Clear();
        AddCategoryPill("all", "All");
        foreach (var cat in _services.Catalog.Categories)
        {
            AddCategoryPill(cat.Id, cat.Title);
        }
    }

    private void AddCategoryPill(string id, string title)
    {
        bool active = string.Equals(_activeCategory, id, StringComparison.OrdinalIgnoreCase);
        var btn = new Button
        {
            Content = title,
            Background = active ? Theme.AccentChip : Theme.Card,
            Foreground = active ? Theme.Text : Theme.Muted,
            BorderBrush = active ? Theme.Accent : Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 4, 12, 4),
            FontFamily = Chrome.Font,
            FontSize = 13,
        };
        btn.Click += (_, _) =>
        {
            _activeCategory = id;
            RebuildCategoryPills();
            if (id == "all" && string.IsNullOrWhiteSpace(_activeQuery))
            {
                ResetToHome();
            }
            else if (id == "all")
            {
                _ = RunSearchAsync(_activeQuery);
            }
            else
            {
                _ = BrowseCategoryAsync(id);
            }
        };
        _categoryPills.Children.Add(btn);
    }

    private void ResetToHome()
    {
        _debounceCts?.Cancel();
        _searchCts?.Cancel();
        _activeQuery = string.Empty;
        _activeCategory = "all";
        _searchBox.Text = string.Empty;
        _clearSearch.Visibility = Visibility.Collapsed;
        _resultsView.Visibility = Visibility.Collapsed;
        _homeScroll.Visibility = Visibility.Visible;
        RebuildCategoryPills();
        RenderHome();
    }

    private void RenderHome()
    {
        _homeBody.Children.Clear();

        var popular = _services.Catalog.GetPopularPicks();
        AddShelf("Popular apps", "Picked by DeskVerve", popular, seeAllCategoryId: null);

        foreach (var cat in _services.Catalog.Categories)
        {
            var items = _services.Catalog.GetCategoryPicks(cat.Id);
            if (items.Count > 0)
            {
                AddShelf(cat.Title, null, items.Take(8).ToList(), seeAllCategoryId: cat.Id);
            }
        }
    }

    private void AddShelf(string title, string? caption, IReadOnlyList<MarketplaceItem> items, string? seeAllCategoryId)
    {
        var head = new Grid { ColumnSpacing = 10 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var h2 = new TextBlock
        {
            Text = title,
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        };
        Grid.SetColumn(h2, 0);
        head.Children.Add(h2);

        if (!string.IsNullOrEmpty(caption))
        {
            var cap = Chrome.Caption(caption);
            cap.VerticalAlignment = VerticalAlignment.Bottom;
            cap.Margin = new Thickness(0, 0, 0, 3);
            Grid.SetColumn(cap, 1);
            head.Children.Add(cap);
        }

        if (!string.IsNullOrEmpty(seeAllCategoryId))
        {
            var seeAll = Chrome.GhostButton("See all in catalog");
            seeAll.Click += (_, _) =>
            {
                _activeCategory = seeAllCategoryId;
                RebuildCategoryPills();
                _ = BrowseCategoryAsync(seeAllCategoryId);
            };
            Grid.SetColumn(seeAll, 2);
            head.Children.Add(seeAll);
        }

        var shelf = new StackPanel { Spacing = 10 };
        shelf.Children.Add(head);

        // 4-column card grid
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (int c = 0; c < 4; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        int rows = (int)Math.Ceiling(items.Count / 4.0);
        for (int r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var card = new AppCard();
            var qItem = _services.Queue.CurrentBatch.FirstOrDefault(q =>
                (string.Equals(q.Key, $"{item.SourceKind}:{item.CatalogId}", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(q.Key, item.CatalogId, StringComparison.OrdinalIgnoreCase))
                && q.State is ActionState.Waiting or ActionState.Working);
            card.Apply(item, qItem);
            card.InstallRequested += clicked => _services.Catalog.EnqueueInstall(clicked);
            card.CancelRequested += id => _services.Queue.Cancel(id);
            card.Selected += clicked => OpenItemInDetails(clicked);

            BindIcon(card.Icon, item.CatalogId, item.Homepage);

            Grid.SetRow(card, i / 4);
            Grid.SetColumn(card, i % 4);
            grid.Children.Add(card);
        }

        shelf.Children.Add(grid);
        _homeBody.Children.Add(shelf);
    }

    private void OnSearchTextChanged(string text)
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();

        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            if (_activeCategory == "all")
            {
                ResetToHome();
            }
            else
            {
                _ = BrowseCategoryAsync(_activeCategory);
            }
            return;
        }

        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, cts.Token).ConfigureAwait(false);
                _services.Post(() => _ = RunSearchAsync(trimmed));
            }
            catch (OperationCanceledException)
            {
                // Newer keystroke superseded this timer.
            }
        });
    }

    private async Task RunSearchAsync(string query)
    {
        _activeQuery = (query ?? string.Empty).Trim();
        if (_activeQuery.Length == 0)
        {
            ResetToHome();
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        ShowResultsChrome($"Searching winget & Store for \"{_activeQuery}\"…", loading: true);

        try
        {
            var results = await _services.Catalog.SearchFullCatalogAsync(_activeQuery, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested) return;

            _currentResults = results;
            _resultsCount.Text = results.Count == 0
                ? $"No packages matched \"{_activeQuery}\""
                : $"{results.Count} packages matching \"{_activeQuery}\"";
            _searchSpinner.IsActive = false;
            _searchSpinner.Visibility = Visibility.Collapsed;

            RenderResultsList();
            if (results.Count > 0)
            {
                await SelectForDetailsAsync(results[0], cts.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by newer search.
        }
    }

    private async Task BrowseCategoryAsync(string categoryId)
    {
        _activeQuery = string.Empty;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        var cat = _services.Catalog.Categories.FirstOrDefault(c =>
            string.Equals(c.Id, categoryId, StringComparison.OrdinalIgnoreCase));
        string label = cat?.Title ?? categoryId;

        ShowResultsChrome($"Loading {label} from the Microsoft catalog…", loading: true);

        try
        {
            var results = await _services.Catalog.BrowseCategoryAsync(categoryId, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested) return;

            _currentResults = results;
            _resultsCount.Text = $"{results.Count} apps in {label}";
            _searchSpinner.IsActive = false;
            _searchSpinner.Visibility = Visibility.Collapsed;

            RenderResultsList();
            if (results.Count > 0)
            {
                await SelectForDetailsAsync(results[0], cts.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled.
        }
    }

    private void ShowResultsChrome(string countLabel, bool loading)
    {
        _homeScroll.Visibility = Visibility.Collapsed;
        _resultsView.Visibility = Visibility.Visible;
        _clearSearch.Visibility = Visibility.Visible;
        _resultsCount.Text = countLabel;
        _searchSpinner.IsActive = loading;
        _searchSpinner.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderResultsList()
    {
        _resultsList.Children.Clear();
        IEnumerable<MarketplaceItem> ordered = _currentResults;
        if (_sortBox.SelectedIndex == 1)
        {
            ordered = _currentResults.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase);
        }

        var list = ordered.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                _resultsList.Children.Add(new Border { Height = 1, Background = Theme.Stroke });
            }

            _resultsList.Children.Add(BuildResultRow(list[i]));
        }
    }

    private UIElement BuildResultRow(MarketplaceItem item)
    {
        bool isSelected = _selectedItem is not null
            && string.Equals(_selectedItem.CatalogId, item.CatalogId, StringComparison.OrdinalIgnoreCase);

        var row = new Grid
        {
            ColumnSpacing = 12,
            Padding = new Thickness(14, 10, 14, 10),
            MinHeight = 58,
            Background = isSelected ? Theme.CardHover : Theme.Card,
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new AppIcon(32);
        icon.Show("catalog:" + item.CatalogId, item.Name);
        BindIcon(icon, item.CatalogId, item.Homepage);
        Grid.SetColumn(icon, 0);
        row.Children.Add(icon);

        var title = new TextBlock
        {
            Text = item.Name,
            FontFamily = Chrome.Font,
            FontSize = Theme.BodySize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var sub = Chrome.Caption($"{item.Publisher}  ·  {item.CatalogId}");
        sub.TextTrimming = TextTrimming.CharacterEllipsis;
        var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textCol.Children.Add(title);
        textCol.Children.Add(sub);
        Grid.SetColumn(textCol, 1);
        row.Children.Add(textCol);

        var sourceChip = Chrome.Chip(item.SourceLabel, ChipKind.Neutral);
        Grid.SetColumn(sourceChip, 2);
        row.Children.Add(sourceChip);

        var qAction = _services.Queue.CurrentBatch.FirstOrDefault(q =>
            (string.Equals(q.Key, $"{item.SourceKind}:{item.CatalogId}", StringComparison.OrdinalIgnoreCase)
             || string.Equals(q.Key, item.CatalogId, StringComparison.OrdinalIgnoreCase))
            && q.State is ActionState.Waiting or ActionState.Working);

        FrameworkElement actionEl;
        if (qAction is not null)
        {
            var txt = qAction.State == ActionState.Waiting
                ? "Waiting"
                : qAction.Percent is int p ? $"{qAction.Phase ?? "Working"} {p}%" : (qAction.Phase ?? "Working");
            actionEl = Chrome.Chip(txt, ChipKind.Accent);
        }
        else if (item.State == MarketplaceItemState.Installed)
        {
            actionEl = Chrome.Chip("Installed", ChipKind.Ok);
        }
        else
        {
            var btn = Chrome.Action(item.State == MarketplaceItemState.UpdateAvailable ? "Update" : "Install");
            btn.Padding = new Thickness(12, 5, 12, 5);
            btn.Click += (_, _) => _services.Catalog.EnqueueInstall(item);
            AutomationProperties.SetName(btn, $"{btn.Content} {item.Name}");
            actionEl = btn;
        }

        Grid.SetColumn(actionEl, 3);
        row.Children.Add(actionEl);

        row.PointerEntered += (_, _) => { if (!isSelected) row.Background = Theme.CardHover; };
        row.PointerExited += (_, _) => { if (!isSelected) row.Background = Theme.Card; };
        row.Tapped += (_, e) =>
        {
            if (e.OriginalSource is not Button)
            {
                _ = SelectForDetailsAsync(item, CancellationToken.None);
            }
        };

        return row;
    }

    private void OpenItemInDetails(MarketplaceItem item)
    {
        _currentResults = new[] { item };
        ShowResultsChrome(item.Name, loading: false);
        RenderResultsList();
        _ = SelectForDetailsAsync(item, CancellationToken.None);
    }

    private async Task SelectForDetailsAsync(MarketplaceItem item, CancellationToken token)
    {
        _selectedItem = item;
        _detailsPanel.ShowItem(item, null);
        BindIcon(_detailsPanel.Icon, item.CatalogId, item.Homepage, size: 64);

        var details = await _services.Catalog.GetDetailsAsync(item.CatalogId, token).ConfigureAwait(true);
        if (details is not null && _selectedItem?.CatalogId == item.CatalogId)
        {
            _detailsPanel.ShowItem(item, details);
            BindIcon(_detailsPanel.Icon, item.CatalogId, details.Homepage ?? item.Homepage, size: 64);
        }
    }

    private void BindIcon(AppIcon icon, string catalogId, string? fallbackHomepage = null, int size = 48)
    {
        int ticket = icon.CurrentTicket;
        var installed = _services.InstalledApps.FindByCatalogId(catalogId);
        var cached = (installed is not null ? _services.Icons.TryGetCachedPath(installed.Key) : null)
            ?? _services.Icons.TryGetCachedPath("catalog:" + catalogId);
        if (cached is not null)
        {
            var img = IconService.LoadImageSource(cached, size);
            if (img is not null)
            {
                icon.SetImage(ticket, img);
                return;
            }
        }

        _ = Task.Run(async () =>
        {
            var path = installed is not null
                ? await _services.Icons.ResolveInstalledAsync(installed, CancellationToken.None).ConfigureAwait(false)
                : await _services.Icons.ResolveCatalogAsync(catalogId, fallbackHomepage, CancellationToken.None).ConfigureAwait(false);
            if (path is null) return;

            _services.Post(() =>
            {
                var img = IconService.LoadImageSource(path, size);
                if (img is not null)
                {
                    icon.SetImage(ticket, img);
                }
            });
        });
    }
}
