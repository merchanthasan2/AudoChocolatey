using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace QuantumUpdater;

public class App : Application, IXamlMetadataProvider
{
    private readonly Microsoft.UI.Xaml.XamlTypeInfo.XamlControlsXamlMetaDataProvider _controlsProvider = new();
    private Window? _window;

    // Code-only WinUI: tell the XAML parser where the standard control types live.
    public IXamlType GetXamlType(Type type) => _controlsProvider.GetXamlType(type);
    public IXamlType GetXamlType(string fullName) => _controlsProvider.GetXamlType(fullName);
    public XmlnsDefinition[] GetXmlnsDefinitions() => _controlsProvider.GetXmlnsDefinitions();

    public App()
    {
        CrashLog.Write("Mark:AppStart", null); // TEMP DIAGNOSTIC
        UnhandledException += (_, e) => CrashLog.Write("UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog.Write("Domain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => CrashLog.Write("Task", e.Exception);

        // TEMP DIAGNOSTIC (16 Nov, start crash 0xC000027B). Remove after the cause is found.
        if (!DiagSwitches.Has("nofirstchance")) // TEMP DIAGNOSTIC 46.7.38
        {
            AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            {
                if (_inFirstChance)
                {
                    return;
                }

                _inFirstChance = true;
                try
                {
                    CrashLog.Write("FirstChance", e.Exception);
                }
                finally
                {
                    _inFirstChance = false;
                }
            };
        }
    }

    [ThreadStatic]
    private static bool _inFirstChance;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Resources.MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());
            _window = new MainWindow();
            _window.Activate();
            CrashLog.Write("Mark:Activated", null); // TEMP DIAGNOSTIC
        }
        catch (Exception ex)
        {
            CrashLog.Write("Launch", ex);
            throw;
        }
    }
}
