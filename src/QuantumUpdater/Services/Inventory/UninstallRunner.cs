using System.Diagnostics;
using Windows.Management.Deployment;
using QuantumUpdater.Models;
using QuantumUpdater.Services.Chocolatey;
using QuantumUpdater.Services.Elevation;
using QuantumUpdater.Services.Scoop;
using QuantumUpdater.Services.Winget;

namespace QuantumUpdater.Services.Inventory;

/// <summary>
/// Executes a safe <see cref="UninstallPlan"/> through <see cref="ActionQueue"/> (46.5.19).
/// </summary>
public sealed class UninstallRunner
{
    private static readonly TimeSpan OwnTimeout = TimeSpan.FromMinutes(15);

    private readonly ActionQueue _queue;
    private readonly InstalledService _wingetInstalled;
    private readonly InstalledCatalog _catalog;
    private readonly Func<string, bool, Task>? _reportActivity;
    private readonly ScoopService? _scoop;
    private readonly ChocolateyService? _choco;

    public UninstallRunner(
        ActionQueue queue,
        InstalledService wingetInstalled,
        InstalledCatalog catalog,
        Func<string, bool, Task>? reportActivity = null,
        ScoopService? scoop = null,
        ChocolateyService? choco = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _wingetInstalled = wingetInstalled ?? throw new ArgumentNullException(nameof(wingetInstalled));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _reportActivity = reportActivity;
        _scoop = scoop;
        _choco = choco;
    }

    public QueuedAction Enqueue(AppEntry app, UninstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(plan);

        return _queue.Enqueue(
            app.Key,
            ActionKind.Remove,
            app.Name,
            (progress, token) => ExecuteAsync(app, plan, progress, token));
    }

    private async Task<ActionOutcome> ExecuteAsync(
        AppEntry app,
        UninstallPlan plan,
        IProgress<ActionProgress> progress,
        CancellationToken token)
    {
        if (!plan.CanRun)
        {
            return new ActionOutcome(false, plan.SummaryText);
        }

        progress.Report(new ActionProgress("Removing", null));
        ActionResult result;

        switch (plan.Route)
        {
            case UninstallRoute.Winget when !string.IsNullOrEmpty(plan.CatalogId):
                result = await _wingetInstalled.UninstallAsync(plan.CatalogId, token).ConfigureAwait(false);
                break;

            case UninstallRoute.Scoop when !string.IsNullOrEmpty(plan.CatalogId) && _scoop is not null:
                result = await _scoop.UninstallAsync(plan.CatalogId, token).ConfigureAwait(false);
                break;

            case UninstallRoute.Chocolatey when !string.IsNullOrEmpty(plan.CatalogId) && _choco is not null:
                result = await _choco.UninstallAsync(plan.CatalogId, token).ConfigureAwait(false);
                break;

            case UninstallRoute.Store when !string.IsNullOrEmpty(plan.StoreFullName):
                result = await RemoveStoreAppAsync(plan.StoreFullName, token).ConfigureAwait(false);
                break;

            case UninstallRoute.Msi:
            case UninstallRoute.OwnUninstaller:
                result = await RunNativeUninstallerAsync(plan, token).ConfigureAwait(false);
                break;

            default:
                result = RunOutcome.NotStarted(plan.SummaryText);
                break;
        }

        if (result.Success)
        {
            _catalog.RemoveEntry(app.Key);
        }

        if (_reportActivity is not null && !token.IsCancellationRequested)
        {
            try
            {
                await _reportActivity(app.CatalogId ?? app.Name, result.Success).ConfigureAwait(false);
            }
            catch
            {
                // Ignore activity reporting failure.
            }
        }

        return new ActionOutcome(result.Success, result.Text, result.StillRunning);
    }

    private static async Task<ActionResult> RemoveStoreAppAsync(string fullName, CancellationToken token)
    {
        try
        {
            var pm = new PackageManager();
            var op = pm.RemovePackageAsync(fullName);
            var res = await op.AsTask(token).ConfigureAwait(false);
            if (res.IsRegistered || !string.IsNullOrEmpty(res.ErrorText))
            {
                return new ActionResult(
                    false,
                    false,
                    false,
                    string.IsNullOrWhiteSpace(res.ErrorText) ? "Store app could not be removed." : res.ErrorText,
                    null,
                    null);
            }

            return new ActionResult(true, false, false, "Removed.", null, null);
        }
        catch (OperationCanceledException)
        {
            return new ActionResult(false, false, false, "The removal was cancelled.", null, null);
        }
        catch (Exception ex)
        {
            return new ActionResult(false, false, false, string.IsNullOrWhiteSpace(ex.Message) ? "Store app could not be removed." : ex.Message, null, null);
        }
    }

    private static async Task<ActionResult> RunNativeUninstallerAsync(UninstallPlan plan, CancellationToken token)
    {
        if (string.IsNullOrEmpty(plan.Program))
        {
            return RunOutcome.NotStarted("Uninstaller program missing.");
        }

        if (plan.RunAsNormalUser)
        {
            var launch = await Task.Run(() => NormalUserLauncher.Launch(plan.Program, plan.Arguments), token).ConfigureAwait(false);
            if (!launch.Started || launch.ProcessId is null)
            {
                return RunOutcome.NotStarted(launch.Error ?? "Could not start uninstaller as current user.");
            }

            try
            {
                using var proc = Process.GetProcessById(launch.ProcessId.Value);
                using var timeoutCts = new CancellationTokenSource(OwnTimeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                await proc.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                int code = proc.ExitCode;
                if (code == 0 || code == 3010 || code == 1641)
                {
                    return new ActionResult(true, code == 3010 || code == 1641, false, code == 0 ? "Removed." : "Removed. Restart the PC to finish.", null, null);
                }

                return new ActionResult(false, false, false, $"The uninstaller stopped (code {code}).", null, null);
            }
            catch (ArgumentException)
            {
                // Process already exited before we attached.
                return new ActionResult(true, false, false, "Removed.", null, null);
            }
            catch (OperationCanceledException)
            {
                return new ActionResult(false, false, false, "The removal was cancelled.", null, null);
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = plan.Program,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        foreach (var arg in plan.Arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        await Task.Run(process.Start, token).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(OwnTimeout);
        using var merged = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);
        try
        {
            await process.WaitForExitAsync(merged.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill errors.
            }

            return new ActionResult(false, false, false, token.IsCancellationRequested ? "The removal was cancelled." : "The removal took too long.", null, null);
        }

        int exit = process.ExitCode;
        if (exit == 0 || exit == 3010 || exit == 1641)
        {
            return new ActionResult(true, exit == 3010 || exit == 1641, false, exit == 0 ? "Removed." : "Removed. Restart the PC to finish.", null, null);
        }

        return new ActionResult(false, false, false, $"The uninstaller stopped (code {exit}).", null, null);
    }
}
