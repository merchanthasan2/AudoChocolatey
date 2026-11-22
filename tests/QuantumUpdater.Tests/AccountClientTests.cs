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
    public async Task Session_check_with_no_session_sends_nothing()
    {
        var handler = new FakeHttpHandler();

        var check = await new AccountClient(handler).CheckSessionAsync(NewSession());

        Assert.Equal(SessionCheck.SignedOut, check);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Session_check_calls_get_me_with_the_token_and_keeps_a_valid_session()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(System.Net.HttpStatusCode.OK, """{"ok":true,"email":"person@example.test"}"""));
        var session = NewSession();
        session.Save("token-abc", "person@example.test");

        var check = await new AccountClient(handler).CheckSessionAsync(session);

        Assert.Equal(SessionCheck.Valid, check);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("/api/me", sent.Path);
        Assert.Equal("token-abc", sent.Bearer);
        Assert.True(session.IsSignedIn);
    }

    [Fact]
    public async Task Session_check_401_clears_the_stored_session_and_raises_rejected()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(System.Net.HttpStatusCode.Unauthorized, """{"ok":false,"message":"Sign in required."}"""));
        var folder = Path.Combine(_root, "session");
        var session = new SessionStore(folder);
        session.Save("token-abc", "person@example.test");
        var client = new AccountClient(handler);
        int rejected = 0;
        client.SessionRejected += () => rejected++;

        var check = await client.CheckSessionAsync(session);

        Assert.Equal(SessionCheck.Rejected, check);
        Assert.False(session.IsSignedIn);
        Assert.Null(session.Email);
        Assert.Equal(1, rejected);
        Assert.False(File.Exists(Path.Combine(folder, "session.json")));
        Assert.False(new SessionStore(folder).IsSignedIn);
    }

    [Theory]
    [InlineData("heartbeat")]
    [InlineData("activity")]
    public async Task Any_401_with_the_token_clears_the_session(string call)
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(System.Net.HttpStatusCode.Unauthorized, """{"ok":false}"""));
        var session = NewSession();
        session.Save("token-abc", "person@example.test");
        var client = new AccountClient(handler);
        int rejected = 0;
        client.SessionRejected += () => rejected++;

        if (call == "heartbeat")
        {
            await client.ReportHeartbeatAsync(session, Summary);
        }
        else
        {
            await client.ReportActivityAsync(session, "remove", "Git.Git", success: true);
        }

        Assert.False(session.IsSignedIn);
        Assert.Equal(1, rejected);
    }

    [Fact]
    public async Task Offline_or_server_error_keeps_the_session()
    {
        var session = NewSession();
        session.Save("token-abc", "person@example.test");
        var offline = new AccountClient(new FakeHttpHandler(_ => throw new HttpRequestException("No connection could be made to 127.0.0.1:8787")));
        var broken = new AccountClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(System.Net.HttpStatusCode.InternalServerError, """{"ok":false}""")));

        Assert.Equal(SessionCheck.Unreachable, await offline.CheckSessionAsync(session));
        Assert.Equal(SessionCheck.Unreachable, await broken.CheckSessionAsync(session));
        Assert.True(session.IsSignedIn);
    }

    [Fact]
    public async Task Unreachable_message_is_plain_and_has_no_exception_text()
    {
        var client = new AccountClient(new FakeHttpHandler(_ => throw new HttpRequestException("secret-detail-4471")));

        var result = await client.SignInAsync("person@example.test", "password-1");

        Assert.False(result.Ok);
        Assert.DoesNotContain("secret-detail-4471", result.Message);
    }

    [Fact]
    public async Task A_401_on_sign_in_does_not_clear_another_stored_session()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(System.Net.HttpStatusCode.Unauthorized, """{"ok":false,"message":"Email or password is wrong."}"""));
        var session = NewSession();
        session.Save("token-abc", "person@example.test");
        var client = new AccountClient(handler);
        int rejected = 0;
        client.SessionRejected += () => rejected++;

        var result = await client.SignInAsync("other@example.test", "wrong-password");

        Assert.False(result.Ok);
        Assert.True(session.IsSignedIn);
        Assert.Equal(0, rejected);
    }

    [Fact]
    public void Activity_log_write_failure_does_not_throw()
    {
        // A folder in place of the file makes the write fail.
        Directory.CreateDirectory(ActivityLog.PathName);

        ActivityLog.Add("install", "Mozilla.Firefox", success: true);
    }
}
