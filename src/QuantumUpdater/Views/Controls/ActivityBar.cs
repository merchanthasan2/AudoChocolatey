using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views.Controls;

/// <summary>
/// The activity bar at the bottom of the window. It shows the current item of the queue batch.
/// Layout: 01-updates.html .activity. Hold look: 05-updates-states.html #hold.
/// Bound to an ActionQueue. Hidden when the batch has no open item and no hold is set. UI thread only.
/// </summary>
public sealed class ActivityBar : Grid
{
    private const string DefaultHoldHeadline = "Waiting for an installer to end.";

    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    private readonly ActionQueue _queue;
    private readonly Action<QueuedAction> _onChanged;
    private bool _subscribed;
    private bool _hold;
    private string? _holdText;
    private int? _percent;

    // Top parts. They sit over the top edge of the bar.
    // mockup: 01-updates.html .activity border-top 1px var(--stroke).
    private readonly Border _topStroke = new()
    {
        Height = 1,
        VerticalAlignment = VerticalAlignment.Top,
        Background = Theme.Stroke,
    };

    // mockup: 01-updates.html .activity .line (height 2px, width = percent).
    // mockup: 05-updates-states.html .activity .line (full width, Warning colour) for the hold state.
    private readonly Border _solidLine = new()
    {
        Height = 2,
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalAlignment = HorizontalAlignment.Left,
        Background = Theme.Accent,
        Visibility = Visibility.Collapsed,
    };

    // Moving line for work with no percent. Plan 46.7.8: an indeterminate ProgressBar.
    private readonly ProgressBar _movingLine = new()
    {
        Height = 2,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Top,
        Background = TransparentBrush,
        Foreground = Theme.Accent,
        IsIndeterminate = true,
        Visibility = Visibility.Collapsed,
    };

