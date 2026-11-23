using System.Diagnostics;
using System.Text;
using System.Text.Json;
using QuantumUpdater.Services;
using Xunit;
using Xunit.Abstractions;

namespace QuantumUpdater.Tests;

/// <summary>Session hardening: DPAPI session file, junction-safe writes, and the ActivityLog lock.</summary>
[Collection("ActivityLog")]
public class SessionHardeningTests : IDisposable
{
    private const string Token = "tok-4f1d9c2e-secret-value";
    private const string Email = "owner@example.test";

    private readonly ITestOutputHelper _output;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qu-session-" + Guid.NewGuid().ToString("N"));
    private readonly string _oldLogPath = ActivityLog.PathName;

    public SessionHardeningTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        ActivityLog.PathName = _oldLogPath;
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

    private string Folder(string name)
    {
        return Path.Combine(_root, name);
    }

    // ---- DPAPI session file ----

    [Fact]
    public void Saved_session_file_does_not_hold_the_token_or_email_in_plain_text()
    {
        var folder = Folder("s1");
        new SessionStore(folder).Save(Token, Email);

        var bytes = File.ReadAllBytes(Path.Combine(folder, "session.json"));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain(Token, text);
        Assert.DoesNotContain(Email, text);
        Assert.DoesNotContain("secret", text);

        using var doc = JsonDocument.Parse(text);
        Assert.Equal(SessionStore.ProtectedVersion, doc.RootElement.GetProperty("v").GetInt32());
    }

    [Fact]
    public void Protected_session_reads_back()
    {
        var folder = Folder("s2");
        new SessionStore(folder).Save(Token, Email);

        var again = new SessionStore(folder);

        Assert.True(again.IsSignedIn);
        Assert.Equal(Token, again.Token);
        Assert.Equal(Email, again.Email);
    }

    [Fact]
    public void Old_plain_text_session_is_read_once_and_written_again_protected()
    {
        var folder = Folder("s3");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "session.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { Token, Email }));

        var store = new SessionStore(folder);

        Assert.Equal(Token, store.Token);
        Assert.Equal(Email, store.Email);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain(Token, text);
        Assert.Contains("\"v\"", text);
        Assert.Equal(Token, new SessionStore(folder).Token);
    }

    [Fact]
    public void Protected_data_changed_on_disk_clears_the_session()
    {
        var folder = Folder("s4");
        new SessionStore(folder).Save(Token, Email);
        var path = Path.Combine(folder, "session.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { v = 2, data = Convert.ToBase64String(new byte[64]) }));

        var store = new SessionStore(folder);

        Assert.False(store.IsSignedIn);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"v\":3,\"data\":\"AAAA\"}")]
    [InlineData("{\"v\":2,\"data\":\"***\"}")]
    [InlineData("{\"Token\":\"\",\"Email\":\"a@b.c\"}")]
    public void Bad_session_file_gives_signed_out(string content)
    {
        var folder = Folder("s5-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "session.json"), content);

        Assert.False(new SessionStore(folder).IsSignedIn);
    }

    [Fact]
    public void Clear_deletes_the_session_file()
    {
        var folder = Folder("s6");
        var store = new SessionStore(folder);
        store.Save(Token, Email);

        store.Clear();

        Assert.False(store.IsSignedIn);
        Assert.False(File.Exists(Path.Combine(folder, "session.json")));
        Assert.False(new SessionStore(folder).IsSignedIn);
    }

    [Fact]
    public void Install_id_is_kept_between_runs()
    {
        var folder = Folder("s7");
        var first = new SessionStore(folder).InstallId;

        Assert.True(Guid.TryParse(first, out _));
        Assert.Equal(first, new SessionStore(folder).InstallId);
    }

    [Fact]
    public void Dpapi_round_trip_and_tamper_check()
    {
        var clear = Encoding.UTF8.GetBytes("hello");
        var sealedBytes = SessionStore.Dpapi.Protect(clear);

        Assert.NotEqual(clear, sealedBytes);
        Assert.Equal(clear, SessionStore.Dpapi.Unprotect(sealedBytes));

        sealedBytes[^1] ^= 0xFF;
        Assert.Throws<SessionStore.CryptographicFailure>(() => SessionStore.Dpapi.Unprotect(sealedBytes));
    }

    // ---- Junction-safe writes ----

    [Fact]
    public void Write_replaces_the_file_and_leaves_no_temp_file()
    {
        var folder = Folder("w1");

        Assert.True(SafeLocalFile.TryWriteText(folder, "a.json", "one"));
        Assert.True(SafeLocalFile.TryWriteText(folder, "a.json", "two"));

        Assert.Equal("two", File.ReadAllText(Path.Combine(folder, "a.json")));
        Assert.Single(Directory.GetFiles(folder));
    }

    [Theory]
    [InlineData("..\\x.json")]
    [InlineData("sub\\x.json")]
    [InlineData("C:\\x.json")]
    [InlineData("..")]
    [InlineData("")]
    public void Write_refuses_a_name_with_a_folder_part(string name)
    {
        var folder = Folder("w2");

        Assert.False(SafeLocalFile.TryWriteText(folder, name, "x"));
        Assert.Null(SafeLocalFile.TryRead(folder, name, 100));
        Assert.False(SafeLocalFile.TryDelete(folder, name));
    }

    [Fact]
    public void Read_refuses_a_file_over_the_limit()
    {
        var folder = Folder("w3");
        Assert.True(SafeLocalFile.TryWriteText(folder, "big.txt", new string('x', 200)));

        Assert.Null(SafeLocalFile.TryRead(folder, "big.txt", 100));
        Assert.NotNull(SafeLocalFile.TryRead(folder, "big.txt", 200));
    }

    [Fact]
    public void Folder_that_is_a_junction_is_not_written_read_or_deleted()
    {
        var target = Folder("real");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "session.json"), "{\"Token\":\"t\",\"Email\":\"e\"}");
        var junction = Folder("link");
        if (!TryCreateJunction(junction, target))
        {
            _output.WriteLine("Skipped: could not create a junction in the temp folder.");
            return;
        }

