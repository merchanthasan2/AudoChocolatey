using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using QuantumUpdater.Models;
using QuantumUpdater.Services;
using QuantumUpdater.Services.Catalog;
using QuantumUpdater.Services.Inventory;
using QuantumUpdater.Services.Net;
using QuantumUpdater.Services.Scoop;
using QuantumUpdater.Services.Chocolatey;
using QuantumUpdater.Services.Updates;
using QuantumUpdater.Services.Winget;
using Xunit;

namespace QuantumUpdater.Tests;

public sealed class Phase46CompletionTests
{
    [Fact]
    public void ComProgressMap_CalculatesDownloadBytes_AndNeverGoesBackwards()
    {
        var map = new ComProgressMap();

        Assert.Equal(new ActionProgress("Waiting for Windows", null), map.Map(ComState.Queued, 0, 0, 0));
        Assert.Equal(new ActionProgress("Downloading", null), map.Map(ComState.Downloading, 500, 0, 0));
        Assert.Equal(new ActionProgress("Downloading", 50), map.Map(ComState.Downloading, 500, 1000, 0));
        Assert.Equal(new ActionProgress("Downloading", 50), map.Map(ComState.Downloading, 250, 1000, 0));

        Assert.Equal(new ActionProgress("Installing", null), map.Map(ComState.Installing, 0, 0, 0));
        Assert.Equal(new ActionProgress("Installing", 40), map.Map(ComState.Installing, 0, 0, 0.4));
        Assert.Equal(new ActionProgress("Installing", 40), map.Map(ComState.Installing, 0, 0, 0.2));
        Assert.Equal(new ActionProgress("Finishing", null), map.Map(ComState.PostInstall, 0, 0, 0));
        Assert.Equal(new ActionProgress("Done", 100), map.Map(ComState.Finished, 0, 0, 0));
    }

    [Fact]
    public async Task UpdatesCatalog_FallsBackToCli_AndSavesSnapshot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qu-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new SnapshotCache(dir);
            var primary = new ThrowingUpdatesSource();
            var fallback = new FakeUpdatesSource(new[]
            {
                new UpdateCandidate("Mozilla.Firefox", "Firefox", "Mozilla", "128.0", "129.0", "Microsoft catalog"),
            });

            var fixedNow = new DateTimeOffset(2026, 11, 25, 14, 0, 0, TimeSpan.Zero);
            var catalog = new UpdatesCatalog(primary, fallback, cache, a => a(), () => fixedNow);
            var entries = await catalog.RefreshAsync();

            Assert.Single(entries);
            Assert.Equal("Mozilla.Firefox", entries[0].CatalogId);
            Assert.Equal(UpgradeGroup.Ready, entries[0].Group);

            catalog.MarkUpdated("Mozilla.Firefox", "129.0", fixedNow.AddMinutes(5));
            Assert.Equal(UpgradeGroup.UpdatedToday, catalog.Entries.Single().Group);

            var reloaded = new UpdatesCatalog(primary, fallback, cache, a => a(), () => fixedNow.AddMinutes(6));
            Assert.True(reloaded.LoadSaved());
            Assert.Single(reloaded.Entries);
            Assert.Equal(UpgradeGroup.UpdatedToday, reloaded.Entries[0].Group);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateActions_UsesCliOnlyWhenComReturnsNotStarted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qu-act-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new SnapshotCache(dir);
            var catalog = new UpdatesCatalog(
                new FakeUpdatesSource(Array.Empty<UpdateCandidate>()),
                new FakeUpdatesSource(Array.Empty<UpdateCandidate>()),
                cache,
                a => a());
            var queue = new ActionQueue(a => a());

            int cliCalls = 0;
            var actions = new UpdateActions(
                queue,
                catalog,
                (id, _, _) => Task.FromResult(RunOutcome.NotStarted("COM unavailable")),
                (id, _, _) =>
                {
                    cliCalls++;
                    return Task.FromResult(new ActionResult(true, false, false, "Updated.", null, null));
                });

