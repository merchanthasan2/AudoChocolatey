using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QuantumUpdater.Services;

namespace QuantumUpdater;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (StartWithWindowsService.HasBackgroundSwitch(args))
        {
            // Tracker 49.1: Tray UI comes later (49.2). For now, start hidden with no window and exit cleanly.
            return;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
