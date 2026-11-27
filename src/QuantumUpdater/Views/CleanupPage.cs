using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Cleanup;
using Windows.System;

namespace QuantumUpdater.Views;

/// <summary>
/// System Cleanup view (ADR 048, Phase 46.2).
/// All 4 parts:
/// 1. Temporary files (Windows-supported route)
/// 2. Startup apps (view, turn off, undo)
/// 3. Leftover folders (Recycle Bin per folder)
/// 4. Activity history (read-only audit trail)
/// </summary>
public sealed class CleanupPage : UserControl
{
    private readonly AppServices _services;
    private readonly StackPanel _mainStack;
    private readonly ContentControl _subHost;

    private readonly Button _tabTemp;
    private readonly Button _tabStartup;
    private readonly Button _tabLeftover;
    private readonly Button _tabHistory;

    private int _activeTab = 0;

    public CleanupPage(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Background = Theme.Canvas;

        _mainStack = new StackPanel { Spacing = 20, Padding = new Thickness(0, 0, 16, 28) };

        // 1. Header
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(Chrome.Title("System Cleanup"));
        header.Children.Add(Chrome.Caption("Clean temporary files, manage startup programs with undo, reclaim orphan folders, and review activity."));
        _mainStack.Children.Add(header);

        // 2. Navigation Tabs
        var navBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        _tabTemp = CreateTabButton("Temporary Files", 0);
        _tabStartup = CreateTabButton("Startup Apps", 1);
        _tabLeftover = CreateTabButton("Leftover Folders", 2);
        _tabHistory = CreateTabButton("Activity History", 3);

        navBar.Children.Add(_tabTemp);
        navBar.Children.Add(_tabStartup);
        navBar.Children.Add(_tabLeftover);
        navBar.Children.Add(_tabHistory);

        _mainStack.Children.Add(navBar);

        // 3. Sub-view Host
        _subHost = new ContentControl();
        _mainStack.Children.Add(_subHost);

        Content = new ScrollViewer
        {
            Content = _mainStack,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        SelectTab(0);
    }

    private Button CreateTabButton(string label, int index)
    {
        var btn = new Button
        {
            Content = label,
            FontFamily = Chrome.Font,
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Padding = new Thickness(14, 6, 14, 6),
        };
        btn.Click += (_, _) => SelectTab(index);
        return btn;
    }

    private void SelectTab(int index)
    {
        _activeTab = index;

        _tabTemp.Style = index == 0 ? (Style)Application.Current.Resources["AccentButtonStyle"] : (Style)Application.Current.Resources["DefaultButtonStyle"];
        _tabStartup.Style = index == 1 ? (Style)Application.Current.Resources["AccentButtonStyle"] : (Style)Application.Current.Resources["DefaultButtonStyle"];
        _tabLeftover.Style = index == 2 ? (Style)Application.Current.Resources["AccentButtonStyle"] : (Style)Application.Current.Resources["DefaultButtonStyle"];
        _tabHistory.Style = index == 3 ? (Style)Application.Current.Resources["AccentButtonStyle"] : (Style)Application.Current.Resources["DefaultButtonStyle"];

        _subHost.Content = index switch
        {
            0 => BuildTempFilesView(),
            1 => BuildStartupAppsView(),
            2 => BuildLeftoversView(),
            3 => BuildHistoryView(),
            _ => BuildTempFilesView(),
        };
    }

    // ==========================================
    // 1. Temporary Files View
    // ==========================================

    private UIElement BuildTempFilesView()
    {
        var stack = new StackPanel { Spacing = 16 };

        var summary = _services.Cleanup.ScanTempFiles();
        string formattedBytes = LargeFile.FormatBytes(summary.TotalBytes);

        var tileGrid = new Grid { ColumnSpacing = 16 };
        tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        tileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var bentoContent = new StackPanel { Spacing = 6 };
        bentoContent.Children.Add(new TextBlock
        {
            Text = "Safe to Clean",
            FontFamily = Chrome.Font,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.TextSecondary,
        });
        bentoContent.Children.Add(new TextBlock
        {
            Text = formattedBytes,
            FontFamily = Chrome.Font,
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Foreground = Theme.Accent,
        });
        bentoContent.Children.Add(Chrome.Caption($"{summary.FileCount:N0} files found in Windows & User Temp"));

        var bentoCard = Chrome.Card(bentoContent);
        Grid.SetColumn(bentoCard, 0);
        tileGrid.Children.Add(bentoCard);

        var actionStack = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var cleanBtn = new Button
        {
            Content = $"Clean {formattedBytes}",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            Height = 40,
            Padding = new Thickness(20, 0, 20, 0),
        };

        var statusBlock = Chrome.Caption("Windows-supported temp cleanup route only. Active files and locked application caches are preserved safely.");
        cleanBtn.Click += async (_, _) =>
        {
            cleanBtn.IsEnabled = false;
            statusBlock.Text = "Cleaning temporary files...";
            var (cleanedBytes, cleanedCount) = await Task.Run(() => _services.Cleanup.CleanTempFiles());
            statusBlock.Text = $"Successfully cleaned {cleanedCount} files ({LargeFile.FormatBytes(cleanedBytes)}).";
            cleanBtn.Content = "Cleaned";
        };

        actionStack.Children.Add(cleanBtn);
        actionStack.Children.Add(statusBlock);
        Grid.SetColumn(actionStack, 1);
        tileGrid.Children.Add(actionStack);

        stack.Children.Add(tileGrid);

        // Folders inspected
        var inspectedCard = new StackPanel { Spacing = 6 };
        inspectedCard.Children.Add(new TextBlock
        {
            Text = "Inspected System Locations",
            FontFamily = Chrome.Font,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        });
        inspectedCard.Children.Add(Chrome.Line("• User Temporary Files (%TEMP%)", Theme.TextSecondary));
        inspectedCard.Children.Add(Chrome.Line("• Windows System Temp (C:\\Windows\\Temp)", Theme.TextSecondary));
        inspectedCard.Children.Add(Chrome.Line("• Windows Crash Dumps & Error Reports (WER)", Theme.TextSecondary));
        stack.Children.Add(Chrome.Card(inspectedCard));

        return stack;
    }

    // ==========================================
    // 2. Startup Apps View
    // ==========================================

    private UIElement BuildStartupAppsView()
    {
        var stack = new StackPanel { Spacing = 14 };

        var headInfo = new StackPanel { Spacing = 2 };
        headInfo.Children.Add(new TextBlock
        {
            Text = "Startup Applications",
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        });
        headInfo.Children.Add(Chrome.Caption("Manage applications that launch when Windows boots. Disabling is completely undoable."));
        stack.Children.Add(headInfo);

        var apps = _services.Cleanup.ListStartupApps();
        if (apps.Count == 0)
        {
            stack.Children.Add(Chrome.Card(Chrome.Line("No startup programs detected.", Theme.TextSecondary)));
            return stack;
        }

        foreach (var item in apps)
        {
            var rowGrid = new Grid { ColumnSpacing = 16 };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 2 };
            info.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontFamily = Chrome.Font,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
            });
            info.Children.Add(new TextBlock
            {
                Text = $"{item.Source}  ·  {item.Command}",
                FontFamily = Chrome.Font,
                FontSize = 12,
                Foreground = Theme.TextSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(info, 0);
            rowGrid.Children.Add(info);

            var toggle = new ToggleSwitch
            {
                IsOn = item.IsEnabled,
                OnContent = "Enabled",
                OffContent = "Disabled",
                VerticalAlignment = VerticalAlignment.Center,
            };

            toggle.Toggled += (_, _) =>
            {
                _services.Cleanup.ToggleStartupApp(item, toggle.IsOn);
            };

            Grid.SetColumn(toggle, 1);
            rowGrid.Children.Add(toggle);

            stack.Children.Add(Chrome.Card(rowGrid));
        }

        return stack;
    }

