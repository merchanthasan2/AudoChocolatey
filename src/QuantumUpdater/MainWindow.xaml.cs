using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace QuantumUpdater;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        var tag = item.Tag?.ToString() ?? "GettingReady";
        PlaceholderText.Text = tag switch
        {
            "Updates" => "Updates — placeholder",
            "Installed" => "Installed — placeholder",
            _ => "Getting ready — placeholder",
        };
    }
}