using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Models;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

/// <summary>
/// About This PC &amp; DeskVerve Settings view with machine summary, fixed drives,
/// Start with Windows switch (49.1), and self-update check.
/// </summary>
public sealed class AboutPage : UserControl
{
    private readonly AppServices _services;
    private readonly TextBlock _updateStatus;
    private readonly Button _check;
    private readonly ToggleSwitch _startWithWindows;
    private readonly TextBlock _startupStatus;

    public AboutPage(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Background = Theme.Canvas;

        var stack = new StackPanel { Spacing = 16, Padding = new Thickness(0, 0, 8, 24) };
        stack.Children.Add(Chrome.Title("About This PC"));

        // 1. Hardware & Windows card
        var machine = MachineSummary.Read();
        var hwGrid = new Grid { RowSpacing = 8, ColumnSpacing = 16 };
        hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddFact(hwGrid, 0, "Computer", machine.Name);
        AddFact(hwGrid, 1, "Operating system", machine.Windows);
        AddFact(hwGrid, 2, "Processor", machine.Cpu);
        AddFact(hwGrid, 3, "Memory", machine.Memory);
        AddFact(hwGrid, 4, "Storage", machine.Disk);
        stack.Children.Add(Chrome.Card(hwGrid));

        // 2. Fixed volumes card
        var volumes = _services.Disk.Volumes();
        if (volumes.Count > 0)
        {
            var volStack = new StackPanel { Spacing = 8 };
            volStack.Children.Add(new TextBlock
            {
                Text = "Fixed drives",
                FontFamily = Chrome.Font,
                FontSize = Theme.SectionSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
            });
            foreach (var vol in volumes)
            {
                double totalGb = vol.Total / (1024.0 * 1024 * 1024);
                double freeGb = vol.Free / (1024.0 * 1024 * 1024);
                double usedPct = Math.Clamp((vol.Total - vol.Free) * 100.0 / vol.Total, 0, 100);

                var row = new StackPanel { Spacing = 4 };
                row.Children.Add(Chrome.Line($"{vol.Name.TrimEnd('\\')}   ·   {freeGb:0.#} GB free of {totalGb:0.#} GB", Theme.Text));
                row.Children.Add(new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = usedPct,
                    Height = 6,
                    Foreground = usedPct > 90 ? Theme.Warning : Theme.Accent,
                });
                volStack.Children.Add(row);
            }
            stack.Children.Add(Chrome.Card(volStack));
        }

        // 3. Start with Windows card (49.1)
        _startWithWindows = new ToggleSwitch
        {
            Header = "Start with Windows",
            OnContent = "On",
            OffContent = "Off",
            Foreground = Theme.Text,
            FontFamily = Chrome.Font,
            IsOn = _services.StartWithWindows.IsEnabled(),
        };
        _startupStatus = Chrome.Caption("Runs DeskVerve quietly when you sign in to Windows.");
        _startWithWindows.Toggled += async (_, _) => await ToggleStartWithWindowsAsync().ConfigureAwait(true);

        var startCard = new StackPanel { Spacing = 6 };
        startCard.Children.Add(_startWithWindows);
        startCard.Children.Add(_startupStatus);
        stack.Children.Add(Chrome.Card(startCard));

        // 4. DeskVerve identity & updates card
        var identity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };
        var logo = BrandAssets.Emblem(40);
        AutomationProperties.SetAccessibilityView(logo, AccessibilityView.Raw);
        identity.Children.Add(logo);
        var verStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        verStack.Children.Add(new TextBlock
        {
            Text = $"DeskVerve {AppVersion.Label}",
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        });
        verStack.Children.Add(Chrome.Caption("Quantum Step · Windows App SDK 1.8"));
        identity.Children.Add(verStack);

        _updateStatus = Chrome.Line("Check for newer DeskVerve releases on GitHub.");
        _check = Chrome.Action("Check for DeskVerve updates");
        _check.HorizontalAlignment = HorizontalAlignment.Left;
        _check.Click += async (_, _) => await CheckAsync().ConfigureAwait(true);

        var appStack = new StackPanel { Spacing = 10 };
        appStack.Children.Add(identity);
        appStack.Children.Add(_updateStatus);
        appStack.Children.Add(_check);
        stack.Children.Add(Chrome.Card(appStack));

        Content = new ScrollViewer { Background = Theme.Canvas, Content = stack };
    }

    private async Task ToggleStartWithWindowsAsync()
    {
        bool target = _startWithWindows.IsOn;
        _startWithWindows.IsEnabled = false;
        try
        {
            await Task.Run(() =>
            {
                if (target)
                {
                    _services.StartWithWindows.Enable();
                }
                else
                {
                    _services.StartWithWindows.Disable();
                }
            }).ConfigureAwait(true);
            _startupStatus.Text = target ? "Enabled via Windows scheduled task." : "Disabled.";
            _startupStatus.Foreground = Theme.HealthyText;
        }
        catch (Exception ex)
        {
            _startupStatus.Text = ex.Message;
            _startupStatus.Foreground = Theme.CriticalText;
            _startWithWindows.IsOn = _services.StartWithWindows.IsEnabled();
        }
        finally
        {
            _startWithWindows.IsEnabled = true;
        }
    }

    private async Task CheckAsync()
    {
        _check.IsEnabled = false;
        _updateStatus.Text = "Checking the official release…";
        _updateStatus.Foreground = Theme.Muted;
        try
        {
            var result = await _services.Update.CheckAsync().ConfigureAwait(true);
            _updateStatus.Text = result.Message;
            _updateStatus.Foreground = result.HasUpdate ? Theme.WarningText : Theme.HealthyText;
        }
        catch (Exception ex)
        {
            _updateStatus.Text = "Offline or failed. " + ex.Message;
            _updateStatus.Foreground = Theme.WarningText;
        }
        finally
        {
            _check.IsEnabled = true;
        }
    }

    private static void AddFact(Grid grid, int row, string label, string value)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var l = Chrome.Caption(label);
        l.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(l, row);
        Grid.SetColumn(l, 0);
        grid.Children.Add(l);

        var v = Chrome.Line(value, Theme.Text);
        Grid.SetRow(v, row);
        Grid.SetColumn(v, 1);
        grid.Children.Add(v);
    }
}
