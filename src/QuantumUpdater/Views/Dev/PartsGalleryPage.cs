// TEMP 46.7.48 — remove in 46.7.85
// Parts gallery: one scroll page that shows every shared part in each state.
// It opens only with DESKVERVE_DIAG=gallery (see MainWindow). The demo actions
// for the activity bar (46.7.49) are in AddDemoActions below.
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Scan;
using QuantumUpdater.Views.Controls;
using GroupItem = QuantumUpdater.Views.Controls.GroupItem;

namespace QuantumUpdater.Views.Dev;

// TEMP DIAGNOSTIC 46.7.48
// Base class is UserControl, not ScrollViewer: WinUI ScrollViewer is sealed. The scroll view is inside.
public sealed class PartsGalleryPage : UserControl
{
    private static readonly string[] TileNames =
    {
        "Mozilla Firefox", "7-Zip", "Zoom", "", "Visual Studio Code", "Notepad++", "Git", "Python 3.12",
    };

    private static readonly int[] TileSizes = { 32, 48 };

    /// <summary>Builds the page. The queue is the one app-wide queue from AppServices.</summary>
    public PartsGalleryPage(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scroll = new ScrollViewer
        {
            Background = Theme.Canvas,
            Padding = new Thickness(32, 24, 32, 24),
        };

        var page = new StackPanel { Spacing = Theme.Gap32 };

        // 1. Buttons
        page.Children.Add(Section(
            "Buttons",
            Row(
                Chrome.Action("Accent"),
                Chrome.SecondaryButton("Secondary"),
                Chrome.GhostButton("Ghost"),
                Chrome.DangerButton("Danger"))));

        // 2. Chips
        page.Children.Add(Section(
            "Chips",
            Row(
                Chrome.Chip("Microsoft catalog", ChipKind.Neutral),
                Chrome.Chip("Working", ChipKind.Accent),
                Chrome.Chip("Done", ChipKind.Ok),
                Chrome.Chip("Waiting", ChipKind.Warn),
                Chrome.Chip("Failed", ChipKind.Bad))));

        // 3. Text
        page.Children.Add(Section(
            "Text",
            Chrome.Title("Title text (28 px)"),
            new TextBlock
            {
                Text = "Section text (20 px)",
                FontFamily = Chrome.Font,
                FontSize = Theme.SectionSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
            },
            Chrome.Line("Body text (14 px). The text wraps when the window is narrow."),
            Chrome.Caption("Caption text (12 px)")));

        // 4. Search box
        page.Children.Add(Section("Search box", Chrome.SearchBox("Search updates")));

        // 5. Letter tiles
        var tiles = new List<UIElement>();
        foreach (var size in TileSizes)
        {
            tiles.Add(Chrome.Caption(size + " px"));
            for (var start = 0; start < TileNames.Length; start += 4)
            {
                var line = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Theme.Gap16,
                };
                for (var i = start; i < start + 4 && i < TileNames.Length; i++)
                {
                    line.Children.Add(TileCell(TileNames[i], size));
                }

                tiles.Add(line);
            }
        }

        page.Children.Add(Section("Letter tiles", tiles.ToArray()));

        // 6. App rows, one for each state
        page.Children.Add(Section("App rows", AppRowItems().ToArray()));

        // 7. Virtual list with 2,000 rows
        page.Children.Add(VirtualListSection());

        // 8. Scan job rows
        page.Children.Add(Section("Scan job rows", ScanJobItems().ToArray()));

        // 9. Dialog buttons
        var updateAll = Chrome.SecondaryButton("Open Update all dialog");
        updateAll.Click += async (_, _) =>
        {
            await ConfirmDialog.ShowAsync(XamlRoot, new ConfirmRequest(
                "Update 3 apps?",
                "DeskVerve updates these apps one at a time. You can keep using DeskVerve while they update.",
                new[]
                {
                    new ConfirmApp("Mozilla Firefox", "129.0"),
                    new ConfirmApp("Visual Studio Code", "1.95.0"),
                    new ConfirmApp("7-Zip", "24.08"),
                },
                "Some apps close while they update. Save your work in these apps first.",
                "Update all",
                false));
        };

        var removeZoom = Chrome.SecondaryButton("Open Remove dialog");
        removeZoom.Click += async (_, _) =>
        {
            await ConfirmDialog.ShowAsync(XamlRoot, new ConfirmRequest(
                "Remove Zoom?",
                "DeskVerve removes this app quietly.",
                null,
                "Your documents, photos and other files stay on this PC.",
                "Remove",
                true,
                "Zoom"));
        };

        page.Children.Add(Section("Dialogs", Row(updateAll, removeZoom)));

