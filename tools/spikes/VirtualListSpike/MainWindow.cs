using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VirtualListSpike;

/// <summary>Spike window: 2,000 fake rows in an ItemsRepeater with a code-only element factory.</summary>
public sealed class MainWindow : Window
{
    private const int RowTotal = 2000;
    private const double ScrollStartSeconds = 3.0;
    private const double ScrollHalfSeconds = 1.5;
    private const double CloseSeconds = 10.0;

    private readonly ItemsRepeater _repeater = new();
    private readonly ScrollViewer _scroll = new();
    private readonly List<string> _names = new();
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly Stopwatch _scrollWatch = new();
    private readonly string _resultPath;
    private readonly DispatcherQueueTimer _clock;
    private DispatcherQueueTimer? _scrollTimer;
    private bool _scrollStarted;
    private double _scrollTarget;
    private string _scrollPhase = "waiting";
    private double _peakMb;

    public MainWindow()
    {
        Title = "VirtualListSpike";
        _resultPath = FindResultPath();

        for (var i = 0; i < RowTotal; i++)
        {
            _names.Add($"Fake app {i + 1:0000}");
        }

        _repeater.ItemTemplate = new RowFactory();
        _repeater.ItemsSource = _names;
        _repeater.Layout = new StackLayout();
        _repeater.ElementPrepared += (_, e) =>
        {
            if (e.Element is FrameworkElement row && row.Tag is TextBlock name && e.Index < _names.Count)
            {
                name.Text = _names[e.Index];
            }
        };

        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.Content = _repeater;

        // Star row: the scroll viewer gets the window height, so the repeater only realises visible rows.
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(_scroll);
        Content = root;

        Closed += (_, _) => Application.Current.Exit();

        _clock = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += OnClockTick;
        _clock.Start();
    }

    private void OnClockTick(DispatcherQueueTimer sender, object args)
    {
        var seconds = _elapsed.Elapsed.TotalSeconds;
        if (!_scrollStarted && seconds >= ScrollStartSeconds)
        {
            StartScroll();
        }

        var mb = CurrentMemoryMb();
        _peakMb = Math.Max(_peakMb, mb);
        var last = seconds >= CloseSeconds;
        WriteResult(seconds, mb);

        if (last)
        {
            _clock.Stop();
            _scrollTimer?.Stop();
            Close();
        }
    }

    private void StartScroll()
    {
        _scrollStarted = true;
        _scrollTarget = _scroll.ScrollableHeight;
        _scrollWatch.Restart();
        _scrollTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromMilliseconds(16);
        _scrollTimer.Tick += OnScrollTick;
        _scrollTimer.Start();
    }

    private void OnScrollTick(DispatcherQueueTimer sender, object args)
    {
        var t = _scrollWatch.Elapsed.TotalSeconds;
        double offset;
        if (t < ScrollHalfSeconds)
        {
            _scrollPhase = "down";
            offset = _scrollTarget * (t / ScrollHalfSeconds);
        }
        else if (t < ScrollHalfSeconds * 2)
        {
            _scrollPhase = "up";
            offset = _scrollTarget * (1 - ((t - ScrollHalfSeconds) / ScrollHalfSeconds));
        }
        else
        {
            _scrollPhase = "done";
            offset = 0;
            _scrollTimer?.Stop();
        }

        _scroll.ChangeView(null, offset, null, true);
    }

    private void WriteResult(double seconds, double mb)
    {
        var text = string.Join(Environment.NewLine,
            "Virtual list spike (task 46.7.1)",
            $"Elapsed seconds: {seconds:0.0}",
            $"Rows created: {RowFactory.CreatedCount}",
            $"Memory MB: {mb:0.0}",
            $"Peak memory MB: {_peakMb:0.0}",
            $"Rows in list: {RowTotal}",
            $"Icon loaded: {(RowFactory.IconLoaded ? "yes" : "no")}",
            $"Scroll phase: {_scrollPhase}",
            $"Scroll target px: {_scrollTarget:0}",
            "");
        File.WriteAllText(_resultPath, text);
    }

    private static double CurrentMemoryMb()
    {
        using var process = Process.GetCurrentProcess();
        return process.WorkingSet64 / 1048576.0;
    }

    private static string FindResultPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VirtualListSpike.csproj")))
            {
                return Path.Combine(dir.FullName, "result.txt");
            }

            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "result.txt");
    }
}

/// <summary>Builds one row in code. Recycled rows go to a pool and are reused. Counts only new rows.</summary>
internal sealed class RowFactory : IElementFactory
{
    public static int CreatedCount;
    private static BitmapImage? _icon;

    // Recycled rows wait here until the repeater asks for a row again.
    private readonly Stack<UIElement> _pool = new();

    public static bool IconLoaded => _icon != null;

    public UIElement GetElement(ElementFactoryGetArgs args)
    {
        if (_pool.Count > 0)
        {
            return _pool.Pop();
        }

        CreatedCount++;

        var name = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var detail = new TextBlock
        {
            Text = "Version 1.2.3 · Publisher",
            Opacity = 0.7,
        };
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
        };
        text.Children.Add(name);
        text.Children.Add(detail);

        var image = new Image
        {
            Width = 32,
            Height = 32,
            Stretch = Stretch.Uniform,
            Source = Icon(),
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Height = 48,
            Padding = new Thickness(12, 8, 12, 8),
            Tag = name,
        };
        row.Children.Add(image);
        row.Children.Add(text);
        return row;
    }

    public void RecycleElement(ElementFactoryRecycleArgs args)
    {
        _pool.Push(args.Element);
    }

    private static BitmapImage? Icon()
    {
        if (_icon != null)
        {
            return _icon;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "desk-verve-emblem-32.png");
        if (File.Exists(path))
        {
            // Decode once at 32 px. All rows share this one image.
            _icon = new BitmapImage
            {
                DecodePixelWidth = 32,
                UriSource = new Uri(path, UriKind.Absolute),
            };
        }

        return _icon;
    }
}
