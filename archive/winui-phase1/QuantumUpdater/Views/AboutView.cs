using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Models;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

/// <summary>About page with version and update check (Phase 44.3).</summary>
public sealed class AboutView : StackPanel
{
    private readonly AppServices _services;
    private readonly Button _checkButton;
    private readonly TextBlock _statusText;

    public AboutView(AppServices services)
    {
        _services = services;
        Spacing = 12;

        Children.Add(new TextBlock
        {
            Text = "About",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        Children.Add(new TextBlock
        {
            Text = $"Quantum Updater {AppVersion.Label}",
            FontSize = 16,
        });

        Children.Add(new TextBlock
        {
            Text = "Publisher: Quantum Step",
            FontSize = 14,
            Opacity = 0.9,
        });

        _checkButton = new Button
        {
            Content = "Check for updates",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0),
        };
        _checkButton.Click += OnCheckClick;
        Children.Add(_checkButton);

        _statusText = new TextBlock
        {
            Text = "Check GitHub Releases for a newer build.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.9,
        };
        Children.Add(_statusText);
    }

    private async void OnCheckClick(object sender, RoutedEventArgs e)
    {
        _checkButton.IsEnabled = false;
        _statusText.Text = "Checking for updates…";

        try
        {
            var result = await _services.Update.CheckAsync().ConfigureAwait(true);
            _statusText.Text = result.Message;

            if (result.HasUpdate && !string.IsNullOrEmpty(result.ZipUrl))
            {
                _statusText.Text += $"\nLatest tag: {result.LatestTag}";
            }
            else if (!string.IsNullOrEmpty(result.LatestTag))
            {
                _statusText.Text += $"\nLatest release tag: {result.LatestTag}";
            }
        }
        catch (Exception ex)
        {
            _statusText.Text = ex.Message;
        }
        finally
        {
            _checkButton.IsEnabled = true;
        }
    }
}