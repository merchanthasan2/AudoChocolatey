using QuantumUpdater.Services;
using Xunit;

namespace QuantumUpdater.Tests;

// ActivityLog.PathName is shared state, so these tests do not run in parallel with each other.
[Collection("ActivityLog")]
public class AccountClientTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qu-account-" + Guid.NewGuid().ToString("N"));
    private readonly string _oldLogPath = ActivityLog.PathName;

    public AccountClientTests()
    {
        Directory.CreateDirectory(_root);
        ActivityLog.PathName = Path.Combine(_root, "activity.json");
    }

    public void Dispose()
    {
        ActivityLog.PathName = _oldLogPath;
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temp folder cleanup is best effort.
        }
    }

    private SessionStore NewSession() => new(Path.Combine(_root, "session"));

    private static readonly MachineSummary Summary = new("PC-NAME", "Windows 11", "CPU", "16 GB", "C: 100 GB free");

    [Fact]
    public async Task Activity_is_logged_locally_when_signed_out_and_nothing_is_sent()
    {
        var handler = new FakeHttpHandler();
        var client = new AccountClient(handler);

        await client.ReportActivityAsync(NewSession(), "install", "Mozilla.Firefox", success: true);

        var entry = Assert.Single(ActivityLog.Read());
        Assert.Equal("install", entry.Action);
        Assert.Equal("Mozilla.Firefox", entry.PackageId);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Activity_is_logged_locally_and_sent_when_signed_in()
    {
        var handler = new FakeHttpHandler();
        var client = new AccountClient(handler);
        var session = NewSession();
        session.Save("token-abc", "person@example.test");

        await client.ReportActivityAsync(session, "upgrade", "Git.Git", success: false);

        Assert.Single(ActivityLog.Read());
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("/api/activity", sent.Path);
        Assert.Equal("token-abc", sent.Bearer);
    }

    [Fact]
    public async Task Unknown_activity_is_neither_logged_nor_sent()
    {
        var handler = new FakeHttpHandler();

        await new AccountClient(handler).ReportActivityAsync(NewSession(), "format-disk", "X.Y", success: true);

        Assert.Empty(ActivityLog.Read());
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Heartbeat_sends_no_email_and_no_pc_name()
    {
        var handler = new FakeHttpHandler();
        var session = NewSession();
        session.Save("token-abc", "person@example.test");

        await new AccountClient(handler).ReportHeartbeatAsync(session, Summary);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal("/api/heartbeat", sent.Path);
        Assert.DoesNotContain("email", sent.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("person@example.test", sent.Body);
        Assert.DoesNotContain("PC-NAME", sent.Body);
        Assert.Contains(session.InstallId, sent.Body);
    }

    [Fact]
    public async Task Heartbeat_is_not_sent_when_signed_out()
    {
        var handler = new FakeHttpHandler();

        await new AccountClient(handler).ReportHeartbeatAsync(NewSession(), Summary);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Activity_log_write_failure_does_not_throw()
    {
        // A folder in place of the file makes the write fail.
        Directory.CreateDirectory(ActivityLog.PathName);

        ActivityLog.Add("install", "Mozilla.Firefox", success: true);
    }
}
