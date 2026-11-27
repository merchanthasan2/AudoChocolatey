using System.Text.RegularExpressions;
using Microsoft.Win32;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Inventory;

public sealed record UninstallPlan(
    bool CanRun,
    UninstallRoute Route,
    string? Program,
    IReadOnlyList<string> Arguments,
    bool RunAsNormalUser,
    string? CatalogId,
    string? StoreFullName,
    string SummaryText,
    string? RefusalReason = null);

public sealed record UninstallEnvironment(
    string ProgramFiles,
    string ProgramFilesX86,
    string SystemRoot,
    string ProgramData,
    string LocalAppData,
    string AppData,
    Func<string, bool> FileExists)
{
    public static UninstallEnvironment Current { get; } = new(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        File.Exists);
}

/// <summary>
/// Chooses a safe uninstall route for any <see cref="AppEntry"/> (section 5.2, 46.5.18).
/// Pure rules so unit tests validate every safety boundary without touching the registry.
/// </summary>
public static class UninstallPlanner
{
    private static readonly HashSet<string> BlockedPrograms = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "cmd.exe",
        "powershell", "powershell.exe",
        "pwsh", "pwsh.exe",
        "wscript", "wscript.exe",
        "cscript", "cscript.exe",
        "mshta", "mshta.exe",
        "reg", "reg.exe",
        "regedit", "regedit.exe",
        "rundll32", "rundll32.exe",
    };

    private static readonly string[] ForbiddenArgTokens =
    {
        "http://", "https://", "|", "&", "<", ">", "`", "$(", "%", "..",
    };

    private static readonly Regex MsiGuidPattern = new(
        @"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static UninstallPlan Plan(AppEntry app, UninstallEnvironment? env = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        env ??= UninstallEnvironment.Current;

        if (app.IsProtected)
        {
            return Refused(UninstallRoute.None, "This app is protected by Windows.", "Protected by Windows.");
        }

        // 0. Scoop or Chocolatey package
        if (app.Source == AppSource.Scoop && !string.IsNullOrWhiteSpace(app.CatalogId))
        {
            return new UninstallPlan(
                CanRun: true,
                Route: UninstallRoute.Scoop,
                Program: null,
                Arguments: Array.Empty<string>(),
                RunAsNormalUser: true,
                CatalogId: app.CatalogId,
                StoreFullName: null,
                SummaryText: "DeskVerve removes this app quietly using Scoop.");
        }

        if (app.Source == AppSource.Chocolatey && !string.IsNullOrWhiteSpace(app.CatalogId))
        {
            return new UninstallPlan(
                CanRun: true,
                Route: UninstallRoute.Chocolatey,
                Program: null,
                Arguments: Array.Empty<string>(),
                RunAsNormalUser: false,
                CatalogId: app.CatalogId,
                StoreFullName: null,
                SummaryText: "DeskVerve removes this app quietly using Chocolatey.");
        }

        // 1. Exact winget match
        if (app.Match == MatchKind.Exact && PackageId.IsCatalogId(app.CatalogId))
        {
            return new UninstallPlan(
                CanRun: true,
                Route: UninstallRoute.Winget,
                Program: null,
                Arguments: Array.Empty<string>(),
                RunAsNormalUser: false,
                CatalogId: app.CatalogId,
                StoreFullName: null,
                SummaryText: "DeskVerve removes this app quietly using the Microsoft catalog.");
        }

        // 2. Store / MSIX app
        if (app.Source == AppSource.MicrosoftStore && !string.IsNullOrWhiteSpace(app.StoreFullName))
        {
            return new UninstallPlan(
                CanRun: true,
                Route: UninstallRoute.Store,
                Program: null,
                Arguments: Array.Empty<string>(),
                RunAsNormalUser: true,
                CatalogId: null,
                StoreFullName: app.StoreFullName,
                SummaryText: "DeskVerve removes this Store app for your account.");
        }

        // 3. MSI product code
        string? msiGuid = ExtractMsiGuid(app);
        if (msiGuid is not null)
        {
            var msiexec = Path.Combine(env.SystemRoot, "System32", "msiexec.exe");
            return new UninstallPlan(
                CanRun: true,
                Route: UninstallRoute.Msi,
                Program: msiexec,
                Arguments: [$"/x{msiGuid}", "/passive"],
                RunAsNormalUser: false,
                CatalogId: null,
                StoreFullName: null,
                SummaryText: "DeskVerve runs Windows Installer to remove this app.");
        }

        // 4. Own uninstaller (QuietUninstallString first, then UninstallString)
        string? raw = !string.IsNullOrWhiteSpace(app.QuietUninstallString)
            ? app.QuietUninstallString
            : app.UninstallString;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Refused(UninstallRoute.None, "No safe uninstaller was found. Open Windows Settings to remove it.", "Missing uninstall string.");
        }

        if (!TrySplitCommand(raw.Trim(), out string exe, out List<string> args))
        {
            return Refused(UninstallRoute.None, "Open Windows Settings to remove this app.", "Could not parse uninstall string.");
        }

        exe = ExpandKnownEnv(exe, env);
        if (!Path.IsPathFullyQualified(exe) || !env.FileExists(exe))
        {
            return Refused(UninstallRoute.None, "Open Windows Settings to remove this app.", "Uninstaller executable was not found.");
        }

        string fileName = Path.GetFileName(exe);
        if (BlockedPrograms.Contains(fileName))
        {
            return Refused(UninstallRoute.None, "Open Windows Settings to remove this app.", "Script or shell uninstallers are blocked.");
        }

        bool isUser = app.RegistryRef?.Hive == RegistryHive.CurrentUser;
        if (!IsAllowedFolder(exe, isUser, env))
        {
            return Refused(UninstallRoute.None, "Open Windows Settings to remove this app.", "Uninstaller is outside allowed app folders.");
        }

        foreach (var arg in args)
        {
            foreach (var bad in ForbiddenArgTokens)
            {
                if (arg.Contains(bad, StringComparison.OrdinalIgnoreCase))
                {
                    return Refused(UninstallRoute.None, "Open Windows Settings to remove this app.", "Uninstall arguments contain blocked characters.");
                }
            }
        }

        return new UninstallPlan(
            CanRun: true,
            Route: UninstallRoute.OwnUninstaller,
            Program: exe,
            Arguments: args,
            RunAsNormalUser: isUser,
            CatalogId: null,
            StoreFullName: null,
            SummaryText: $"DeskVerve runs {(string.IsNullOrWhiteSpace(app.Publisher) ? "the app's" : app.Publisher + "'s")} own uninstaller. A window can open. Follow its steps to finish.");
    }

    private static UninstallPlan Refused(UninstallRoute route, string summary, string reason) =>
        new(false, route, null, Array.Empty<string>(), false, null, null, summary, reason);

    private static string? ExtractMsiGuid(AppEntry app)
    {
        if (!string.IsNullOrWhiteSpace(app.ProductCode) && MsiGuidPattern.IsMatch(app.ProductCode))
        {
            return MsiGuidPattern.Match(app.ProductCode).Value;
        }

        var raw = app.UninstallString ?? "";
        if (raw.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            var m = MsiGuidPattern.Match(raw);
            if (m.Success)
            {
                return m.Value;
            }
        }

        return null;
    }

    private static bool IsAllowedFolder(string exe, bool isUser, UninstallEnvironment env)
    {
        if (isUser)
        {
            return IsUnder(exe, env.LocalAppData) || IsUnder(exe, env.AppData);
        }

        return IsUnder(exe, env.ProgramFiles)
            || IsUnder(exe, env.ProgramFilesX86)
            || IsUnder(exe, Path.Combine(env.SystemRoot, "Installer"))
            || IsUnder(exe, Path.Combine(env.ProgramData, "Package Cache"));
    }

    private static bool IsUnder(string candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        string fullCandidate = candidate.Replace('/', '\\').TrimEnd('\\');
        string fullRoot = root.Replace('/', '\\').TrimEnd('\\') + '\\';
        return fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpandKnownEnv(string path, UninstallEnvironment env)
    {
        return path
            .Replace("%ProgramFiles%", env.ProgramFiles, StringComparison.OrdinalIgnoreCase)
            .Replace("%ProgramFiles(x86)%", env.ProgramFilesX86, StringComparison.OrdinalIgnoreCase)
            .Replace("%SystemRoot%", env.SystemRoot, StringComparison.OrdinalIgnoreCase)
            .Replace("%WinDir%", env.SystemRoot, StringComparison.OrdinalIgnoreCase)
            .Replace("%ProgramData%", env.ProgramData, StringComparison.OrdinalIgnoreCase)
            .Replace("%LocalAppData%", env.LocalAppData, StringComparison.OrdinalIgnoreCase)
            .Replace("%AppData%", env.AppData, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TrySplitCommand(string command, out string program, out List<string> args)
    {
        program = string.Empty;
        args = new List<string>();
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var tokens = new List<string>();
        int i = 0;
        while (i < command.Length)
        {
            while (i < command.Length && char.IsWhiteSpace(command[i])) i++;
            if (i >= command.Length) break;

            if (command[i] == '"')
            {
                i++;
                int start = i;
                while (i < command.Length && command[i] != '"') i++;
                tokens.Add(command[start..i]);
                if (i < command.Length) i++;
            }
            else
            {
                int start = i;
                while (i < command.Length && !char.IsWhiteSpace(command[i])) i++;
                tokens.Add(command[start..i]);
            }
        }

        if (tokens.Count == 0)
        {
            return false;
        }

        // Handle unquoted path with spaces when first token isn't an exe yet.
        if (!tokens[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !command.StartsWith('"'))
        {
            int exeIdx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0)
            {
                int end = exeIdx + 4;
                if (end == command.Length || char.IsWhiteSpace(command[end]))
                {
                    program = command[..end].Trim();
                    args = TokenizeArgs(command[end..]);
                    return true;
                }
            }
        }

        program = tokens[0];
        args = tokens.Skip(1).ToList();
        return true;
    }

    private static List<string> TokenizeArgs(string rest)
    {
        var list = new List<string>();
        int i = 0;
        while (i < rest.Length)
        {
            while (i < rest.Length && char.IsWhiteSpace(rest[i])) i++;
            if (i >= rest.Length) break;
            if (rest[i] == '"')
            {
                i++;
                int start = i;
                while (i < rest.Length && rest[i] != '"') i++;
                list.Add(rest[start..i]);
                if (i < rest.Length) i++;
            }
            else
            {
                int start = i;
                while (i < rest.Length && !char.IsWhiteSpace(rest[i])) i++;
                list.Add(rest[start..i]);
            }
        }

        return list;
    }
}
