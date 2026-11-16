using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Threading;

namespace QuantumUpdater;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Self-contained Windows App SDK — do not call Bootstrap.Initialize
        // (that resolves the framework package and fails with 0x80670016 here).
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
