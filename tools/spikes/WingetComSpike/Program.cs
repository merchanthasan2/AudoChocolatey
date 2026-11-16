// Spike 46.5.1 (run 2): read installed packages through the winget COM API.
// Read-only. This program installs, upgrades and uninstalls nothing.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Management.Deployment;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Spike 46.5.1 run 2: WinGet COM read test");
        Console.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Elevated (admin) process: {IsElevated()}");
        Console.WriteLine($"Runtime: {Environment.Version}, OS: {Environment.OSVersion}");
        Console.WriteLine();

        var total = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();

        // Step 1: start the COM API (out-of-proc activation of the App Installer server).
        PackageManager manager;
        try
        {
            manager = new PackageManager();
        }
        catch (COMException ex)
        {
            Console.WriteLine($"[1] COM start FAILED after {Ms(sw)} ms: HRESULT 0x{ex.HResult:X8}: {ex.Message}");
            return Finish(total, 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[1] COM start FAILED after {Ms(sw)} ms: {ex.GetType().Name}: {ex.Message}");
            return Finish(total, 1);
        }
        Console.WriteLine($"[1] COM start OK in {Ms(sw)} ms");

        // Step 2: composite catalog, documented pattern. Remote winget catalog only.
        // LocalCatalogs adds the installed packages automatically.
        sw.Restart();
        PackageCatalog catalog;
        try
        {
            var remoteRef = manager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog);
            var options = new CreateCompositePackageCatalogOptions();
            options.Catalogs.Add(remoteRef);
            options.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;
            var connect = manager.CreateCompositePackageCatalog(options).Connect();
            catalog = connect.PackageCatalog;
            if (catalog == null)
            {
                Console.WriteLine($"[2] Composite connect returned no catalog. Status: {connect.Status}");
                return Finish(total, 2);
            }
            Console.WriteLine($"[2] Composite catalog connected in {Ms(sw)} ms. Status: {connect.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[2] Composite connect FAILED after {Ms(sw)} ms: {ex.GetType().Name}: {ex.Message}");
            return Finish(total, 2);
        }

        // Step 3: try each find attempt in order. Keep the first one that returns Ok.
        var attempts = new List<(string Label, Func<Task<FindPackagesResult>> Run)>
        {
            ("A sync, no filter", () => Task.FromResult(catalog.FindPackages(NewFindOptions(null)))),
            ("B async, no filter", async () => await catalog.FindPackagesAsync(NewFindOptions(null))),
            ("C sync, Id contains \"\"", () => Task.FromResult(catalog.FindPackages(NewFindOptions("")))),
            ("D sync, Id contains \".\"", () => Task.FromResult(catalog.FindPackages(NewFindOptions(".")))),
            ("E async, Id contains \"\"", async () => await catalog.FindPackagesAsync(NewFindOptions(""))),
            ("F async, Id contains \".\"", async () => await catalog.FindPackagesAsync(NewFindOptions(".")))
        };
        // --fast skips the two "." attempts (about 4 minutes each, known useless).
        if (Environment.GetCommandLineArgs().Contains("--fast"))
        {
            attempts.RemoveAll(a => a.Label.Contains("\".\""));
        }

        FindPackagesResult chosen = null;
        string chosenLabel = null;
        foreach (var (label, run) in attempts)
        {
            sw.Restart();
            try
            {
                var result = await run();
                Console.WriteLine($"[3] Attempt {label}: status {result.Status}, matches {result.Matches?.Count ?? 0}, {Ms(sw)} ms");
                if (chosen == null && result.Status == FindPackagesResultStatus.Ok)
                {
                    chosen = result;
                    chosenLabel = label;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3] Attempt {label}: EXCEPTION after {Ms(sw)} ms: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (chosen == null)
        {
            Console.WriteLine("[3] No attempt returned Ok. No update data.");
            return Finish(total, 3);
        }
        Console.WriteLine($"[3] Using attempt {chosenLabel}");

        // Step 4: read the matches and count them.
        sw.Restart();
        var matches = chosen.Matches.ToList();
        int arpCount = 0;
        int msixCount = 0;
        int otherCount = 0;
        foreach (var match in matches)
        {
            string id = match.CatalogPackage.Id ?? string.Empty;
            if (id.StartsWith("ARP\\", StringComparison.OrdinalIgnoreCase)) arpCount++;
            else if (id.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase)) msixCount++;
            else otherCount++;
        }
        Console.WriteLine($"[4] Total matches: {matches.Count}. Local id ARP\\: {arpCount}, MSIX\\: {msixCount}, other: {otherCount} in {Ms(sw)} ms");

        // Step 5: update data per package.
        sw.Restart();
        int updateCount = 0;
        int knownCount = 0;
        int unknownCount = 0;
        var withUpdate = new List<(string Name, string Id, string Installed, string Available)>();
        foreach (var match in matches)
        {
            var pkg = match.CatalogPackage;
            if (pkg.DefaultInstallVersion != null) knownCount++;
            else unknownCount++;
            if (pkg.IsUpdateAvailable)
            {
                updateCount++;
                withUpdate.Add((pkg.Name, pkg.Id, pkg.InstalledVersion?.Version, pkg.DefaultInstallVersion?.Version));
            }
        }
        Console.WriteLine($"[5] IsUpdateAvailable: {updateCount}. Known to winget (DefaultInstallVersion not null): {knownCount}. Not known: {unknownCount}. in {Ms(sw)} ms");

        // Step 6: first 10 packages with an update.
        Console.WriteLine("[6] First 10 packages with an update (name | id | installed | available):");
        foreach (var row in withUpdate.Take(10))
        {
            Console.WriteLine($"    {row.Name} | {row.Id} | {row.Installed} | {row.Available}");
        }

        return Finish(total, 0);
    }

    private static FindPackagesOptions NewFindOptions(string idContains)
    {
        var options = new FindPackagesOptions();
        if (idContains != null)
        {
            options.Selectors.Add(new PackageMatchFilter
            {
                Field = PackageMatchField.Id,
                Option = PackageFieldMatchOption.ContainsCaseInsensitive,
                Value = idContains
            });
        }
        return options;
    }

    private static int Finish(Stopwatch total, int code)
    {
        Console.WriteLine();
        Console.WriteLine($"Total run time: {Ms(total)} ms. Exit code: {code}");
        return code;
    }

    private static long Ms(Stopwatch sw) => sw.ElapsedMilliseconds;

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