    // ==========================================
    // 3. Leftover Folders View
    // ==========================================

    private UIElement BuildLeftoversView()
    {
        var stack = new StackPanel { Spacing = 14 };

        var headInfo = new StackPanel { Spacing = 2 };
        headInfo.Children.Add(new TextBlock
        {
            Text = "Leftover Folders",
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        });
        headInfo.Children.Add(Chrome.Caption("Folders left behind by uninstalled apps in AppData. Reclaim space by sending them to the Recycle Bin."));
        stack.Children.Add(headInfo);

        var knownApps = _services.InstalledApps.Apps.Select(a => a.Name);
        var leftovers = _services.Cleanup.ScanLeftoverFolders(knownApps);

        if (leftovers.Count == 0)
        {
            stack.Children.Add(Chrome.Card(Chrome.Line("No orphan folders found in AppData.", Theme.TextSecondary)));
            return stack;
        }

        var listContainer = new StackPanel { Spacing = 8 };
        foreach (var folder in leftovers)
        {
            var rowGrid = new Grid { ColumnSpacing = 16 };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 2 };
            info.Children.Add(new TextBlock
            {
                Text = folder.Name,
                FontFamily = Chrome.Font,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
            });
            info.Children.Add(new TextBlock
            {
                Text = folder.Path,
                FontFamily = Chrome.Font,
                FontSize = 12,
                Foreground = Theme.TextSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(info, 0);
            rowGrid.Children.Add(info);

            var sizeText = new TextBlock
            {
                Text = folder.FormattedSize,
                FontFamily = Chrome.Font,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
            };
            Grid.SetColumn(sizeText, 1);
            rowGrid.Children.Add(sizeText);

            var recycleBtn = new Button
            {
                Content = "Recycle",
                FontSize = 12,
                Padding = new Thickness(10, 4, 10, 4),
            };

            var card = Chrome.Card(rowGrid);
            recycleBtn.Click += (_, _) =>
            {
                if (_services.Cleanup.RecycleLeftoverFolder(folder.Path))
                {
                    listContainer.Children.Remove(card);
                }
            };

            Grid.SetColumn(recycleBtn, 2);
            rowGrid.Children.Add(recycleBtn);

            listContainer.Children.Add(card);
        }

        stack.Children.Add(listContainer);
        return stack;
    }

    // ==========================================
    // 4. Activity History View (Read-Only)
    // ==========================================

    private UIElement BuildHistoryView()
    {
        var stack = new StackPanel { Spacing = 14 };

        var headInfo = new StackPanel { Spacing = 2 };
        headInfo.Children.Add(new TextBlock
        {
            Text = "Activity History",
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        });
        headInfo.Children.Add(Chrome.Caption("Read-only audit record of actions taken on this computer."));
        stack.Children.Add(headInfo);

        var history = _services.Cleanup.GetHistory();
        if (history.Count == 0)
        {
            stack.Children.Add(Chrome.Card(Chrome.Line("No activity recorded yet.", Theme.TextSecondary)));
            return stack;
        }

        foreach (var item in history)
        {
            var rowGrid = new Grid { ColumnSpacing = 16 };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var timeText = Chrome.Caption(item.Timestamp.ToString("yyyy-MM-dd HH:mm"));
            Grid.SetColumn(timeText, 0);
            rowGrid.Children.Add(timeText);

            var typeText = new TextBlock
            {
                Text = item.ActionType,
                FontFamily = Chrome.Font,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(typeText, 1);
            rowGrid.Children.Add(typeText);

            var detailStack = new StackPanel { Spacing = 2 };
            detailStack.Children.Add(new TextBlock
            {
                Text = item.Target,
                FontFamily = Chrome.Font,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = Theme.Text,
            });
            detailStack.Children.Add(Chrome.Caption(item.Details));
            Grid.SetColumn(detailStack, 2);
            rowGrid.Children.Add(detailStack);

            var statusBadge = new Border
            {
                Background = item.Success ? Theme.AccentChip : Theme.BadChip,
                BorderBrush = Theme.Stroke,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = item.Success ? "Success" : "Failed",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = item.Success ? Theme.AccentText : Theme.CriticalText,
                },
            };
            Grid.SetColumn(statusBadge, 3);
            rowGrid.Children.Add(statusBadge);

            stack.Children.Add(Chrome.Card(rowGrid));
        }

        return stack;
    }
}
