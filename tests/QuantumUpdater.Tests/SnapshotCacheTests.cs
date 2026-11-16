using System.Diagnostics;
using QuantumUpdater.Services;
using Xunit;
using Xunit.Abstractions;

namespace QuantumUpdater.Tests;

public class SnapshotCacheTests : IDisposable
{
    private const int MaxBytes = 20 * 1024 * 1024;

    private readonly ITestOutputHelper _output;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qu-snapcache-" + Guid.NewGuid().ToString("N"));

    public SnapshotCacheTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp folder cleanup is best effort.
        }
    }

    private string CacheFolder => Path.Combine(_root, "cache");

    private sealed record Item(string Text, int Count);

    [Fact]
    public void Round_trip_gives_the_value_and_a_recent_saved_time()
    {
        var cache = new SnapshotCache(CacheFolder);

        cache.Save("updates", 1, new Item("Firefox", 3));
        Snapshot<Item>? snapshot = cache.Load<Item>("updates", 1);

        Assert.NotNull(snapshot);
        Assert.Equal(new Item("Firefox", 3), snapshot.Value);
        Assert.True(Math.Abs((DateTimeOffset.UtcNow - snapshot.SavedAt).TotalSeconds) < 5);
    }

    [Fact]
    public void Bad_file_is_deleted_and_load_gives_null()
    {
        Directory.CreateDirectory(CacheFolder);
        string path = Path.Combine(CacheFolder, "updates.json");
        File.WriteAllText(path, "not json");
        var cache = new SnapshotCache(CacheFolder);

        Snapshot<Item>? snapshot = cache.Load<Item>("updates", 1);

        Assert.Null(snapshot);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Old_schema_gives_null_and_the_file_is_kept()
    {
        var cache = new SnapshotCache(CacheFolder);
        cache.Save("updates", 1, new Item("Firefox", 3));

        Snapshot<Item>? snapshot = cache.Load<Item>("updates", 2);

        Assert.Null(snapshot);
        Assert.True(File.Exists(Path.Combine(CacheFolder, "updates.json")));
    }

    [Fact]
    public void Value_over_20_MB_is_not_saved_and_load_gives_null()
    {
        var cache = new SnapshotCache(CacheFolder);
        string big = new string('a', MaxBytes + 1);

        cache.Save("big", 1, big);

        Assert.False(File.Exists(Path.Combine(CacheFolder, "big.json")));
        Assert.False(File.Exists(Path.Combine(CacheFolder, "big.json.tmp")));
        Assert.Null(cache.Load<string>("big", 1));
    }

    [Fact]
    public void File_over_20_MB_gives_null_on_load()
    {
        Directory.CreateDirectory(CacheFolder);
        string path = Path.Combine(CacheFolder, "big.json");
        using (var stream = new FileStream(path, FileMode.Create))
        {
            stream.SetLength(MaxBytes + 1);
        }
        var cache = new SnapshotCache(CacheFolder);

        Assert.Null(cache.Load<Item>("big", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Updates")]
    [InlineData("my_cache")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("name\n")]
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijk")]
    public void Bad_name_is_refused(string name)
    {
        var cache = new SnapshotCache(CacheFolder);

        Assert.Throws<ArgumentException>(() => cache.Save(name, 1, new Item("x", 1)));
        Assert.Throws<ArgumentException>(() => cache.Load<Item>(name, 1));
        Assert.False(Directory.Exists(CacheFolder));
    }

    [Fact]
    public void Folder_that_is_a_junction_is_not_written()
    {
        string target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        string junction = Path.Combine(_root, "link");
        if (!TryCreateJunction(junction, target))
        {
            _output.WriteLine("Skipped: could not create a junction in the temp folder.");
            return;
        }

        try
        {
            var cache = new SnapshotCache(junction);

            cache.Save("updates", 1, new Item("Firefox", 3));

            Assert.Empty(Directory.GetFiles(target));
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    // Makes a junction with cmd. Returns false when it cannot be made.
    private static bool TryCreateJunction(string link, string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
