// Spike 46.5.1 (run 2): read installed packages through the winget COM API.
// Default mode is read-only. It installs, upgrades and uninstalls nothing.
// Spike 46.7.88: "upgrade <id> [--cancel-after <seconds>]" upgrades ONE app. Run it only when the Manager asks, with the owner's yes.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Management.Deployment;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "upgrade", StringComparison.Ordinal))
        {
            return await RunUpgradeAsync(args);
        }

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

    // Spike 46.7.88. Exit code 3 is the refusal code: a bad argument or a failed safety check.
    private const int RefusedExitCode = 3;

    // Same rule as the task 46.7.88 id pattern. \z is used, not $, so a trailing new line is refused.
    private static readonly Regex UpgradeIdPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9.+_-]{0,127}\z",
        RegexOptions.CultureInvariant);

    // Upgrades one app with the COM API. Checks run in this order: id, admin, catalog match, 5 second wait.
    private static async Task<int> RunUpgradeAsync(string[] args)
    {
        var total = Stopwatch.StartNew();
        Console.WriteLine("Spike 46.7.88: WinGet COM upgrade test");
        Console.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        // Usage: upgrade <id> [--cancel-after <seconds>]
        if (!TryParseUpgradeArgs(args, out double? cancelAfter))
        {
            Console.WriteLine("Usage: WingetComSpike.exe upgrade <id> [--cancel-after <seconds>]");
            return RefusedExitCode;
        }
        string id = args[1];

        // Check 1: the id. No spaces and no wildcards.
        if (!UpgradeIdPattern.IsMatch(id))
        {
            Console.WriteLine("Refused: the id is not allowed. Use 1 to 128 letters, digits, '.', '+', '_' or '-'. No spaces.");
            return RefusedExitCode;
        }

        // Check 2: admin process.
        if (!IsElevated())
        {
            Console.WriteLine("Run this from an admin window.");
            return RefusedExitCode;
        }

        // Check 3: the catalog. Exactly one match, with the same id.
        PackageManager manager;
        try
        {
            manager = new PackageManager();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"COM start FAILED: {ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}");
            return Finish(total, 1);
        }

        PackageCatalog catalog;
        try
        {
            var remoteRef = manager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog);
            var composite = new CreateCompositePackageCatalogOptions();
            composite.Catalogs.Add(remoteRef);
            composite.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;
            var connect = manager.CreateCompositePackageCatalog(composite).Connect();
            catalog = connect.PackageCatalog;
            if (catalog == null)
            {
                Console.WriteLine($"Composite connect returned no catalog. Status: {connect.Status}");
                return Finish(total, 2);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Composite connect FAILED: {ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}");
            return Finish(total, 2);
        }

        FindPackagesResult found;
        try
        {
            var find = new FindPackagesOptions();
            find.Selectors.Add(new PackageMatchFilter
            {
                Field = PackageMatchField.Id,
                Option = PackageFieldMatchOption.EqualsCaseInsensitive,
                Value = id
            });
            found = await catalog.FindPackagesAsync(find);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Find FAILED: {ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}");
            return Finish(total, 2);
        }
        if (found.Status != FindPackagesResultStatus.Ok)
        {
            Console.WriteLine($"Find returned status {found.Status}. Nothing changed.");
            return Finish(total, 2);
        }

        int matchCount = found.Matches?.Count ?? 0;
        if (matchCount != 1)
        {
            Console.WriteLine($"Refused: {matchCount} packages match the id. Exactly one is required.");
            return Finish(total, RefusedExitCode);
        }

        var package = found.Matches[0].CatalogPackage;
        if (package is null || !string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Refused: the package found does not have the same id.");
            return Finish(total, RefusedExitCode);
        }
        if (package.InstalledVersion is null)
        {
            Console.WriteLine("Not installed: nothing to upgrade.");
            return Finish(total, RefusedExitCode);
        }
        if (!package.IsUpdateAvailable)
        {
            Console.WriteLine("No update available.");
            return Finish(total, RefusedExitCode);
        }

        string installedVersion = package.InstalledVersion.Version ?? "(unknown)";
        string availableVersion = package.DefaultInstallVersion?.Version ?? "(unknown)";
        Console.WriteLine($"Found one package. Installed: {installedVersion}. Available: {availableVersion}.");

        // Check 4: 5 second wait. Ctrl+C ends the program here, before the upgrade starts.
        Console.WriteLine($"About to upgrade {id} from {installedVersion} to {availableVersion}. Starting in 5 seconds. Press Ctrl+C to stop.");
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Upgrade with the COM API.
        var options = new InstallOptions
        {
            PackageInstallMode = PackageInstallMode.Silent,
            AcceptPackageAgreements = true,
            AllowUpgradeToUnknownVersion = true,
            AllowHashMismatch = false,
            Force = false
        };

        var stats = new UpgradeStats();
        var sw = Stopwatch.StartNew();
        var operation = manager.UpgradePackageAsync(package, options);
        operation.Progress = (op, p) =>
        {
            lock (stats.Gate)
            {
                stats.Total++;
                stats.PerState[p.State] = stats.PerState.GetValueOrDefault(p.State) + 1;
                if (p.State == PackageInstallProgressState.Downloading)
                {
                    stats.DownloadEvents++;
                    if (p.BytesRequired > 0)
                    {
                        stats.DownloadWithBytes++;
                    }
                }
                stats.LastState = p.State;
                Console.WriteLine(
                    $"{sw.ElapsedMilliseconds,8} ms  State={p.State}  BytesDownloaded={p.BytesDownloaded}  BytesRequired={p.BytesRequired}  DownloadProgress={p.DownloadProgress:F3}  InstallationProgress={p.InstallationProgress:F3}");
            }
        };

        var task = operation.AsTask();
        bool cancelSent = false;
        bool cancelDuringDownload = false;
        bool stillRunning = false;
        long cancelEndMs = -1;

        if (cancelAfter is double seconds)
        {
            var timer = Task.Delay(TimeSpan.FromSeconds(seconds));
            var first = await Task.WhenAny(task, timer);
            if (first == timer && !task.IsCompleted)
            {
                lock (stats.Gate)
                {
                    cancelDuringDownload = stats.LastState == PackageInstallProgressState.Downloading;
                }

                cancelSent = true;
                var cancelSw = Stopwatch.StartNew();
                operation.Cancel();
                Console.WriteLine($"{sw.ElapsedMilliseconds,8} ms  Cancel() called. The call took {cancelSw.ElapsedMilliseconds} ms.");

                var end = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
                cancelEndMs = cancelSw.ElapsedMilliseconds;
                if (end == task)
                {
                    Console.WriteLine($"cancel ended in {cancelEndMs} ms");
                }
                else
                {
                    stillRunning = true;
                    Console.WriteLine("STILL RUNNING after 10 s");
                }
            }
            else
            {
                Console.WriteLine($"The operation ended before {seconds} s. No cancel was sent.");
            }
        }

        int exitCode = 0;
        if (stillRunning)
        {
            // The spike stops waiting here. The summary still prints.
            exitCode = 1;
        }
        else
        {
            try
            {
                var result = await task;
                PrintInstallResult(result);
            }
            catch (Exception ex)
            {
                string kind = ex is OperationCanceledException ? "cancelled" : "faulted";
                Console.WriteLine($"Operation {kind}: {ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}");
                exitCode = 1;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Summary:");
        lock (stats.Gate)
        {
            Console.WriteLine($"Total progress events: {stats.Total}");
            foreach (var pair in stats.PerState.OrderBy(pair => (int)pair.Key))
            {
                Console.WriteLine($"  {pair.Key}: {pair.Value}");
            }

            Console.WriteLine($"Download events (State Downloading): {stats.DownloadEvents}. With BytesRequired above 0: {stats.DownloadWithBytes}.");
            bool bytesRule = stats.DownloadWithBytes >= 5;
            Console.WriteLine($"Rule A (5 or more download events with real bytes): {Yes(bytesRule)}");

            bool cancelRule = true;
            if (cancelSent && cancelDuringDownload)
            {
                cancelRule = !stillRunning && cancelEndMs <= 2000;
                string endText = stillRunning ? "still running" : $"{cancelEndMs} ms";
                Console.WriteLine($"Rule B (cancel during download ends in 2000 ms or less): {Yes(cancelRule)} ({endText})");
            }
            else
            {
                Console.WriteLine("Rule B: not used. No cancel during download.");
            }

            Console.WriteLine($"PASS rule of 46.7.89 met: {Yes(bytesRule && cancelRule)}");
        }

        return Finish(total, exitCode);
    }

    // Accepts "upgrade <id>" or "upgrade <id> --cancel-after <seconds>". Seconds: above 0 and up to 600.
    private static bool TryParseUpgradeArgs(string[] args, out double? cancelAfter)
    {
        cancelAfter = null;
        if (args.Length == 2)
        {
            return true;
        }

        if (args.Length == 4
            && args[2] == "--cancel-after"
            && double.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
            && seconds > 0
            && seconds <= 600)
        {
            cancelAfter = seconds;
            return true;
        }

        return false;
    }

    private static void PrintInstallResult(InstallResult result)
    {
        Console.WriteLine(
            $"Result: Status={result.Status}, ExtendedErrorCode={ErrorHex(result.ExtendedErrorCode)}, InstallerErrorCode={result.InstallerErrorCode} (0x{result.InstallerErrorCode:X8}), RebootRequired={result.RebootRequired}");
        if (!string.IsNullOrEmpty(result.CorrelationData))
        {
            Console.WriteLine($"CorrelationData: {result.CorrelationData}");
        }
    }

    // The projection gives ExtendedErrorCode as an Exception. Its HResult is the error code. No error gives 0x00000000.
    private static string ErrorHex(Exception error) =>
        $"0x{(error is null ? 0u : (uint)error.HResult):X8}";

    private static string Yes(bool value) => value ? "YES" : "NO";

    // Counts for the upgrade summary. Progress events come from another thread, so use Gate.
    private sealed class UpgradeStats
    {
        public readonly object Gate = new();
        public readonly Dictionary<PackageInstallProgressState, int> PerState = new();
        public int Total;
        public int DownloadEvents;
        public int DownloadWithBytes;
        public PackageInstallProgressState LastState;
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
