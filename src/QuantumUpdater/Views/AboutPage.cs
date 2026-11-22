using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Models;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

public sealed class AboutPage : StackPanel
{
    private readonly AppServices _services;
    private readonly TextBlock _updateStatus;
    private readonly Button _check;
    private readonly Button _install;

    public AboutPage(AppServices services)
    {
        _services = services;
        Spacing = 8;
        var machine = MachineSummary.Read();
        Children.Add(Chrome.Title("About This PC"));
        Children.Add(Chrome.Card(Chrome.Line(
            $"{machine.Name}\n{machine.Windows}\n{machine.Cpu}\nMemory {machine.Memory}\nStorage {machine.Disk}",
            Theme.Text)));
        var identity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        var logo = BrandAssets.Emblem(40);
        AutomationProperties.SetAccessibilityView(logo, AccessibilityView.Raw);
        identity.Children.Add(logo);
        identity.Children.Add(new TextBlock
        {
            Text = $"DeskVerve {AppVersion.Label} · Quantum Step",
            Foreground = Theme.Muted,
            FontFamily = Chrome.Font,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Children.Add(identity);
        _updateStatus = Chrome.Line("Self-update has not been checked this visit.");
        Children.Add(_updateStatus);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _check = Chrome.Action("Check for DeskVerve updates");
        _check.Click += async (_, _) => await CheckAsync().ConfigureAwait(true);
        _install = Chrome.Action("Install update");
        _install.IsEnabled = false;
        _install.Click += async (_, _) => await OfferInstallAsync().ConfigureAwait(true);
        row.Children.Add(_check);
        row.Children.Add(_install);
        Children.Add(row);
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
            _updateStatus.Foreground = result.HasUpdate ? Theme.Warning : Theme.Healthy;
            _install.IsEnabled = result.HasUpdate;
        }
        catch (Exception ex)
        {
            _updateStatus.Text = "Offline or failed. " + ex.Message;
            _updateStatus.Foreground = Theme.Warning;
            _install.IsEnabled = false;
        }
        finally
        {
            _check.IsEnabled = true;
        }
    }

    private async Task OfferInstallAsync()
    {
        _install.IsEnabled = false;
        var message = await _services.Update.ApplyAsync().ConfigureAwait(true);
        _updateStatus.Text = message;
        _updateStatus.Foreground = Theme.Warning;
    }
}