            var entry = new UpgradeEntry("Git.Git", "Git", "Git", "2.46", "2.47", "Microsoft catalog", UpgradeGroup.Ready, null, null);
            var queued = actions.Update(entry);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (queued.State is ActionState.Waiting or ActionState.Working && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.Equal(1, cliCalls);
            Assert.Equal(ActionState.Done, queued.State);

            int cliCallsAfterStart = 0;
            var actionsStartedFail = new UpdateActions(
                queue,
                catalog,
                (id, _, _) => Task.FromResult(new ActionResult(false, false, NotStarted: false, "Failed in installer", null, null)),
                (id, _, _) =>
                {
                    cliCallsAfterStart++;
                    return Task.FromResult(new ActionResult(true, false, false, "Updated.", null, null));
                });

            var queued2 = actionsStartedFail.Update(entry with { CatalogId = "7zip.7zip", Name = "7-Zip" });
            deadline = DateTime.UtcNow.AddSeconds(5);
            while (queued2.State is ActionState.Waiting or ActionState.Working && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.Equal(0, cliCallsAfterStart);
            Assert.Equal(ActionState.Failed, queued2.State);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ActionQueue_HoldBlocksNextItemUntilStillRunningFinishes()
    {
        var queue = new ActionQueue(a => a());
        var releaseHold = new TaskCompletionSource();
        bool secondStarted = false;

        queue.Enqueue("a", ActionKind.Update, "App A", (_, _) =>
            Task.FromResult(new ActionOutcome(false, "Still running", releaseHold.Task)));

        var second = queue.Enqueue("b", ActionKind.Update, "App B", (_, _) =>
        {
            secondStarted = true;
            return Task.FromResult(new ActionOutcome(true, null));
        });

        await Task.Delay(120);
        Assert.True(queue.IsHeld);
        Assert.False(secondStarted);

        releaseHold.SetResult();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (second.State is ActionState.Waiting or ActionState.Working && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.False(queue.IsHeld);
        Assert.True(secondStarted);
        Assert.Equal(ActionState.Done, second.State);
    }

    [Fact]
    public void InstalledMerger_MatchesArpStoreAndWingetPortable()
    {
        var reg = new[]
        {
            new AppEntry("r1", "Mozilla Firefox (x64 en-US)", "Mozilla", "128.0", AppSource.InstalledDirectly, null,
                new RegistryRef(RegistryHive.LocalMachine, RegistryView.Registry64, "Mozilla Firefox 128.0 (x64 en-US)"),
                null, MatchKind.None, UninstallRoute.OwnUninstaller, false, null, null, null, null, null, null, @"C:\Program Files\Mozilla Firefox\uninstall\helper.exe", null),
        };
        var store = new[]
        {
            new AppEntry("s1", "Windows Calculator", "Microsoft Corporation", "11.0", AppSource.MicrosoftStore, null,
                null, "Microsoft.WindowsCalculator_11.24_x64__8wekyb3d8bbwe", MatchKind.None, UninstallRoute.Store, false, null, null, null, null, null, null, null, null),
        };
        var winget = new[]
        {
            new CatalogApp(@"ARP\Machine\X64\Mozilla Firefox 128.0 (x64 en-US)", "Mozilla.Firefox", "Mozilla Firefox", "Mozilla", "128.0", "129.0", true, Array.Empty<string>(), Array.Empty<string>()),
            new CatalogApp("voidtools.Everything", "voidtools.Everything", "Everything", "voidtools", "1.4.1", null, false, Array.Empty<string>(), Array.Empty<string>()),
        };

        var merged = InstalledMerger.Merge(reg, store, winget);
        Assert.Equal(3, merged.Count);

        var firefox = merged.Single(a => a.Key == "r1");
        Assert.Equal("Mozilla.Firefox", firefox.CatalogId);
        Assert.Equal(MatchKind.Exact, firefox.Match);
        Assert.Equal(UninstallRoute.Winget, firefox.Route);
        Assert.Equal("129.0", firefox.UpdateVersion);

        Assert.Contains(merged, a => a.CatalogId == "voidtools.Everything" && a.Route == UninstallRoute.Winget);
        Assert.Contains(merged, a => a.Key == "s1" && a.Route == UninstallRoute.Store);
    }

    [Fact]
    public void UninstallPlanner_EnforcesUserElevationAndBlockedShellRules()
    {
        var env = new UninstallEnvironment(
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Users\demo\AppData\Local",
            @"C:\Users\demo\AppData\Roaming",
            _ => true);

        var hkcuApp = new AppEntry(
            "u1", "User App", "Maker", "1.0", AppSource.InstalledDirectly, null,
            new RegistryRef(RegistryHive.CurrentUser, RegistryView.Registry64, "UserApp"),
            null, MatchKind.None, UninstallRoute.OwnUninstaller, false, null, null, null, null, null, null,
            "\"C:\\Users\\demo\\AppData\\Local\\UserApp\\unins000.exe\" /SILENT", null);

        var userPlan = UninstallPlanner.Plan(hkcuApp, env);
        Assert.True(userPlan.CanRun);
        Assert.True(userPlan.RunAsNormalUser);
        Assert.Equal(UninstallRoute.OwnUninstaller, userPlan.Route);

        var cmdApp = hkcuApp with { UninstallString = @"C:\Windows\System32\cmd.exe /c del foo" };
        var cmdPlan = UninstallPlanner.Plan(cmdApp, env);
        Assert.False(cmdPlan.CanRun);

        var badArgApp = hkcuApp with { UninstallString = "\"C:\\Users\\demo\\AppData\\Local\\UserApp\\unins000.exe\" https://evil.test" };
        Assert.False(UninstallPlanner.Plan(badArgApp, env).CanRun);
    }

    [Fact]
    public async Task SafeIconFetcher_EnforcesHttpsMakerDomainAndMagicBytes()
    {
        byte[] pngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        var handler = new FakeHttpHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri == "https://cdn.example.com/icon.png")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(pngBytes),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var fetcher = new SafeIconFetcher(handler);
        Assert.Null(await fetcher.FetchImageAsync("http://cdn.example.com/icon.png"));
        Assert.Null(await fetcher.FetchImageAsync("https://cdn.example.com/icon.png", makerSiteUrl: "https://other.org"));

        var sha = Convert.ToHexString(SHA256.HashData(pngBytes));
        Assert.Null(await fetcher.FetchImageAsync("https://cdn.example.com/icon.png", "https://example.com", "0000"));
        var ok = await fetcher.FetchImageAsync("https://cdn.example.com/icon.png", "https://example.com", sha);
        Assert.NotNull(ok);
        Assert.Equal(".png", ok!.Extension);
    }

    [Fact]
    public void CatalogPicks_SkipsInvalidIdsAndParsesCategories()
    {
        const string json = """
        {
          "categories": [
            { "id": "ai", "title": "AI tools", "tag": "ai", "query": "ai" }
          ],
          "picks": [
            { "id": "Ollama.Ollama", "name": "Ollama", "publisher": "Ollama", "category": "ai", "popular": true, "summary": "Local LLMs" },
            { "id": "bad id with spaces", "name": "Bad", "publisher": "X", "category": "ai", "popular": true, "summary": "No" }
          ]
        }
        """;

        var catalog = CatalogPicks.Parse(json);
        Assert.Single(catalog.Categories);
        Assert.Single(catalog.Picks);
        Assert.Equal("Ollama.Ollama", catalog.Picks[0].Id);
    }

    [Fact]
    public void BundledCatalogPicksJson_LoadsCleanlyWithAllTenCategories()
    {
        // Walk up from the test output folder (bin/Debug/net10.0 or bin/x64/Debug/net10.0)
        // until the repo's src/QuantumUpdater folder is found.
        var rel = Path.Combine("src", "QuantumUpdater", "Assets", "catalog-picks.json");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, rel)))
            dir = dir.Parent;
        Assert.True(dir != null, $"Missing {rel} above {AppContext.BaseDirectory}");
        var path = Path.Combine(dir!.FullName, rel);

