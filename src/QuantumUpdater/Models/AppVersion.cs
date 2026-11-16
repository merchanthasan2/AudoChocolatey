namespace QuantumUpdater.Models;

/// <summary>Shipped product version for the greenfield WinUI app.</summary>
public static class AppVersion
{
    public static Version Version { get; } = new(3, 0, 0);

    public static string Label => $"{Version.Major}.{Version.Minor}.{Version.Build}";
}
