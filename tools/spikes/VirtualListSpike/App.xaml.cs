using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace VirtualListSpike;

public class App : Application, IXamlMetadataProvider
{
    private readonly Microsoft.UI.Xaml.XamlTypeInfo.XamlControlsXamlMetaDataProvider _controlsProvider = new();
    private Window? _window;

    // Code-only WinUI: tell the XAML parser where the standard control types live.
    public IXamlType GetXamlType(Type type) => _controlsProvider.GetXamlType(type);
    public IXamlType GetXamlType(string fullName) => _controlsProvider.GetXamlType(fullName);
    public XmlnsDefinition[] GetXmlnsDefinitions() => _controlsProvider.GetXmlnsDefinitions();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Resources.MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());
        _window = new MainWindow();
        _window.Activate();
    }
}