        var catalog = CatalogPicks.LoadFromFile(path);
        Assert.Equal(10, catalog.Categories.Count);
        Assert.True(catalog.Picks.Count >= 70);

        var netSpeedBoss = Assert.Single(catalog.Picks, p => p.Id == "netspeedboss");
        Assert.True(netSpeedBoss.Popular);
        Assert.Equal("scoop", netSpeedBoss.Source);
        Assert.Equal("netspeedboss", netSpeedBoss.Bucket);
        Assert.Equal("https://github.com/merchanthasan2/scoop-bucket", netSpeedBoss.BucketUrl);
    }

    [Fact]
    public async Task ScoopService_AddsBucketBeforeInstall_AndParsesListAndSearch()
    {
        var calls = new List<string>();
        var scoop = new ScoopService(
            () => @"C:\Users\demo\scoop\shims\scoop.ps1",
            (args, _, _, _) =>
            {
                calls.Add(string.Join(" ", args));
                return Task.FromResult(new RunResult(0, false, false, new[] { "Installed!" }));
            });

        var res = await scoop.InstallAsync(
            "netspeedboss",
            "netspeedboss",
            "https://github.com/merchanthasan2/scoop-bucket");

        Assert.True(res.Success);
        Assert.Equal(new[]
        {
            "bucket add netspeedboss https://github.com/merchanthasan2/scoop-bucket",
            "install netspeedboss",
        }, calls);

        var list = ScoopService.ParseList(new[]
        {
            "Installed apps:",
            "",
            "Name          Version Updated             Info",
            "----          ------- -------             ----",
            "netspeedboss  1.1.1   2026-11-25 20:00:00",
        });
        Assert.Single(list);
        Assert.Equal("netspeedboss", list[0].Name);
        Assert.Equal("1.1.1", list[0].Version);

        var search = ScoopService.ParseSearch(new[]
        {
            "'netspeedboss' bucket:",
            "    netspeedboss (1.1.1)",
        });
        Assert.Single(search);
        Assert.Equal("netspeedboss", search[0].Name);
        Assert.Equal("netspeedboss", search[0].Bucket);
    }

    [Fact]
    public async Task ChocolateyService_ValidatesIds_AndParsesSearchOutput()
    {
        var calls = new List<string>();
        var choco = new ChocolateyService(
            () => @"C:\ProgramData\chocolatey\bin\choco.exe",
            (args, _, _) =>
            {
                calls.Add(string.Join(" ", args));
                return Task.FromResult(new RunResult(0, false, false, new[] { "git|2.47.0", "7zip|24.8.0" }));
            });

        var search = await choco.SearchAsync("git");
        Assert.Equal(2, search.Count);
        Assert.Equal("git", search[0].Id);
        Assert.Equal("2.47.0", search[0].Version);

        var bad = await choco.InstallAsync("bad id with spaces");
        Assert.False(bad.Success);
        Assert.True(bad.NotStarted);

        var ok = await choco.InstallAsync("git");
        Assert.True(ok.Success);
        Assert.Contains("install git -y --no-progress", calls);
    }

    private sealed class ThrowingUpdatesSource : IUpdatesSource
    {
        public Task<IReadOnlyList<UpdateCandidate>> ReadAsync(IProgress<int> step, CancellationToken token) =>
            throw new InvalidOperationException("COM unavailable");
    }

    private sealed class FakeUpdatesSource : IUpdatesSource
    {
        private readonly IReadOnlyList<UpdateCandidate> _rows;
        public FakeUpdatesSource(IReadOnlyList<UpdateCandidate> rows) => _rows = rows;
        public Task<IReadOnlyList<UpdateCandidate>> ReadAsync(IProgress<int> step, CancellationToken token) =>
            Task.FromResult(_rows);
    }
}
