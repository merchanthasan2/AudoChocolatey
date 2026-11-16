using System;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Services.Scan;

namespace QuantumUpdater.Views.Controls;

/// <summary>
/// One row of the Getting ready screen. It shows one scan job.
/// Job icon glyphs (Segoe Fluent Icons code, pass one per job):
/// updates E895, installed E71D, marketplace E719, icons E8B9, drives EDA2, version E946.
/// Layout: 00-getting-ready.html .row (grid 32 px, 1 fr, 150 px; min height 64).
/// Call Apply to show a state. WinUI control, UI thread only.
/// </summary>
public sealed class ScanJobRow : Grid
{
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    // mockup: 00-getting-ready.html .bar background #3A3A3A.
    private static readonly SolidColorBrush TrackBrush = new(Windows.UI.Color.FromArgb(255, 0x3A, 0x3A, 0x3A));

    private readonly string _jobId;
    private readonly string _title;
    private string _stateLine = "Waiting";

    // mockup: 00-getting-ready.html .jic (32 px, radius 6, background #333).
    private readonly Border _iconBox = new()
    {
        Width = 32,
        Height = 32,
        CornerRadius = new Microsoft.UI.Xaml.CornerRadius(6),
        Background = Theme.CardHover,
        Margin = new Thickness(16, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    // mockup: 00-getting-ready.html .jic svg (16 px, stroke Muted).
    private readonly FontIcon _icon;

    // mockup: 00-getting-ready.html .nm b (BodySize, semibold).
    private readonly TextBlock _titleText;

    // mockup: 00-getting-ready.html .nm .s (Caption). Colour changes by state.
    private readonly TextBlock _stateText;

    // mockup: 00-getting-ready.html .bar.sm (height 4, margin-top 6).
    private readonly ProgressBar _bar = new()
    {
        Height = 4,
        MinHeight = 0,
        Margin = new Thickness(0, 6, 0, 0),
        Minimum = 0,
        Maximum = 100,
        Background = TrackBrush,
        Foreground = Theme.Accent,
        Visibility = Visibility.Collapsed,
    };

    // mockup: 00-getting-ready.html .spin (12 px, accent). Shows in Working only.
    private readonly ProgressRing _spinner = new()
    {
        Width = 12,
        Height = 12,
        MinWidth = 0,
        MinHeight = 0,
        IsActive = false,
        Foreground = Theme.Accent,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    // The chip is rebuilt in Apply, because Chrome.Chip returns a new Border each call.
    // mockup: 00-getting-ready.html .st (flex, end, gap 6).
    private readonly StackPanel _chipSlot = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = Theme.Gap8,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // mockup: 00-getting-ready.html .ghost "Try again". Plan 46.7.20 asks for SecondaryButton.
    private readonly Button _retry;

    /// <summary>Raised with the job id when the user clicks "Try again".</summary>
    public event Action<string>? RetryRequested;

    /// <summary>Makes a row for one job. Call Apply before the first show.</summary>
    public ScanJobRow(string jobId, string title, string glyph)
    {
        _jobId = jobId;
        _title = title;

        _icon = new FontIcon
        {
            Glyph = glyph,
            FontFamily = IconFont,
            FontSize = 16,
            Foreground = Theme.Muted,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _iconBox.Child = _icon;

        _titleText = new TextBlock
        {
            Text = title,
            FontFamily = Chrome.Font,
            FontSize = Theme.BodySize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        _stateText = Chrome.Caption(_stateLine);

        var textColumn = new StackPanel
        {
            Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        textColumn.Children.Add(_titleText);
        textColumn.Children.Add(_stateText);
        textColumn.Children.Add(_bar);

        var statusColumn = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Theme.Gap8,
            Margin = new Thickness(0, 0, 16, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        statusColumn.Children.Add(_spinner);
        statusColumn.Children.Add(_chipSlot);

        // mockup: 00-getting-ready.html .row "Try again" (Failed only).
        _retry = Chrome.GhostButton("Try again"); // mockup uses a ghost button
        _retry.Visibility = Visibility.Collapsed;
        _retry.Click += (_, _) => RetryRequested?.Invoke(_jobId);
        AutomationProperties.SetName(_retry, "Try again " + title);
        statusColumn.Children.Add(_retry);

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(166) });

        SetColumn(_iconBox, 0);
        SetColumn(textColumn, 1);
        SetColumn(statusColumn, 2);
        Children.Add(_iconBox);
        Children.Add(textColumn);
        Children.Add(statusColumn);

        MinHeight = 64;
        VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(this, title);

        Apply(ScanJobState.Waiting, null, null, null, null, null);
    }

    /// <summary>
    /// Shows one state of the job. Call it again when the state changes. Safe on a recycled row.
    /// </summary>
    /// <param name="state">The job state.</param>
    /// <param name="fraction">Share of work done, 0 to 1. Null when there is no real number.</param>
    /// <param name="summary">One result line, used in Done.</param>
    /// <param name="reason">Plain English reason, used in Failed.</param>
    /// <param name="stepNow">Current step, used with <paramref name="stepCount"/>.</param>
    /// <param name="stepCount">Total steps.</param>
    public void Apply(ScanJobState state, double? fraction, string? summary, string? reason, int? stepNow, int? stepCount)
    {
        _spinner.Visibility = Visibility.Collapsed;
        _spinner.IsActive = false;
        _bar.Visibility = Visibility.Collapsed;
        _bar.IsIndeterminate = false;
        _retry.Visibility = Visibility.Collapsed;
        _stateText.Foreground = Theme.TextSecondary;

        switch (state)
        {
            case ScanJobState.Waiting:
                SetChip("Waiting", ChipKind.Neutral);
                _stateLine = "Waiting";
                break;

            case ScanJobState.Working:
                _spinner.Visibility = Visibility.Visible;
                _spinner.IsActive = true;
                _bar.Visibility = Visibility.Visible;
                _bar.Foreground = Theme.Accent;
                _stateText.Foreground = Theme.AccentText;
                if (fraction is double share)
                {
                    // Never a percent from time. The number comes only from real counts or steps.
                    var clamped = Math.Clamp(share, 0.0, 1.0);
                    _bar.IsIndeterminate = false;
                    _bar.Value = clamped * 100;
                    var percentText = $"{(int)Math.Round(clamped * 100)}%";
                    var hasSteps = stepNow is int && stepCount is int total && total > 0;
                    _stateLine = hasSteps ? $"Step {stepNow} of {stepCount}" : percentText;

                    // mockup: 00-getting-ready.html icons job: a counted job shows its percent in the chip.
                    SetChip(hasSteps ? "Working" : percentText, ChipKind.Accent);
                }
                else
                {
                    // No real number: a moving bar and the word Working.
                    _bar.IsIndeterminate = true;
                    _stateLine = "Working";
                    SetChip("Working", ChipKind.Accent);
                }
                break;

            case ScanJobState.Done:
                // mockup: 00-getting-ready.html#done. The bar is not shown in the finished state.
                SetChip("Done", ChipKind.Ok);
                _stateText.Foreground = Theme.HealthyText;
                _stateLine = string.IsNullOrWhiteSpace(summary) ? "Done" : summary;
                break;

            case ScanJobState.Failed:
                SetChip("Failed", ChipKind.Bad);
                _stateText.Foreground = Theme.CriticalText;
                _stateLine = string.IsNullOrWhiteSpace(reason) ? "Failed" : "Failed · " + reason;
                _retry.Visibility = Visibility.Visible;
                break;
        }

        _stateText.Text = _stateLine;
        AutomationProperties.SetName(this, _title + ", " + _stateLine);
    }

    private void SetChip(string text, ChipKind kind)
    {
        _chipSlot.Children.Clear();
        _chipSlot.Children.Add(Chrome.Chip(text, kind));
    }
}