        try
        {
            Assert.True(SafeLocalFile.IsReparsePoint(junction));
            Assert.False(SafeLocalFile.TryWriteText(junction, "new.json", "x"));
            Assert.Null(SafeLocalFile.TryRead(junction, "session.json", 1000));
            Assert.False(SafeLocalFile.TryDelete(junction, "session.json"));

            var store = new SessionStore(junction);
            Assert.False(store.IsSignedIn);
            store.Save(Token, Email);
            Assert.True(store.IsSignedIn);

            // Nothing went through the junction into the real folder.
            Assert.False(File.Exists(Path.Combine(target, "new.json")));
            Assert.False(File.Exists(Path.Combine(target, "install-id.txt")));
            Assert.Equal("{\"Token\":\"t\",\"Email\":\"e\"}", File.ReadAllText(Path.Combine(target, "session.json")));
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    [Fact]
    public void File_that_is_a_link_is_not_written_through()
    {
        var folder = Folder("w5");
        Directory.CreateDirectory(folder);
        var outside = Folder("outside.txt");
        File.WriteAllText(outside, "keep");
        var link = Path.Combine(folder, "a.json");
        try
        {
            File.CreateSymbolicLink(link, outside);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _output.WriteLine("Skipped: symbolic links need Developer Mode or admin here.");
            return;
        }

        Assert.False(SafeLocalFile.TryWriteText(folder, "a.json", "changed"));
        Assert.Null(SafeLocalFile.TryRead(folder, "a.json", 1000));
        Assert.Equal("keep", File.ReadAllText(outside));
    }

    [Fact]
    public void Hard_link_at_the_target_is_replaced_not_written_through()
    {
        var folder = Folder("w6");
        Directory.CreateDirectory(folder);
        var outside = Folder("outside-hard.txt");
        File.WriteAllText(outside, "keep");
        var link = Path.Combine(folder, "a.json");
        if (!TryCreateHardLink(link, outside))
        {
            _output.WriteLine("Skipped: could not create a hard link in the temp folder.");
            return;
        }

        Assert.True(SafeLocalFile.TryWriteText(folder, "a.json", "new"));

        Assert.Equal("new", File.ReadAllText(link));
        Assert.Equal("keep", File.ReadAllText(outside));
    }

    // ---- ActivityLog lock ----

    [Fact]
    public async Task Activity_entries_written_at_the_same_time_are_all_kept()
    {
        ActivityLog.PathName = Path.Combine(Folder("log"), "activity.json");
        const int count = 40;

        var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, count)
            .Select(i => Task.Run(() =>
            {
                start.Wait();
                ActivityLog.Add("install", $"Vendor.App{i:D2}", success: true);
            }))
            .ToArray();
        start.Set();
        await Task.WhenAll(tasks);

        var entries = ActivityLog.Read();
        Assert.Equal(count, entries.Count);
        Assert.Equal(count, entries.Select(e => e.PackageId).Distinct().Count());
    }

    [Fact]
    public void Activity_log_keeps_the_last_200()
    {
        ActivityLog.PathName = Path.Combine(Folder("log2"), "activity.json");
        for (int i = 0; i < 205; i++)
        {
            ActivityLog.Add("upgrade", $"Vendor.App{i:D3}", success: true);
        }

        var entries = ActivityLog.Read();
        Assert.Equal(200, entries.Count);
        Assert.Equal("Vendor.App005", entries[0].PackageId);
        Assert.Equal("Vendor.App204", entries[^1].PackageId);
    }

    [Fact]
    public void Activity_log_in_a_junction_folder_is_not_written()
    {
        var target = Folder("log-real");
        Directory.CreateDirectory(target);
        var junction = Folder("log-link");
        if (!TryCreateJunction(junction, target))
        {
            _output.WriteLine("Skipped: could not create a junction in the temp folder.");
            return;
        }

        try
        {
            ActivityLog.PathName = Path.Combine(junction, "activity.json");
            ActivityLog.Add("install", "Vendor.App", success: true);

            Assert.False(File.Exists(Path.Combine(target, "activity.json")));
            Assert.Empty(ActivityLog.Read());
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    // Makes a junction with cmd. Returns false when it cannot be made.
    private static bool TryCreateJunction(string link, string target)
    {
        return RunCmd($"/c mklink /J \"{link}\" \"{target}\"") && Directory.Exists(link);
    }

    private static bool TryCreateHardLink(string link, string target)
    {
        return RunCmd($"/c mklink /H \"{link}\" \"{target}\"") && File.Exists(link);
    }

    private static bool RunCmd(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(10_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
