using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

public sealed class GettingReadyPage : StackPanel
{
    private readonly AppServices _services;
    private readonly TextBlock _updates;
    private readonly TextBlock _installed;
    private readonly TextBlock _self;
    private bool _started;

    public event Action? Settled;

    public GettingReadyPage(AppServices services)
    {
        _services = services;
        Spacing = 8;
        Children.Add(Chrome.Title("Getting ready"));
        Children.Add(Chrome.Line("Each job is shown. A failed job does not hold the app."));
        _updates = Chrome.Line("Updates scan · waiting");
        _installed = Chrome.Line("Installed scan · waiting");
        _self = Chrome.Line("DeskVerve check · waiting");
        Children.Add(Chrome.Card(_updates));
        Children.Add(Chrome.Card(_installed));
        Children.Add(Chrome.Card(_self));
        Loaded += (_, _) =>
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _ = RunAsync();
        };
    }

    private async Task RunAsync()
    {
        Set(_updates, "Updates scan · running", Theme.Warning);
        Set(_installed, "Installed scan · running", Theme.Warning);
        Set(_self, "DeskVerve check · running", Theme.Warning);

        var upgradesTask = _services.Winget.ListUpgradesAsync();
        var installedTask = _services.Installed.ListInstalledAsync();
        var selfTask = _services.Update.CheckAsync();
        _ = upgradesTask.ContinueWith(_ => CrashLog.Write("Mark:UpgradesDone", null)); // TEMP DIAGNOSTIC
        _ = installedTask.ContinueWith(_ => CrashLog.Write("Mark:InstalledDone", null)); // TEMP DIAGNOSTIC
        _ = selfTask.ContinueWith(_ => CrashLog.Write("Mark:SelfDone", null)); // TEMP DIAGNOSTIC
        await Task.WhenAll(upgradesTask, installedTask, selfTask).ConfigureAwait(true);

        CrashLog.Write("Mark:ScansDone", null); // TEMP DIAGNOSTIC
        var upgrades = await upgradesTask.ConfigureAwait(true);
        var installed = await installedTask.ConfigureAwait(true);
        var self = await selfTask.ConfigureAwait(true);

        _services.Catalog = new CatalogSnapshot
        {
            Upgrades = upgrades,
            UpgradesError = _services.Winget.LastError,
            Installed = installed,
            InstalledError = _services.Installed.LastError,
        };

        Set(
            _updates,
            string.IsNullOrEmpty(_services.Winget.LastError)
                ? $"Updates scan · ready · {upgrades.Count}"
                : "Updates scan · failed · " + _services.Winget.LastError,
            string.IsNullOrEmpty(_services.Winget.LastError) ? Theme.Healthy : Theme.Critical);
        Set(
            _installed,
            string.IsNullOrEmpty(_services.Installed.LastError)
                ? $"Installed scan · ready · {installed.Count}"
                : "Installed scan · failed · " + _services.Installed.LastError,
            string.IsNullOrEmpty(_services.Installed.LastError) ? Theme.Healthy : Theme.Critical);
        Set(_self, "DeskVerve check · " + self.Message, self.HasUpdate ? Theme.Warning : Theme.Healthy);

        try
        {
            await _services.Account.ReportHeartbeatAsync(_services.Session, MachineSummary.Read()).ConfigureAwait(true);
        }
        catch
        {
            // A missed report does not hold the app.
        }

        CrashLog.Write("Mark:Settled", null); // TEMP DIAGNOSTIC
        Settled?.Invoke();
    }

    private static void Set(TextBlock block, string text, Microsoft.UI.Xaml.Media.SolidColorBrush color)
    {
        block.Text = text;
        block.Foreground = color;
    }
}