    // Lead slot: spinner (work) or warning icon (hold). Both sit in the same 18 px cell.
    // mockup: 01-updates.html .spin (12 px). 05-updates-states.html .activity svg (18 px, Warning text colour).
    private readonly ProgressRing _spinner = new()
    {
        Width = 12,
        Height = 12,
        MinWidth = 0,
        MinHeight = 0,
        IsActive = true,
        Foreground = Theme.Accent,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    // Segoe MDL2 "Warning" glyph (E7BA). It is a triangle with a mark, like the mockup icon.
    private readonly FontIcon _warnIcon = new()
    {
        Glyph = "",
        FontFamily = IconFont,
        FontSize = 18,
        Foreground = Theme.WarningText,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    // mockup: 01-updates.html .activity b (semibold headline). Plan 46.7.8: BodySize, semibold.
    private readonly TextBlock _headline = new()
    {
        FontFamily = Chrome.Font,
        FontSize = Theme.BodySize,
        FontWeight = FontWeights.SemiBold,
        Foreground = Theme.Text,
        TextWrapping = TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // mockup: 01-updates.html .activity .cap (Caption, secondary text).
    private readonly TextBlock _detail = new()
    {
        FontFamily = Chrome.Font,
        FontSize = Theme.CaptionSize,
        Foreground = Theme.TextSecondary,
        TextWrapping = TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // mockup: 01-updates.html .ghost "View all" (accent text, no fill).
    private readonly Button _viewAll;

    // mockup: 01-updates.html .btn2 "Cancel all" (card fill, stroke border).
    private readonly Button _cancelAll;

    private readonly Flyout _flyout = new() { Placement = FlyoutPlacementMode.TopEdgeAlignedRight };
    private readonly StackPanel _flyoutList = new() { Spacing = 8, MinWidth = 380 };

    /// <summary>Creates the bar and reads the queue. Events start on Loaded.</summary>
    public ActivityBar(ActionQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _onChanged = OnChanged;

        // mockup: 01-updates.html .activity background var(--sidebar).
        Background = Theme.Sidebar;

        // Content row. mockup: 01-updates.html .activity padding 10px 32px, min-height 52px, gap 14px.
        var lead = new Grid { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
        lead.Children.Add(_spinner);
        lead.Children.Add(_warnIcon);

        var textHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        textHost.Children.Add(_headline);
        textHost.Children.Add(_detail);

        // Only the headline is the live region ("Updating Mozilla Firefox"). The detail changes at each
        // percent tick, so a live detail would make Narrator speak all the time. Polite waits for a pause.
        AutomationProperties.SetLiveSetting(_headline, AutomationLiveSetting.Polite);

        _viewAll = Chrome.GhostButton("View all");
        _viewAll.Flyout = _flyout;
        _viewAll.Padding = new Thickness(8, 6, 8, 6);
        _flyout.Content = _flyoutList;
        _flyout.Opening += (_, _) => RebuildFlyout();

        _cancelAll = Chrome.SecondaryButton("Cancel all");
        _cancelAll.Click += (_, _) => CancelAll();

        var row = new Grid
        {
            Padding = new Thickness(32, 10, 32, 10),
            MinHeight = 52,
            ColumnSpacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(lead, 0);
        row.Children.Add(lead);
        Grid.SetColumn(textHost, 1);
        row.Children.Add(textHost);
        Grid.SetColumn(_viewAll, 2);
        row.Children.Add(_viewAll);
        Grid.SetColumn(_cancelAll, 3);
        row.Children.Add(_cancelAll);

        // Row content first, then the top parts on top of it.
        Children.Add(row);
        Children.Add(_topStroke);
        Children.Add(_solidLine);
        Children.Add(_movingLine);

        SizeChanged += (_, _) => ApplyLineWidth();
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();

        Refresh();
    }

    /// <summary>
    /// Shows or hides the hold look (D4, W18). When hold is true, the bar stays visible with a warning look.
    /// The text is the headline. Null or empty uses a short default. UI thread only.
    /// </summary>
    public void SetHold(bool hold, string? text)
    {
        _hold = hold;
        _holdText = text;
        Refresh();
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        _queue.Changed += _onChanged;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _subscribed = false;
        _queue.Changed -= _onChanged;
    }

    private void OnChanged(QueuedAction item)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            Refresh();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => Refresh());
        }
    }

    private void Refresh()
    {
        IReadOnlyList<QueuedAction> batch = _queue.CurrentBatch;
        ActivitySummary summary = ActivitySummary.From(batch);

        Visibility = summary.Visible || _hold ? Visibility.Visible : Visibility.Collapsed;

        // Mockup 05 #hold shows only "View all" while the queue is on hold.
        _cancelAll.Visibility = _hold ? Visibility.Collapsed : Visibility.Visible;

        if (_hold)
        {
            ShowHold(batch.Count(i => i.State == ActionState.Waiting));
        }
        else
        {
            ShowSummary(summary);
        }

        if (_flyout.IsOpen)
        {
            RebuildFlyout();
        }
    }

    // Tells screen readers about a new headline. Only when the text really changes, not at each percent tick.
    private void SetHeadline(string text)
    {
        if (_headline.Text == text)
        {
            return;
        }

        _headline.Text = text;
        if (text.Length > 0)
        {
            FrameworkElementAutomationPeer.FromElement(_headline)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    private void ShowSummary(ActivitySummary summary)
    {
        _warnIcon.Visibility = Visibility.Collapsed;
        _spinner.IsActive = summary.Visible;
        _spinner.Visibility = summary.Visible ? Visibility.Visible : Visibility.Collapsed;

        SetHeadline(summary.Headline);
        _headline.Foreground = Theme.Text;
        _detail.Text = summary.Visible ? "· " + summary.Detail : "";

        _percent = summary.Percent;
        _solidLine.Background = Theme.Accent;

        bool hasPercent = summary.Visible && summary.Percent is not null;
        _solidLine.Visibility = hasPercent ? Visibility.Visible : Visibility.Collapsed;
        _movingLine.Visibility = summary.Visible && !hasPercent ? Visibility.Visible : Visibility.Collapsed;
        ApplyLineWidth();
    }

    private void ShowHold(int waiting)
    {
        _spinner.IsActive = false;
        _spinner.Visibility = Visibility.Collapsed;
        _warnIcon.Visibility = Visibility.Visible;

        SetHeadline(string.IsNullOrWhiteSpace(_holdText) ? DefaultHoldHeadline : _holdText.Trim());
        _headline.Foreground = Theme.Text;

        // mockup: 05-updates-states.html #hold, footer text. Count matches the mockup "2 waiting".
        _detail.Text = waiting > 0
            ? $"· The next action starts when the installer ends · {waiting} waiting"
            : "· The next action starts when the installer ends";

        // mockup: 05-updates-states.html .activity .line (full width, Warning colour, no percent).
        _percent = null;
        _solidLine.Background = Theme.Warning;
        _solidLine.Visibility = Visibility.Visible;
        _movingLine.Visibility = Visibility.Collapsed;
        ApplyLineWidth();
    }

    // The line width is a share of the bar width: Percent of the width, or the full width in hold.
    private void ApplyLineWidth()
    {
        double width = _hold
            ? ActualWidth
            : _percent is int p ? ActualWidth * Math.Clamp(p, 0, 100) / 100.0 : 0;
        _solidLine.Width = Math.Max(0, width);
    }

    private void RebuildFlyout()
    {
        _flyoutList.Children.Clear();
        foreach (QueuedAction item in _queue.CurrentBatch)
        {
            _flyoutList.Children.Add(BuildFlyoutLine(item));
        }
    }

    // One line per batch item: title, state text, and Cancel for a Waiting or Working item.
    private Grid BuildFlyoutLine(QueuedAction item)
    {
        var line = new Grid { ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = item.Title,
            FontFamily = Chrome.Font,
            FontSize = Theme.BodySize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 0);
        line.Children.Add(title);

        TextBlock state = Chrome.Caption(StateText(item));
        state.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(state, 1);
        line.Children.Add(state);

        if (item.State is ActionState.Waiting or ActionState.Working)
        {
            Guid id = item.Id;
            Button cancel = Chrome.GhostButton("Cancel");
            AutomationProperties.SetName(cancel, $"Cancel {item.Title}");
            cancel.Click += (_, _) => _queue.Cancel(id);
            Grid.SetColumn(cancel, 2);
            line.Children.Add(cancel);
        }

        return line;
    }

    private void CancelAll()
    {
        // CurrentBatch is a copy, so the loop is safe while the queue changes.
        foreach (QueuedAction item in _queue.CurrentBatch)
        {
            if (item.State is ActionState.Waiting or ActionState.Working)
            {
                _queue.Cancel(item.Id);
            }
        }
    }

    private static string StateText(QueuedAction item)
    {
        string phase = string.IsNullOrWhiteSpace(item.Phase) ? "Working" : item.Phase!.Trim();
        return item.State switch
        {
            ActionState.Waiting => "Waiting",
            ActionState.Working => item.Percent is int p ? $"{phase} · {p}%" : phase,
            ActionState.Done => "Done",
            ActionState.Failed => "Failed",
            ActionState.Cancelled => "Cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(item), item.State, "Unknown action state."),
        };
    }
}
