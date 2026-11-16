using Microsoft.Win32;

namespace QuantumUpdater.Services;

public sealed record StartupEntry(string Name, string Source);

/// <summary>Reads startup entries. It does not enable, disable, or launch them.</summary>
public sealed class StartupService
{
    public IReadOnlyList<StartupEntry> List()
    {
        var list = new List<StartupEntry>();
        ReadRun(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Your account", list);
        ReadRun(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "This PC", list);
        ReadRun(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "This PC, 32-bit", list);
        ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Your startup folder", list);
        ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Shared startup folder", list);
        return list;
    }

    private static void ReadRun(RegistryKey hive, string path, string source, List<StartupEntry> list)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            if (key is null)
            {
                return;
            }

            foreach (var name in key.GetValueNames())
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    list.Add(new StartupEntry(name.Trim(), source));
                }
            }
        }
        catch
        {
            // A hive can be unreadable. Keep the entries already collected.
        }
    }

    private static void ReadFolder(string folder, string source, List<StartupEntry> list)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                list.Add(new StartupEntry(Path.GetFileName(file), source));
            }
        }
        catch
        {
            // The folder listing is optional.
        }
    }
}
