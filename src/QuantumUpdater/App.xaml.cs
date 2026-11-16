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
        UnhandledException += (_, e) => CrashLog.Write("UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog.Write("Domain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => CrashLog.Write("Task", e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Resources.MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Launch", ex);
            throw;
        }
    }
}

/// <summary>Writes error type and stack trace only. No emails, tokens, or file contents.</summary>
internal static class CrashLog
{
    public static void Write(string where, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantumUpdater");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "winui-crash.log"),
                $"{DateTimeOffset.Now:O} [{where}] {ex?.GetType().FullName}: {ex?.Message}{Environment.NewLine}{ex?.StackTrace}{Environment.NewLine}{ex?.InnerException}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
