using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Scan;
using QuantumUpdater.Services.Scan.Jobs;
using QuantumUpdater.Views.Controls;

namespace QuantumUpdater.Views;

/// <summary>
/// Full parallel system scan shown at startup (ADR 049 Q7, mockup 00-getting-ready.html).
/// Unlocks the sidebar once every job reaches Done or Failed.
/// </summary>
public sealed class GettingReadyPage : UserControl
{
    private static readonly SolidColorBrush TrackBrush = new(Windows.UI.Color.FromArgb(255, 0x3A, 0x3A, 0x3A));

    private readonly AppServices _services;
    private readonly ScanOrchestrator _orchestrator;
    private readonly Dictionary<string, ScanJobRow> _rows = new(StringComparer.Ordinal);
    private readonly TextBlock _heading;
    private readonly TextBlock _lead;
    private readonly TextBlock _percentText;
    private readonly TextBlock _countText;
    private readonly TextBlock _elapsedText;
    private readonly ProgressBar _overallBar;
    private readonly Button _continue;
    private readonly DispatcherTimer _timer;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private bool _started;

    /// <summary>Raised once every scan job has reached Done or Failed. Bool = true if all succeeded.</summary>
    public event Action<bool>? Settled;

    public GettingReadyPage(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Background = Theme.Canvas;

        var jobs = new IScanJob[]
        {
            new UpdatesScanJob(_services.Updates),
            new InstalledScanJob(_services.InstalledApps),
            new MarketplaceScanJob(_services.Catalog),
            new IconScanJob(_services.InstalledApps, _services.Catalog, _services.Icons),
            new DriveScanJob(_services.Disk),
            new SelfUpdateScanJob(_services.Update),
        };

        _orchestrator = new ScanOrchestrator(jobs, _services.Post);
        _orchestrator.Changed += OnJobChanged;
        _orchestrator.Completed += OnAllCompleted;

        var panel = new StackPanel
        {
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 16, 0, 32),
            Spacing = 12,
        };

        _heading = Chrome.Title("Getting your PC ready");
        _lead = Chrome.Line("DeskVerve checks your apps, updates, icons, and drives before you start.");
        panel.Children.Add(_heading);
        panel.Children.Add(_lead);

        // Overall progress card
        _percentText = new TextBlock
        {
            Text = "0%",
            FontFamily = Chrome.Font,
            FontSize = Theme.TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        };
        _countText = new TextBlock
        {
            Text = $"0 of {jobs.Length} checks done",
            FontFamily = Chrome.Font,
            FontSize = Theme.BodySize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 3),
        };
        _elapsedText = Chrome.Caption("0.0 s");
        _elapsedText.VerticalAlignment = VerticalAlignment.Bottom;
        _elapsedText.Margin = new Thickness(0, 0, 0, 4);

        var topRow = new Grid { ColumnSpacing = 12 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_percentText, 0);
        Grid.SetColumn(_countText, 1);
        Grid.SetColumn(_elapsedText, 2);
        topRow.Children.Add(_percentText);
        topRow.Children.Add(_countText);
        topRow.Children.Add(_elapsedText);

        _overallBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 6,
            Background = TrackBrush,
            Foreground = Theme.Accent,
        };

        var overallStack = new StackPanel { Spacing = 10 };
        overallStack.Children.Add(topRow);
        overallStack.Children.Add(_overallBar);
        panel.Children.Add(Chrome.Card(overallStack));

        // Job list card
        var jobList = new StackPanel { Spacing = 0 };
        var glyphMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["updates"] = "\uE895",
            ["installed"] = "\uE71D",
            ["marketplace"] = "\uE719",
            ["icons"] = "\uE8B9",
            ["drives"] = "\uEDA2",
            ["self"] = "\uE946",
        };

        for (int i = 0; i < jobs.Length; i++)
        {
            if (i > 0)
            {
                jobList.Children.Add(new Border { Height = 1, Background = Theme.Stroke });
            }

            var job = jobs[i];
            var row = new ScanJobRow(job.Id, job.Title, glyphMap.GetValueOrDefault(job.Id, "\uE946"));
            row.RetryRequested += async id => await _orchestrator.RetryAsync(id, CancellationToken.None).ConfigureAwait(true);
            _rows[job.Id] = row;
            jobList.Children.Add(row);
        }

        panel.Children.Add(new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = Theme.RadiusLarge,
            Child = jobList,
        });

        _continue = Chrome.Action("Continue");
        _continue.HorizontalAlignment = HorizontalAlignment.Right;
        _continue.Visibility = Visibility.Collapsed;
        _continue.Click += (_, _) => Settled?.Invoke(true);
        panel.Children.Add(_continue);

        Content = new ScrollViewer { Background = Theme.Canvas, Content = panel };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) =>
        {
            var secs = (DateTimeOffset.UtcNow - _startedAt).TotalSeconds;
            _elapsedText.Text = $"{secs:0.1} s";
        };

        Loaded += (_, _) =>
        {
            if (_started)
            {
                return;
            }

            _started = true;
            if (DiagSwitches.Has("noscan"))
            {
                _heading.Text = "Scan skipped (diagnostic)";
                _continue.Visibility = Visibility.Visible;
                return;
            }

            _timer.Start();
            _ = _orchestrator.RunAsync(CancellationToken.None);
        };
    }

    private void OnJobChanged(ScanJobStatus status)
    {
        if (_rows.TryGetValue(status.Id, out var row))
        {
            row.Apply(status.State, status.Fraction, status.Summary, status.Reason, null, null);
        }

        var pct = (int)Math.Round(_orchestrator.OverallPercent);
        _percentText.Text = $"{pct}%";
        _overallBar.Value = pct;

        var all = _orchestrator.Jobs;
        int finished = all.Count(j => j.State is ScanJobState.Done or ScanJobState.Failed);
        _countText.Text = finished == all.Count
            ? $"{finished} of {all.Count} checks finished"
            : $"{finished} of {all.Count} checks done";
    }

    private void OnAllCompleted()
    {
        _timer.Stop();
        var elapsed = (DateTimeOffset.UtcNow - _startedAt).TotalSeconds;
        _elapsedText.Text = $"{elapsed:0.1} s";

        var all = _orchestrator.Jobs;
        bool allOk = all.All(j => j.State == ScanJobState.Done);
        _percentText.Text = "100%";
        _overallBar.Value = 100;
        _overallBar.Foreground = allOk ? Theme.Healthy : Theme.Warning;
        _heading.Text = "Your PC is ready";
        _lead.Text = allOk
            ? $"The scan finished in {elapsed:0.1} seconds."
            : $"The scan finished in {elapsed:0.1} seconds. One or more checks did not finish; you can still use DeskVerve.";

        _continue.Visibility = Visibility.Visible;
        AutomationProperties.SetName(_heading, _heading.Text);

        _ = Task.Run(async () =>
        {
            try
            {
                await _services.Account.ReportHeartbeatAsync(_services.Session, MachineSummary.Read()).ConfigureAwait(false);
            }
            catch
            {
                // Ignore offline heartbeat failure.
            }
        });

        Settled?.Invoke(allOk);
    }
}
