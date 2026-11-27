using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Models;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Inventory;
using QuantumUpdater.Views.Controls;
using Windows.System;

namespace QuantumUpdater.Views;

/// <summary>
/// Full Installed inventory screen (46.5.21-22, mockup 02-installed.html).
/// Displays all merged Registry + Store + Winget apps with source pills, search,
/// real icons, protected states, and queued safe uninstallations.
/// </summary>
public sealed class InstalledPage : Grid
{
    private readonly AppServices _services;
    private readonly TextBlock _subtitle;
    private readonly Button _refresh;
    private readonly AutoSuggestBox _searchBox;
    private readonly ComboBox _sortBox;
    private readonly ToggleSwitch _showSystem;
    private readonly StackPanel _sourcePills;
    private readonly VirtualList _list;

    private AppSource? _sourceFilter;
    private bool _sortBySize;
    private IReadOnlyList<AppEntry> _visibleEntries = Array.Empty<AppEntry>();

    public InstalledPage(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search + sort
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Source chips
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

        // Header
        var header = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _subtitle = Chrome.Caption("Loading installed apps…");
        var left = new StackPanel { Spacing = 4 };
        left.Children.Add(Chrome.Title("Installed"));
        left.Children.Add(_subtitle);
        Grid.SetColumn(left, 0);
        header.Children.Add(left);

        _refresh = Chrome.SecondaryButton("Check again");
        _refresh.VerticalAlignment = VerticalAlignment.Bottom;
        _refresh.Click += async (_, _) =>
        {
            _refresh.IsEnabled = false;
            await _services.InstalledApps.RefreshAsync().ConfigureAwait(true);
            _refresh.IsEnabled = true;
        };
        Grid.SetColumn(_refresh, 1);
        header.Children.Add(_refresh);
        Grid.SetRow(header, 0);
        Children.Add(header);

        // Toolbar row
        _searchBox = Chrome.SearchBox("Search installed apps");
        _searchBox.Width = 340;
        _searchBox.TextChanged += (_, _) => Rebuild();

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
            _sortBySize = _sortBox.SelectedIndex == 1;
            Rebuild();
        };

        _showSystem = new ToggleSwitch
        {
            OnContent = "Show Windows parts",
            OffContent = "Show Windows parts",
            Foreground = Theme.Muted,
            FontFamily = Chrome.Font,
            IsOn = false,
        };
        _showSystem.Toggled += (_, _) => Rebuild();

