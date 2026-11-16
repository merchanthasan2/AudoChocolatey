using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace QuantumUpdater;

public class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += (_, e) =>
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QuantumUpdater");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "winui-crash.log"),
                    $"[{DateTime.Now:o}] Unhandled: {e.Message}\n{e.Exception}\n");
            }
            catch
            {
                // ignore logging failures
            }

            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
