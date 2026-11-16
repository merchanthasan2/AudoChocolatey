namespace QuantumUpdater.Models;

/// <summary>Shipped product version for the WinUI shell.</summary>
public static class AppVersion
{
    public static Version Version { get; } = new(2, 0, 3);

    /// <summary>Display label, e.g. <c>2.0.3</c>.</summary>
    public static string Label => $"{Version.Major}.{Version.Minor}.{Version.Build}";
}