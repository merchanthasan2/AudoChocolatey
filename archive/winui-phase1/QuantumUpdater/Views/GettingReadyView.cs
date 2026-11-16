using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

/// <summary>Getting ready — startup catalog scans (Phase 44.2).</summary>
public sealed class GettingReadyView : StackPanel
{
    private readonly AppServices _services;
    private readonly TextBlock _catalogUpdatesStatus;
    private readonly TextBlock _catalogInstalledStatus;
    private bool _scanStarted;

    public GettingReadyView(AppServices services)
    {
        _services = services;
        Spacing = 12;

        Children.Add(new TextBlock
        {
            Text = "Getting ready",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        Children.Add(new TextBlock
        {
            Text = "Startup checklist before updates run.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.9,
        });

        var checklist = new StackPanel { Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
        _catalogUpdatesStatus = MakeStatusLine("Catalog updates", "Pending");
        _catalogInstalledStatus = MakeStatusLine("Catalog installed", "Pending");
        checklist.Children.Add(_catalogUpdatesStatus);
        checklist.Children.Add(_catalogInstalledStatus);
        Children.Add(checklist);

        Loaded += OnLoaded;
    }

    /// <summary>Raised once when both catalog scans have finished (success or failure).</summary>
    public event Action? BothScansSettled;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_scanStarted)
        {
            return;
        }

        _scanStarted = true;
        _ = RunStartupScansAsync();
    }

    private async Task RunStartupScansAsync()
    {
        SetLine(_catalogUpdatesStatus, "Catalog updates", "Running");
        SetLine(_catalogInstalledStatus, "Catalog installed", "Running");

        var updatesFailed = false;
        var installedFailed = false;
        string? updatesFailureDetail;
        string? installedFailureDetail;

        try
        {
            await Task.Run(async () =>
            {
                var upgradesTask = _services.Winget.ListUpgradesAsync();
                var installedTask = _services.Installed.ListInstalledAsync();
                await Task.WhenAll(upgradesTask, installedTask).ConfigureAwait(false);
            }).ConfigureAwait(true);

            updatesFailed = !string.IsNullOrEmpty(_services.Winget.LastError);
            installedFailed = !string.IsNullOrEmpty(_services.Installed.LastError);
            updatesFailureDetail = _services.Winget.LastError;
            installedFailureDetail = _services.Installed.LastError;
        }
        catch (Exception ex)
        {
            updatesFailed = true;
            installedFailed = true;
            updatesFailureDetail = ex.Message;
            installedFailureDetail = ex.Message;
        }

        SetLine(
            _catalogUpdatesStatus,
            "Catalog updates",
            updatesFailed
                ? $"Failed — {updatesFailureDetail ?? "Unknown error"}"
                : "Ready");

        SetLine(
            _catalogInstalledStatus,
            "Catalog installed",
            installedFailed
                ? $"Failed — {installedFailureDetail ?? "Unknown error"}"
                : "Ready");

        BothScansSettled?.Invoke();
    }

    private static TextBlock MakeStatusLine(string label, string state)
    {
        return new TextBlock
        {
            Text = FormatLine(label, state),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private static void SetLine(TextBlock block, string label, string state)
    {
        block.Text = FormatLine(label, state);
    }

    private static string FormatLine(string label, string state) => $"{label}: {state}";
}