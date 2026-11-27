using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace QuantumUpdater.Views.Controls;

/// <summary>What the user did on a row.</summary>
public enum RowAction { Update, Cancel, Retry, Details, Select }

/// <summary>
/// One app row of the Updates list. Call Apply to show a model. Safe to call many times on a recycled row.
/// Layout: 01-updates.html .row and 05-updates-states.html .row. WinUI control, UI thread only.
/// </summary>
public sealed class AppRow : Grid
{
    private static readonly SolidColorBrush TrackBrush = new(Windows.UI.Color.FromArgb(255, 0x3A, 0x3A, 0x3A));
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    private string _key = "";

    private readonly CheckBox _select = new()
    {
        MinWidth = 0,
        Padding = new Thickness(0),
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    private readonly AppIcon _icon = new(32);
    private readonly TextBlock _nameText = new();
    private readonly TextBlock _publisher;
    private readonly ProgressBar _bar;
    private readonly TextBlock _status;
    private readonly TextBlock _version;
    private readonly StackPanel _sourceHost = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _actions = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly Button _update;
    private readonly Button _cancelWaiting;
    private readonly Button _cancelWorking;
    private readonly Button _details;
    private readonly Button _retry;
    private readonly Border _doneChip;
    private readonly StackPanel _waitingChipHost = new() { VerticalAlignment = VerticalAlignment.Center };

    // Row 0: top line, 1 px Stroke. Border is sealed, so the row is a Grid with a line on top.
    private readonly RowDefinition _topRow = new() { Height = new GridLength(1) };
    private readonly Border _topLine = new() { Height = 1, Background = Theme.Stroke };
    private bool _showTopLine = true;

    /// <summary>Raised with the row key and the action. Select is raised when the check box changes.</summary>
    public event Action<string, RowAction>? Action;

    /// <summary>True when the check box is ticked. Read it after a Select event.</summary>
    public bool IsChecked => _select.IsChecked == true;

    /// <summary>The row's app icon control, for binding original icons after <see cref="Apply"/>.</summary>
    public AppIcon Icon => _icon;

    /// <summary>
    /// Shows or hides the 1 px top line. The list hides it on the first row.
    /// mockup: 01-updates.html .row:first-child (border-top 0).
    /// </summary>
    public bool ShowTopLine
    {
        get => _showTopLine;
        set
        {
            _showTopLine = value;
            _topRow.Height = value ? new GridLength(1) : new GridLength(0);
            _topLine.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public AppRow()
    {
        // mockup: 01-updates.html .row (border-top 1px var(--stroke)).
        RowDefinitions.Add(_topRow);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_topLine, 0);
        Children.Add(_topLine);

        PointerEntered += (_, _) => Background = Theme.CardHover;   // mockup: .row:hover
        PointerExited += (_, _) => Background = null;

        // Row 1: the row content. mockup: 01-updates.html .row (padding 10px 16px, min-height 60px).
        var grid = new Grid
        {
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(16, 10, 16, 10),
            MinHeight = 60,
        };
        Grid.SetRow(grid, 1);
        // mockup: 01-updates.html .row grid-template-columns 18px 32px minmax(200px,1fr) 210px 140px 170px (plan 46.7.5, 05 uses 170 too).
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });

        // Column 0: select box.
        // Click fires only on a user change, never from Apply. The new value is in IsChecked.
        _select.Click += (_, _) => Raise(RowAction.Select);
        Grid.SetColumn(_select, 0);
        grid.Children.Add(_select);

        // Column 1: icon.
        Grid.SetColumn(_icon, 1);
        grid.Children.Add(_icon);

        // Column 2: name, publisher, progress bar, status text.
        _nameText.FontFamily = Chrome.Font;
        _nameText.FontSize = Theme.BodySize;
        _nameText.FontWeight = FontWeights.SemiBold;
        _nameText.Foreground = Theme.Text;
        _nameText.TextWrapping = TextWrapping.NoWrap;
        _nameText.TextTrimming = TextTrimming.CharacterEllipsis;

        _publisher = Chrome.Caption("");
        _publisher.TextTrimming = TextTrimming.CharacterEllipsis;
        _publisher.TextWrapping = TextWrapping.NoWrap;

        // mockup: 01-updates.html .bar (height 4px, #3A3A3A track, margin-top 6px).
        _bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 4,
            Background = TrackBrush,
            Foreground = Theme.Accent,
            Margin = new Thickness(0, 6, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        // mockup: 01-updates.html .prog and .err (12px, margin-top 2px).
        _status = new TextBlock
        {
            FontFamily = Chrome.Font,
            FontSize = Theme.CaptionSize,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        var nameColumn = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nameColumn.Children.Add(_nameText);
        nameColumn.Children.Add(_publisher);
        nameColumn.Children.Add(_bar);
        nameColumn.Children.Add(_status);
        Grid.SetColumn(nameColumn, 2);
        grid.Children.Add(nameColumn);

        // Column 3: version, mono font, 12 px. mockup: 01-updates.html .ver.
        _version = new TextBlock
        {
            FontFamily = MonoFont,
            FontSize = Theme.CaptionSize,
            Foreground = Theme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        Grid.SetColumn(_version, 3);
        grid.Children.Add(_version);

        // Column 4: source chip (filled by Apply).
        Grid.SetColumn(_sourceHost, 4);
        grid.Children.Add(_sourceHost);

        // Column 5: action area. mockup: 01-updates.html .act (flex-end, gap 6px).
        _update = Chrome.Action("Update");
        // mockup: 01-updates.html .btn (padding 7px 14px, weight 600).
        _update.Padding = new Thickness(14, 7, 14, 7);
        _update.FontWeight = FontWeights.SemiBold;
        _update.Click += (_, _) => Raise(RowAction.Update);

        _cancelWaiting = Chrome.GhostButton("Cancel");
        _cancelWaiting.Click += (_, _) => Raise(RowAction.Cancel);

        _cancelWorking = Chrome.SecondaryButton("Cancel");
        _cancelWorking.Click += (_, _) => Raise(RowAction.Cancel);

        _details = Chrome.GhostButton("Details");
        _details.Click += (_, _) => Raise(RowAction.Details);

        _retry = Chrome.SecondaryButton("Try again");
        _retry.Click += (_, _) => Raise(RowAction.Retry);

        _doneChip = Chrome.Chip("Done", ChipKind.Ok);

        _actions.Children.Add(_update);
        _actions.Children.Add(_waitingChipHost);
        _actions.Children.Add(_cancelWaiting);
        _actions.Children.Add(_cancelWorking);
        _actions.Children.Add(_details);
        _actions.Children.Add(_retry);
        _actions.Children.Add(_doneChip);
        Grid.SetColumn(_actions, 5);
        grid.Children.Add(_actions);

        Children.Add(grid);
    }

    /// <summary>Sets every part of the row from the model. Safe to call many times.</summary>
    public void Apply(AppRowModel model)
    {
        _key = model.Key;

        _icon.Show(model.Key, model.Name);

        // Select box. Setting IsChecked does not raise Click, so no loop.
        _select.Visibility = model.CanSelect ? Visibility.Visible : Visibility.Collapsed;
        _select.IsChecked = model.IsSelected;
        AutomationProperties.SetName(_select, "Select " + model.Name);

        _nameText.Text = model.Name;

        _publisher.Text = model.Publisher ?? "";
        _publisher.Visibility = string.IsNullOrEmpty(model.Publisher) ? Visibility.Collapsed : Visibility.Visible;

        ApplyVersion(model);

        _sourceHost.Children.Clear();
        if (!string.IsNullOrEmpty(model.SourceLabel))
        {
            _sourceHost.Children.Add(Chrome.Chip(model.SourceLabel, ChipKind.Neutral));
        }

        ApplyStatus(model);
        ApplyActions(model);
    }

    /// <summary>
    /// Version column. Ready and other rows: "from → to" with the new version in AccentText and "Not known" in italics.
    /// Updated: "Updated to ... · time" in HealthyText. mockup: 01-updates.html .ver and .done.
    /// </summary>
    private void ApplyVersion(AppRowModel model)
    {
        _version.Inlines.Clear();

        if (model.State == AppRowState.Updated)
        {
            _version.Inlines.Add(new Run { Text = model.StatusText(), Foreground = Theme.HealthyText });
            return;
        }

        var fromUnknown = string.IsNullOrEmpty(model.FromVersion);
        _version.Inlines.Add(new Run
        {
            Text = fromUnknown ? "Not known" : model.FromVersion!,
            Foreground = fromUnknown ? Theme.TextSecondary : Theme.Muted,
            FontStyle = fromUnknown ? FontStyle.Italic : FontStyle.Normal,
        });

        if (model.ToVersion is null)
        {
            return;
        }

        // mockup: 01-updates.html .ver svg (arrow) and .ver .to (AccentText, weight 600).
        _version.Inlines.Add(new Run { Text = " → ", Foreground = Theme.Muted });
        _version.Inlines.Add(new Run
        {
            Text = model.ToVersion,
            Foreground = Theme.AccentText,
            FontWeight = FontWeights.SemiBold,
        });
    }

    private void ApplyStatus(AppRowModel model)
    {
        // Waiting shows its status as a chip in the action area. Updated shows it in the version column.
        var text = model.StatusText();
        var underName = model.State is not (AppRowState.Waiting or AppRowState.Updated);
        _status.Text = text;
        _status.Visibility = underName && !string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        _status.Foreground = model.State switch
        {
            AppRowState.Working => Theme.AccentText,
            AppRowState.Waiting => Theme.WarningText,
            AppRowState.Stopping => Theme.WarningText,
            AppRowState.Updated => Theme.HealthyText,
            AppRowState.Failed => Theme.CriticalText,
            _ => Theme.Muted,
        };

        // Progress bar. Working with a percent: value. Working without one, and Stopping: moving bar.
        // mockup: 05-updates-states.html .bar.ind (Stopping shows the moving bar in the warning colour).
        switch (model.State)
        {
            case AppRowState.Working when model.Percent.HasValue:
                _bar.Visibility = Visibility.Visible;
                _bar.IsIndeterminate = false;
                _bar.Foreground = Theme.Accent;
                _bar.Value = Math.Clamp(model.Percent.Value, 0, 100);
                break;

            case AppRowState.Working:
                _bar.Visibility = Visibility.Visible;
                _bar.IsIndeterminate = true;
                _bar.Foreground = Theme.Accent;
                break;

            case AppRowState.Stopping:
                _bar.Visibility = Visibility.Visible;
                _bar.IsIndeterminate = true;
                _bar.Foreground = Theme.Warning;
                break;

            default:
                _bar.Visibility = Visibility.Collapsed;
                _bar.IsIndeterminate = false;
                break;
        }
    }

    private void ApplyActions(AppRowModel model)
    {
        _update.Visibility = Visibility.Collapsed;
        _cancelWaiting.Visibility = Visibility.Collapsed;
        _cancelWorking.Visibility = Visibility.Collapsed;
        _details.Visibility = Visibility.Collapsed;
        _retry.Visibility = Visibility.Collapsed;
        _doneChip.Visibility = Visibility.Collapsed;
        _waitingChipHost.Children.Clear();

        switch (model.State)
        {
            case AppRowState.Ready:
                _update.Content = string.IsNullOrWhiteSpace(model.ActionLabel) ? "Update" : model.ActionLabel;
                _update.Visibility = Visibility.Visible;
                break;

            case AppRowState.Waiting:
                // mockup: 05-updates-states.html .chip.warn (Waiting · next), then the ghost Cancel.
                _waitingChipHost.Children.Add(Chrome.Chip(model.StatusText(), ChipKind.Warn));
                _cancelWaiting.Visibility = Visibility.Visible;
                break;

            case AppRowState.Working:
                _cancelWorking.Visibility = Visibility.Visible;
                break;

            case AppRowState.Updated:
                _doneChip.Visibility = Visibility.Visible;
                break;

            case AppRowState.Failed:
                _details.Visibility = Visibility.Visible;
                _retry.Visibility = Visibility.Visible;
                break;

            // Stopping: no button. The status line and the moving bar show the state.
        }

        AutomationProperties.SetName(_update, "Update " + model.Name);
        AutomationProperties.SetName(_cancelWaiting, "Cancel " + model.Name);
        AutomationProperties.SetName(_cancelWorking, "Cancel " + model.Name);
        AutomationProperties.SetName(_details, "Details for " + model.Name);
        AutomationProperties.SetName(_retry, "Try again " + model.Name);
    }

    private void Raise(RowAction action)
    {
        Action?.Invoke(_key, action);
    }
}
