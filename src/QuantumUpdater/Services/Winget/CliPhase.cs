namespace QuantumUpdater.Services.Winget;

/// <summary>Recognizes phase lines from the English CLI backup. It never estimates progress.</summary>
public static class CliPhase
{
    public static bool TryParse(string? line, out string phase)
    {
        phase = string.Empty;
        if (string.IsNullOrWhiteSpace(line)) return false;

        string text = line.Trim();
        if (text.StartsWith("Found ", StringComparison.Ordinal))
            phase = "Preparing";
        else if (text.StartsWith("Downloading ", StringComparison.Ordinal))
            phase = "Downloading";
        else if (text.Equals("Successfully verified installer hash", StringComparison.Ordinal))
            phase = "Checking";
        else if (text.Equals("Starting package install...", StringComparison.Ordinal)
            || text.Equals("Starting package install", StringComparison.Ordinal))
            phase = "Installing";

        return phase.Length != 0;
    }
}