        // 10. Activity bar and demo buttons
        var activity = new ActivityBar(services.Queue);
        var demoButton = Chrome.SecondaryButton("Demo queue");
        demoButton.Click += (_, _) => AddDemoActions(services.Queue);

        var holdButton = Chrome.SecondaryButton("Hold on");
        var hold = false;
        holdButton.Click += (_, _) =>
        {
            hold = !hold;
            holdButton.Content = hold ? "Hold off" : "Hold on";
            activity.SetHold(hold, null);
        };

        page.Children.Add(Section(
            "Activity bar",
            Chrome.Caption("The bar shows only while work is open. Use the buttons to add demo work."),
            activity,
            Row(demoButton, holdButton)));

        scroll.Content = page;
        Content = scroll;
    }

    // TEMP DIAGNOSTIC 46.7.49
    /// <summary>
    /// Adds three demo actions to the queue (46.7.49). Each one honours its cancel token.
    /// 1) Firefox: Downloading 0 to 100 percent over 4 s, then success.
    /// 2) 7-Zip: Installing with no percent for 4 s, then success.
    /// 3) Zoom: 2 s, then failure with the installer error text.
    /// </summary>
    public static void AddDemoActions(ActionQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        queue.Enqueue("demo-1", ActionKind.Update, "Mozilla Firefox", async (progress, ct) =>
        {
            for (var step = 0; step <= 20; step++)
            {
                progress.Report(new ActionProgress("Downloading", step * 5));
                if (step < 20)
                {
                    await Task.Delay(200, ct);
                }
            }

            return new ActionOutcome(true, null);
        });

        queue.Enqueue("demo-2", ActionKind.Update, "7-Zip", async (progress, ct) =>
        {
            progress.Report(new ActionProgress("Installing", null));
            await Task.Delay(4000, ct);
            return new ActionOutcome(true, null);
        });

        queue.Enqueue("demo-3", ActionKind.Install, "Zoom", async (progress, ct) =>
        {
            progress.Report(new ActionProgress("Installing", null));
            await Task.Delay(2000, ct);
            return new ActionOutcome(false, "Update failed. The installer stopped (code 1603).");
        });
    }

    private static StackPanel Section(string title, params UIElement[] items)
    {
        var panel = new StackPanel { Spacing = Theme.Gap12 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        });
        foreach (var item in items)
        {
            panel.Children.Add(item);
        }

        return panel;
    }

    private static StackPanel Row(params UIElement[] items)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Theme.Gap12,
        };
        foreach (var item in items)
        {
            panel.Children.Add(item);
        }

        return panel;
    }

    private static StackPanel Labelled(string label, UIElement item)
    {
        var panel = new StackPanel { Spacing = Theme.Gap4 };
        panel.Children.Add(Chrome.Caption(label));
        panel.Children.Add(item);
        return panel;
    }

    private static StackPanel TileCell(string name, double size)
    {
        var cell = new StackPanel
        {
            Spacing = Theme.Gap4,
            Margin = new Thickness(0, 0, 0, 12),
        };
        cell.Children.Add(Chrome.LetterTile(name, size));
        cell.Children.Add(Chrome.Caption(name.Length == 0 ? "(empty name)" : name));
        return cell;
    }

    private static List<UIElement> AppRowItems()
    {
        var items = new List<UIElement>();
        var models = new (string Label, AppRowModel Model)[]
        {
            ("Ready, with publisher, 128.0 to 129.0, Microsoft catalog", new AppRowModel
            {
                Key = "row-ready",
                Name = "Mozilla Firefox",
                Publisher = "Mozilla",
                FromVersion = "128.0",
                ToVersion = "129.0",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Ready,
            }),
            ("Waiting next (place 1)", new AppRowModel
            {
                Key = "row-next",
                Name = "7-Zip",
                Publisher = "Igor Pavlov",
                FromVersion = "24.07",
                ToVersion = "24.08",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Waiting,
                PlaceInLine = 1,
            }),
            ("Waiting, 3 in line", new AppRowModel
            {
                Key = "row-third",
                Name = "Zoom Workplace",
                Publisher = "Zoom Video Communications",
                FromVersion = "6.2.0",
                ToVersion = "6.3.0",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Waiting,
                PlaceInLine = 3,
            }),
            ("Waiting on hold", new AppRowModel
            {
                Key = "row-hold",
                Name = "Notion",
                Publisher = "Notion Labs",
                FromVersion = "3.1",
                ToVersion = "3.2",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Waiting,
                PlaceInLine = 2,
                IsHeld = true,
            }),
            ("Working 45 percent (Downloading)", new AppRowModel
            {
                Key = "row-45",
                Name = "Visual Studio Code",
                Publisher = "Microsoft",
                FromVersion = "1.94.0",
                ToVersion = "1.95.0",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Working,
                Phase = "Downloading",
                Percent = 45,
            }),
            ("Working with no number (Installing)", new AppRowModel
            {
                Key = "row-install",
                Name = "Spotify",
                Publisher = "Spotify AB",
                FromVersion = "1.2.40",
                ToVersion = "1.2.41",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Working,
                Phase = "Installing",
            }),
            ("Stopping", new AppRowModel
            {
                Key = "row-stop",
                Name = "Java SE Development Kit 25",
                Publisher = "Oracle",
                FromVersion = "25.0.0",
                ToVersion = "25.0.1",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Stopping,
            }),
            ("Updated (129.0, time now)", new AppRowModel
            {
                Key = "row-done",
                Name = "Mozilla Firefox",
                Publisher = "Mozilla",
                FromVersion = "128.0",
                ToVersion = "129.0",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Updated,
                DoneVersion = "129.0",
                DoneAt = DateTimeOffset.Now,
            }),
            ("Failed", new AppRowModel
            {
                Key = "row-failed",
                Name = "Zoom Workplace",
                Publisher = "Zoom Video Communications",
                FromVersion = "6.2.0",
                ToVersion = "6.3.0",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Failed,
                ErrorText = "Update failed. The installer stopped (code 1603).",
            }),
            ("Version not known (from version null)", new AppRowModel
            {
                Key = "row-unknown",
                Name = "Notepad++",
                Publisher = "Don Ho",
                FromVersion = null,
                ToVersion = "8.7.5",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Ready,
            }),
            ("Can select (check box shows)", new AppRowModel
            {
                Key = "row-select",
                Name = "Python 3.12",
                Publisher = "Python Software Foundation",
                FromVersion = "3.12.7",
                ToVersion = "3.12.8",
                SourceLabel = "Microsoft catalog",
                State = AppRowState.Ready,
                CanSelect = true,
            }),
        };

        for (var i = 0; i < models.Length; i++)
        {
            var row = new AppRow();
            row.Apply(models[i].Model);
            if (i == 0)
            {
                row.ShowTopLine = false;
            }

            items.Add(Labelled(models[i].Label, row));
        }

        return items;
    }

    private static List<UIElement> ScanJobItems()
    {
        var items = new List<UIElement>();

        var waiting = new ScanJobRow("job-waiting", "Check for updates", "");
        waiting.Apply(ScanJobState.Waiting, null, null, null, null, null);
        items.Add(Labelled("Waiting", waiting));

        var step = new ScanJobRow("job-step", "Read installed apps", "");
        step.Apply(ScanJobState.Working, 0.66, null, null, 2, 3);
        items.Add(Labelled("Working, Step 2 of 3", step));

        var percent = new ScanJobRow("job-percent", "Read Marketplace", "");
        percent.Apply(ScanJobState.Working, 0.78, null, null, null, null);
        items.Add(Labelled("Working 78 percent (no steps)", percent));

        var noNumber = new ScanJobRow("job-nonumber", "Find app icons", "");
        noNumber.Apply(ScanJobState.Working, null, null, null, null, null);
        items.Add(Labelled("Working with no number", noNumber));

        var done = new ScanJobRow("job-done", "Read this PC", "");
        done.Apply(ScanJobState.Done, null, "214 apps on this PC", null, null, null);
        items.Add(Labelled("Done", done));

        var failed = new ScanJobRow("job-failed", "Check versions", "");
        failed.Apply(ScanJobState.Failed, null, null, "Took too long", null, null);
        items.Add(Labelled("Failed", failed));

        return items;
    }

    private StackPanel VirtualListSection()
    {
        var list = new VirtualList { Height = 400 };
        list.SetItems(BuildListItems());

        var created = Chrome.Caption("Rows created: 0");

        // Read the count once, after 2 s, when the first rows exist.
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(2);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => created.Text = $"Rows created: {list.CreatedRowCount}";
        timer.Start();

        return Section(
            "Virtual list (2,000 rows, 3 groups)",
            created,
            list);
    }

    private static List<ListItem> BuildListItems()
    {
        var titles = new[] { "Ready to update", "Up to date", "Not known" };
        var counts = new[] { 700, 700, 600 };
        var items = new List<ListItem>();
        var number = 0;
        for (var group = 0; group < titles.Length; group++)
        {
            items.Add(new GroupItem("group-" + group, titles[group], counts[group], true));
            for (var i = 0; i < counts[group]; i++)
            {
                number++;
                items.Add(new RowItem(new AppRowModel
                {
                    Key = "list-" + number,
                    Name = "Demo app " + number,
                    FromVersion = "1.0",
                    ToVersion = "1.1",
                    SourceLabel = "Microsoft catalog",
                    State = AppRowState.Ready,
                }));
            }
        }

        return items;
    }
}