        var tool = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 10) };
        tool.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tool.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tool.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tool.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(_searchBox, 0);
        Grid.SetColumn(_sortBox, 2);
        Grid.SetColumn(_showSystem, 3);
        tool.Children.Add(_searchBox);
        tool.Children.Add(_sortBox);
        tool.Children.Add(_showSystem);
        Grid.SetRow(tool, 1);
        Children.Add(tool);

        // Source filter pills
        _sourcePills = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 14),
        };
        Grid.SetRow(_sourcePills, 2);
        Children.Add(_sourcePills);

        // VirtualList
        _list = new VirtualList();
        _list.RowPrepared += BindRowIcon;
        _list.RowAction += async (key, action) => await OnRowActionAsync(key, action).ConfigureAwait(true);

        var frame = new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Child = _list,
        };
        Grid.SetRow(frame, 3);
        Children.Add(frame);

        Loaded += (_, _) =>
        {
            _services.InstalledApps.Changed += OnChanged;
            _services.Queue.Changed += OnQueueChanged;
            Rebuild();
        };
        Unloaded += (_, _) =>
        {
            _services.InstalledApps.Changed -= OnChanged;
            _services.Queue.Changed -= OnQueueChanged;
        };
    }

    public void FocusSearch() => _searchBox.Focus(FocusState.Programmatic);

    public Task RefreshAsync() => _services.InstalledApps.RefreshAsync();

    private void OnChanged() => Rebuild();

    private void OnQueueChanged(QueuedAction _) => Rebuild();

    private void Rebuild()
    {
        var all = _services.InstalledApps.Apps;
        if (!_showSystem.IsOn)
        {
            all = all.Where(a => !a.IsProtected).ToList();
        }

        int total = all.Count;
        int catCount = all.Count(a => a.Source == AppSource.MicrosoftCatalog);
        int storeCount = all.Count(a => a.Source == AppSource.MicrosoftStore);
        int directCount = all.Count(a => a.Source == AppSource.InstalledDirectly);
        int scoopCount = all.Count(a => a.Source == AppSource.Scoop);
        int chocoCount = all.Count(a => a.Source == AppSource.Chocolatey);

        _subtitle.Text = _services.InstalledApps.SavedAt is DateTimeOffset s
            ? $"{total} apps on this PC · Updated {s.ToLocalTime():HH:mm}"
            : $"{total} apps on this PC";

        RebuildSourcePills(total, catCount, storeCount, directCount, scoopCount, chocoCount);

        IEnumerable<AppEntry> filtered = all;
        if (_sourceFilter is AppSource src)
        {
            filtered = filtered.Where(a => a.Source == src);
        }

        string q = (_searchBox.Text ?? string.Empty).Trim();
        if (q.Length > 0)
        {
            filtered = filtered.Where(a =>
                a.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(a.Publisher) && a.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(a.CatalogId) && a.CatalogId.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        filtered = _sortBySize
            ? filtered.OrderBy(a => SourceLabel(a.Source), StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase);

        _visibleEntries = filtered.ToList();

        var batchByKey = _services.Queue.CurrentBatch
            .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var listItems = new List<ListItem>(_visibleEntries.Count + 1);
        foreach (var app in _visibleEntries)
        {
            batchByKey.TryGetValue(app.Key, out var active);
            AppRowState state = AppRowState.Ready;
            string? phase = null;
            int? pct = null;
            string? err = null;

            if (active is not null)
            {
                switch (active.State)
                {
                    case ActionState.Waiting:
                        state = AppRowState.Waiting;
                        break;
                    case ActionState.Working:
                        state = AppRowState.Working;
                        phase = active.Phase ?? "Removing";
                        pct = active.Percent;
                        break;
                    case ActionState.Failed:
                        state = AppRowState.Failed;
                        err = active.Message;
                        break;
                }
            }

            listItems.Add(new RowItem(new AppRowModel
            {
                Key = app.Key,
                Name = app.Name,
                Publisher = app.Publisher ?? SourceLabel(app.Source),
                FromVersion = app.Version,
                ToVersion = app.UpdateVersion,
                SourceLabel = app.IsProtected ? "Protected" : SourceLabel(app.Source),
                State = state,
                Phase = phase,
                Percent = pct,
                ErrorText = err,
                CanSelect = false,
                ActionLabel = app.IsProtected ? "Protected" : "Remove",
            }));
        }

        _list.SetItems(listItems);
    }

    private void RebuildSourcePills(int total, int cat, int store, int direct, int scoop, int choco)
    {
        _sourcePills.Children.Clear();
        AddPill("All", total, null);
        AddPill("Microsoft catalog", cat, AppSource.MicrosoftCatalog);
        AddPill("Microsoft Store", store, AppSource.MicrosoftStore);
        AddPill("Installed directly", direct, AppSource.InstalledDirectly);
        if (scoop > 0 || _services.Scoop.IsAvailable)
        {
            AddPill("Scoop", scoop, AppSource.Scoop);
        }
        if (choco > 0 || _services.Chocolatey.IsAvailable)
        {
            AddPill("Chocolatey", choco, AppSource.Chocolatey);
        }
    }

    private void AddPill(string label, int count, AppSource? filter)
    {
        bool active = _sourceFilter == filter;
        var btn = new Button
        {
            Content = $"{label} ({count})",
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
            _sourceFilter = filter;
            Rebuild();
        };
        _sourcePills.Children.Add(btn);
    }

    private async Task OnRowActionAsync(string key, RowAction action)
    {
        if (action == RowAction.Cancel)
        {
            var running = _services.Queue.CurrentBatch.FirstOrDefault(i =>
                string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)
                && i.State is ActionState.Waiting or ActionState.Working);
            if (running is not null)
            {
                _services.Queue.Cancel(running.Id);
            }
            return;
        }

        if (action is not (RowAction.Update or RowAction.Retry))
        {
            return;
        }

        var app = _visibleEntries.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            return;
        }

        var plan = UninstallPlanner.Plan(app);
        if (!plan.CanRun)
        {
            var openSettings = await ConfirmDialog.ShowAsync(XamlRoot, new ConfirmRequest(
                Title: app.Name,
                Body: plan.SummaryText,
                Apps: null,
                Note: plan.RefusalReason,
                MainButton: "Open Windows Settings",
                Danger: false,
                TileName: app.Name)).ConfigureAwait(true);

            if (openSettings)
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures"));
            }
            return;
        }

        var confirm = await ConfirmDialog.ShowAsync(XamlRoot, new ConfirmRequest(
            Title: $"Remove {app.Name}?",
            Body: plan.SummaryText,
            Apps: null,
            Note: "Your documents, photos, and other personal files stay on this PC.",
            MainButton: "Remove",
            Danger: true,
            TileName: app.Name)).ConfigureAwait(true);

        if (confirm)
        {
            _services.Uninstaller.Enqueue(app, plan);
        }
    }

    private void BindRowIcon(AppRow row, AppRowModel model)
    {
        var app = _visibleEntries.FirstOrDefault(a => string.Equals(a.Key, model.Key, StringComparison.OrdinalIgnoreCase));
        if (app is null) return;

        int ticket = row.Icon.CurrentTicket;
        var cached = _services.Icons.TryGetCachedPath(app.Key);
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
            var path = await _services.Icons.ResolveInstalledAsync(app, CancellationToken.None).ConfigureAwait(false);
            if (path is null) return;
            _services.Post(() =>
            {
                var img = IconService.LoadImageSource(path, 32);
                if (img is not null)
                {
                    row.Icon.SetImage(ticket, img);
                }
            });
        });
    }

    private static string SourceLabel(AppSource source) => source switch
    {
        AppSource.MicrosoftCatalog => "Microsoft catalog",
        AppSource.MicrosoftStore => "Microsoft Store",
        AppSource.Scoop => "Scoop",
        AppSource.Chocolatey => "Chocolatey",
        _ => "Installed directly",
    };
}
