using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace QuantumUpdater.Views.Controls;

/// <summary>
/// App icon for a row: a letter tile first, then the original icon when it is set with SetImage.
/// Size 32 (radius 6) or 48 (radius 8). The icon is decoration: the row has the name, so screen readers skip it.
/// WinUI control. Use it on the UI thread only, Show and SetImage both.
/// </summary>
public sealed class AppIcon : Grid
{
    private readonly double _size;
    private readonly IconSlot _slot = new();
    private readonly Border _tileHost;
    private readonly Border _imageHost;
    private readonly Image _image;

    public AppIcon(double size)
    {
        _size = size;
        var radius = size >= 48 ? 8 : 6;

        Width = size;
        Height = size;

        _tileHost = new Border
        {
            Width = size,
            Height = size,
        };

        _image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
        };

        _imageHost = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(radius),
            Visibility = Visibility.Collapsed,
            // The Border corner radius clips the image. mockup: 01-updates.html .ic (radius 6px; 8px for 48 px).
            Child = _image,
        };

        Children.Add(_tileHost);
        Children.Add(_imageHost);

        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>
    /// Starts a new icon load for the app and shows its letter tile. Hides any image.
    /// Returns the ticket to pass to SetImage. Call on the UI thread.
    /// </summary>
    public int Show(string key, string name)
    {
        var ticket = _slot.Begin(key);
        _tileHost.Child = Chrome.LetterTile(name, _size);
        _tileHost.Visibility = Visibility.Visible;
        _image.Source = null;
        _imageHost.Visibility = Visibility.Collapsed;
        return ticket;
    }

    /// <summary>
    /// Shows the image only when the ticket is still the current one, so a late load on a recycled row is ignored.
    /// Call on the UI thread (a background load must post the call to the UI thread first).
    /// </summary>
    public void SetImage(int ticket, ImageSource image)
    {
        if (!_slot.IsCurrent(ticket))
        {
            return;
        }

        _image.Source = image;
        _imageHost.Visibility = Visibility.Visible;
    }
}
