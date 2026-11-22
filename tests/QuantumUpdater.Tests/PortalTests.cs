using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuantumUpdater.Portal;
using Xunit;

namespace QuantumUpdater.Tests;

internal sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

public class PortalTests : IDisposable
{
    // Low rounds keep the tests fast. The 600,000 default has its own test.
    private const int TestRounds = 1_000;
    private const string Password = "correct-horse-1";
    private const string InstallId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "qu-portal-" + Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new();

    public PortalTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temp folder cleanup is best effort.
        }
    }

    private string StorePath => Path.Combine(_root, "account-store.json");

    private AccountStore NewStore(int rounds = TestRounds) => new(_root, rounds, _clock);

    private PortalHandler NewHandler(AccountStore? store = null) => new(store ?? NewStore(), new RateLimiter(_clock));

    private static PortalRequest Request(
        string method,
        string path,
        string? body = null,
        string? bearer = null,
        string contentType = "application/json; charset=utf-8",
        string? cookie = null,
        string? origin = null,
        string host = PortalHandler.Authority,
        string remote = "127.0.0.1")
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Host"] = host };
        if (body is not null)
        {
            headers["Content-Type"] = contentType;
        }

        if (bearer is not null)
        {
            headers["Authorization"] = "Bearer " + bearer;
        }

        if (cookie is not null)
        {
            headers["Cookie"] = cookie;
        }

        if (origin is not null)
        {
            headers["Origin"] = origin;
        }

        var bytes = Encoding.UTF8.GetBytes(body ?? "");
        return new PortalRequest
        {
            Method = method,
            Path = path,
            RemoteAddress = remote,
            Headers = headers,
            ContentLength = bytes.Length,
            Body = new MemoryStream(bytes),
        };
    }

    private static string Creds(string email, string password = Password) =>
        JsonSerializer.Serialize(new { email, password });

    private static PortalRequest Form(string path, string form, string? cookie = null, string? origin = PortalHandler.Origin) =>
        Request("POST", path, form, contentType: "application/x-www-form-urlencoded", cookie: cookie, origin: origin);

    private static string? TokenOf(PortalResponse response) =>
        JsonDocument.Parse(response.Body).RootElement.TryGetProperty("token", out var token) ? token.GetString() : null;

    private static string MessageOf(PortalResponse response) =>
        JsonDocument.Parse(response.Body).RootElement.GetProperty("message").GetString() ?? "";

    private static string CookieToken(PortalResponse response)
    {
        var cookie = response.Header("Set-Cookie")!;
        return cookie.Split(';')[0].Split('=', 2)[1];
    }

    private async Task<string> RegisterAsync(PortalHandler handler, string email = "person@example.test")
    {
        var reply = await handler.HandleAsync(Request("POST", "/api/register", Creds(email)));
        Assert.Equal(200, reply.Status);
        return TokenOf(reply)!;
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);

    private async Task<string> SetupOwnerAsync(PortalHandler handler, AccountStore store, string email = "owner@example.test")
    {
        var reply = await handler.HandleAsync(Form(
            PortalHandler.AdminPath + "/setup",
            $"setupCode={Encode(AccountStore.FormatCode(store.SetupCode!))}&email={Encode(email)}&password={Encode(Password)}"));
        Assert.Equal(303, reply.Status);
        return CookieToken(reply);
    }

    // ---- F7: GET /api/me ----

    [Fact]
    public async Task Me_answers_200_for_a_good_app_token_and_401_otherwise()
    {
        var handler = NewHandler();
        var token = await RegisterAsync(handler);

        var good = await handler.HandleAsync(Request("GET", "/api/me", bearer: token));
        var none = await handler.HandleAsync(Request("GET", "/api/me"));
        var bad = await handler.HandleAsync(Request("GET", "/api/me", bearer: "00" + token[2..]));

        Assert.Equal(200, good.Status);
        Assert.Contains("person@example.test", good.BodyText);
        Assert.Equal(401, none.Status);
        Assert.Equal(401, bad.Status);
    }

    [Fact]
    public async Task Me_is_401_after_the_app_session_expires_or_logs_out()
    {
        var handler = NewHandler();
        var first = await RegisterAsync(handler);
        var login = await handler.HandleAsync(Request("POST", "/api/login", Creds("person@example.test")));
        var second = TokenOf(login)!;

        await handler.HandleAsync(Request("POST", "/api/logout", "{}", bearer: first));
        Assert.Equal(401, (await handler.HandleAsync(Request("GET", "/api/me", bearer: first))).Status);
        Assert.Equal(200, (await handler.HandleAsync(Request("GET", "/api/me", bearer: second))).Status);

        _clock.Now += AccountStore.AppSessionLife + TimeSpan.FromMinutes(1);
        Assert.Equal(401, (await handler.HandleAsync(Request("GET", "/api/me", bearer: second))).Status);
    }

    // ---- F5: owner setup code ----

    [Fact]
    public async Task First_admin_sign_in_no_longer_creates_an_owner()
    {
        var store = NewStore();
        var handler = NewHandler(store);

        var reply = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/login", $"email=owner%40example.test&password={Encode(Password)}"));

        Assert.Equal(200, reply.Status);
        Assert.Null(reply.Header("Set-Cookie"));
        Assert.False(store.HasOwner);
    }

    [Fact]
    public async Task Owner_setup_needs_the_code_and_then_closes()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        Assert.NotNull(store.SetupCode);

        var page = await handler.HandleAsync(Request("GET", PortalHandler.AdminPath));
        Assert.DoesNotContain(store.SetupCode!, page.BodyText);
        Assert.DoesNotContain(AccountStore.FormatCode(store.SetupCode!), page.BodyText);

        var wrong = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/setup", $"setupCode=AAAA-BBBB-CCCC&email=owner%40example.test&password={Encode(Password)}"));
        Assert.Null(wrong.Header("Set-Cookie"));
        Assert.False(store.HasOwner);

        var cookie = await SetupOwnerAsync(handler, store);
        Assert.True(store.HasOwner);
        Assert.Null(store.SetupCode);

        var dashboard = await handler.HandleAsync(Request("GET", PortalHandler.AdminPath, cookie: $"{PortalHandler.AdminCookie}={cookie}"));
        Assert.Contains("Counts", dashboard.BodyText);

        var again = store.SetupOwner("anything", "second@example.test", Password);
        Assert.Equal(AuthStatus.SetupClosed, again.Status);
    }

    [Fact]
    public void Setup_code_is_dropped_after_five_wrong_tries()
    {
        var store = NewStore();
        var code = store.SetupCode!;
        for (int i = 0; i < AccountStore.MaxSetupTries; i++)
        {
            Assert.Equal(AuthStatus.Failed, store.SetupOwner("WRONG" + i, "owner@example.test", Password).Status);
        }

        Assert.Null(store.SetupCode);
        Assert.Equal(AuthStatus.SetupClosed, store.SetupOwner(code, "owner@example.test", Password).Status);
        Assert.False(store.HasOwner);
    }

    [Fact]
    public void Setup_code_accepts_dashes_spaces_and_small_letters()
    {
        var store = NewStore();
        var typed = " " + AccountStore.FormatCode(store.SetupCode!).ToLowerInvariant().Replace("-", " - ") + " ";

        Assert.True(store.SetupOwner(typed, "owner@example.test", Password).Ok);
    }

    [Fact]
    public void Register_on_the_api_never_makes_an_owner()
    {
        var store = NewStore();
        store.Register("person@example.test", Password);

        Assert.False(store.HasOwner);
        Assert.NotNull(store.SetupCode);
    }

    // ---- F6: rate limits, hashing outside the lock ----

    [Fact]
    public async Task Login_is_limited_per_email()
    {
        var handler = NewHandler();
        await RegisterAsync(handler);
        PortalResponse? last = null;
        for (int i = 0; i < 11; i++)
        {
            last = await handler.HandleAsync(Request("POST", "/api/login", Creds("person@example.test", "wrong-password-" + i)));
        }

        Assert.Equal(429, last!.Status);

        // Even the right password waits until the window passes.
        Assert.Equal(429, (await handler.HandleAsync(Request("POST", "/api/login", Creds("person@example.test")))).Status);
        _clock.Now += TimeSpan.FromMinutes(16);
        Assert.Equal(200, (await handler.HandleAsync(Request("POST", "/api/login", Creds("person@example.test")))).Status);
    }

    [Fact]
    public async Task Login_is_limited_per_address()
    {
        var handler = NewHandler();
        PortalResponse? last = null;
        for (int i = 0; i < 31; i++)
        {
            last = await handler.HandleAsync(Request("POST", "/api/login", Creds($"p{i}@example.test", "wrong-password"), remote: "10.0.0.9"));
        }

        Assert.Equal(429, last!.Status);
        var other = await handler.HandleAsync(Request("POST", "/api/login", Creds("p0@example.test", "wrong-password"), remote: "10.0.0.10"));
        Assert.Equal(401, other.Status);
    }

    [Fact]
    public async Task Register_is_limited_per_address()
    {
        var handler = NewHandler();
        for (int i = 0; i < 10; i++)
        {
            await RegisterAsync(handler, $"p{i}@example.test");
        }

        var over = await handler.HandleAsync(Request("POST", "/api/register", Creds("p10@example.test")));
        Assert.Equal(429, over.Status);
    }

    [Fact]
    public async Task Admin_login_is_limited()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        await SetupOwnerAsync(handler, store);
        PortalResponse? last = null;
        for (int i = 0; i < 6; i++)
        {
            last = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/login", "email=owner%40example.test&password=wrong-password"));
        }

        Assert.Equal(429, last!.Status);
        Assert.Contains("Too many tries", last.BodyText);
    }

    [Fact]
    public void Rate_limiter_allows_the_limit_then_refuses_until_the_window_passes()
    {
        var limiter = new RateLimiter(_clock);
        for (int i = 0; i < 3; i++)
        {
            Assert.True(limiter.TryAcquire("k", 3, TimeSpan.FromMinutes(1)));
        }

        Assert.False(limiter.TryAcquire("k", 3, TimeSpan.FromMinutes(1)));
        _clock.Now += TimeSpan.FromMinutes(1);
        Assert.True(limiter.TryAcquire("k", 3, TimeSpan.FromMinutes(1)));
        limiter.Reset("k");
        Assert.True(limiter.TryAcquire("k", 1, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Filling_the_table_does_not_reset_an_existing_key_and_new_keys_fail_closed()
    {
        var window = TimeSpan.FromMinutes(15);
        var limiter = new RateLimiter(_clock, maxKeys: 100);
        for (int i = 0; i < 9; i++)
        {
            Assert.True(limiter.TryAcquire("login-email:victim@example.test", 10, window));
        }

        // A flood of throwaway emails fills the table. The ones past the cap are refused, not let in by a reset.
        int refused = 0;
        for (int i = 0; i < 10_000; i++)
        {
            if (!limiter.TryAcquire($"login-email:junk{i}@example.test", 10, window))
            {
                refused++;
            }
        }

        Assert.Equal(100, limiter.KeyCount);
        Assert.Equal(10_000 - 99, refused);

        // The victim key kept its 9 tries: one more is allowed, then it is blocked.
        Assert.True(limiter.TryAcquire("login-email:victim@example.test", 10, window));
        Assert.False(limiter.TryAcquire("login-email:victim@example.test", 10, window));
    }

    [Fact]
    public void Full_table_frees_room_only_from_keys_past_their_own_window()
    {
        var limiter = new RateLimiter(_clock, maxKeys: 2);
        Assert.True(limiter.TryAcquire("long", 1, TimeSpan.FromMinutes(15)));
        Assert.True(limiter.TryAcquire("short", 1, TimeSpan.FromMinutes(5)));

        // After 6 minutes only the short key has expired. Pruning with a short window must not drop the long key.
        _clock.Now += TimeSpan.FromMinutes(6);
        Assert.True(limiter.TryAcquire("new", 1, TimeSpan.FromMinutes(5)));
        Assert.False(limiter.TryAcquire("other", 1, TimeSpan.FromMinutes(5)));
        Assert.False(limiter.TryAcquire("long", 1, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void A_shorter_window_never_drops_tries_from_a_longer_one()
    {
        var limiter = new RateLimiter(_clock);
        Assert.True(limiter.TryAcquire("k", 1, TimeSpan.FromMinutes(15)));
        _clock.Now += TimeSpan.FromMinutes(6);

        Assert.False(limiter.TryAcquire("k", 1, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Slow_sign_ins_do_not_block_session_checks()
    {
        // Real rounds make each sign-in slow. A token check must not wait behind them for the store lock.
        var store = NewStore(AccountStore.DefaultIterations);
        var created = store.Register("person@example.test", Password);
        var signIns = Enumerable.Range(0, 2).Select(_ => Task.Run(() => store.Login("person@example.test", "wrong-password"))).ToArray();
        await Task.Delay(50);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var user = store.UserForToken(created.Token, SessionKind.App);
        watch.Stop();
        await Task.WhenAll(signIns);

        Assert.NotNull(user);
        Assert.True(watch.ElapsedMilliseconds < 100, $"Token check waited {watch.ElapsedMilliseconds} ms.");
    }

    // ---- F8: safe store write ----

    [Fact]
    public void Save_keeps_a_bak_copy_and_leaves_no_temp_file()
    {
        var store = NewStore();
        store.Register("one@example.test", Password);
        store.Register("two@example.test", Password);

        Assert.True(File.Exists(StorePath));
        Assert.True(File.Exists(StorePath + ".bak"));
        Assert.False(File.Exists(StorePath + ".tmp"));
        Assert.Contains("one@example.test", File.ReadAllText(StorePath + ".bak"));
        Assert.DoesNotContain("two@example.test", File.ReadAllText(StorePath + ".bak"));
    }

    [Fact]
    public void A_damaged_store_loads_from_the_bak_copy()
    {
        var store = NewStore();
        store.Register("one@example.test", Password);
        store.Register("two@example.test", Password);
        File.WriteAllText(StorePath, "{ not json");

        var reopened = NewStore();

        Assert.True(reopened.Login("one@example.test", Password).Ok);
    }

    [Fact]
    public void A_damaged_store_with_no_good_copy_stops_the_service_and_keeps_the_file()
    {
        File.WriteAllText(StorePath, "{ not json");

        Assert.Throws<InvalidDataException>(() => NewStore());
        Assert.Equal("{ not json", File.ReadAllText(StorePath));
    }

    // ---- F9: request size limits ----

    [Fact]
    public async Task Large_json_body_is_refused_with_413()
    {
        var handler = NewHandler();
        var big = JsonSerializer.Serialize(new { email = "person@example.test", password = new string('x', PortalHandler.MaxJsonBytes) });

        var reply = await handler.HandleAsync(Request("POST", "/api/register", big));

        Assert.Equal(413, reply.Status);
    }

    [Fact]
    public async Task Large_body_without_a_length_is_refused_after_the_cap()
    {
        var handler = NewHandler();
        var request = Request("POST", "/api/login", new string(' ', PortalHandler.MaxJsonBytes + 10));
        var unknown = new PortalRequest
        {
            Method = request.Method,
            Path = request.Path,
            Headers = request.Headers,
            ContentLength = -1,
            Body = request.Body,
        };

        Assert.Equal(413, (await handler.HandleAsync(unknown)).Status);
    }

    [Fact]
    public async Task Large_admin_form_is_refused_with_413()
    {
        var handler = NewHandler();

        var reply = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/login", "email=" + new string('a', PortalHandler.MaxFormBytes)));

        Assert.Equal(413, reply.Status);
    }

    [Fact]
    public async Task Api_post_needs_a_json_content_type()
    {
        var handler = NewHandler();

        var reply = await handler.HandleAsync(Request("POST", "/api/register", Creds("person@example.test"), contentType: "text/plain"));

        Assert.Equal(415, reply.Status);
    }

    [Fact]
    public async Task Long_password_is_refused_without_hashing()
    {
        var store = NewStore();

        Assert.Equal(AuthStatus.Failed, store.Register("person@example.test", new string('x', AccountStore.MaxPasswordLength + 1)).Status);
        Assert.Equal(400, (await NewHandler(store).HandleAsync(Request("POST", "/api/register", Creds("p@example.test", new string('x', 300))))).Status);
    }

    // ---- F10: heartbeat matched to the signed-in user ----

    [Fact]
    public async Task Heartbeat_from_another_account_does_not_overwrite_the_first_record()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        var alice = await RegisterAsync(handler, "alice@example.test");
        var bob = await RegisterAsync(handler, "bob@example.test");

        var beat = JsonSerializer.Serialize(new { installId = InstallId, summary = new { windows = "11", cpu = "A", memory = "8", disk = "1" } });
        var beat2 = JsonSerializer.Serialize(new { installId = InstallId, summary = new { windows = "10", cpu = "B", memory = "4", disk = "2" } });
        Assert.Equal(200, (await handler.HandleAsync(Request("POST", "/api/heartbeat", beat, bearer: alice))).Status);
        Assert.Equal(200, (await handler.HandleAsync(Request("POST", "/api/heartbeat", beat2, bearer: bob))).Status);

        var installs = store.Snapshot().Installs;
        var users = store.Snapshot().Users;
        var aliceId = users.Single(u => u.Email == "alice@example.test").Id;
        var aliceRecord = Assert.Single(installs, item => item.UserId == aliceId);
        Assert.Contains("CPU A", aliceRecord.Summary);
        Assert.Equal("alice@example.test", aliceRecord.Email);
        Assert.Equal(2, installs.Count);
    }

    [Fact]
    public async Task Heartbeat_ignores_an_email_in_the_body_and_needs_a_good_install_id()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        var token = await RegisterAsync(handler, "alice@example.test");

        var spoof = JsonSerializer.Serialize(new { installId = InstallId, email = "someone-else@example.test", summary = new { cpu = "A" } });
        var bad = JsonSerializer.Serialize(new { installId = "not-a-guid", summary = new { cpu = "A" } });
        await handler.HandleAsync(Request("POST", "/api/heartbeat", spoof, bearer: token));

        Assert.Equal("alice@example.test", Assert.Single(store.Snapshot().Installs).Email);
        Assert.Equal(400, (await handler.HandleAsync(Request("POST", "/api/heartbeat", bad, bearer: token))).Status);
    }

    [Fact]
    public async Task Heartbeat_without_a_token_is_401()
    {
        var handler = NewHandler();
        var beat = JsonSerializer.Serialize(new { installId = InstallId });

        Assert.Equal(401, (await handler.HandleAsync(Request("POST", "/api/heartbeat", beat))).Status);
    }

    // ---- F11: hashed tokens and cleanup ----

    [Fact]
    public async Task Store_file_holds_token_hashes_only()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        var token = await RegisterAsync(handler);

        var text = File.ReadAllText(StorePath);

        Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(AccountStore.HashToken(token), text);
    }

    [Fact]
    public void Old_raw_token_sessions_are_hashed_on_load_and_old_owner_sessions_are_dropped()
    {
        var expires = _clock.Now.AddDays(3);
        var old = new
        {
            Users = new object[]
            {
                new { Id = "u1", Email = "person@example.test", Salt = "AAAAAAAAAAAAAAAAAAAAAA==", Hash = "AAAA", Role = "user" },
                new { Id = "o1", Email = "owner@example.test", Salt = "AAAAAAAAAAAAAAAAAAAAAA==", Hash = "AAAA", Role = "owner" },
            },
            Sessions = new object[]
            {
                new { Token = "USERTOKEN1", UserId = "u1", Expires = expires },
                new { Token = "OWNERTOKEN1", UserId = "o1", Expires = expires },
            },
            Installs = Array.Empty<object>(),
            Events = Array.Empty<object>(),
        };
        File.WriteAllText(StorePath, JsonSerializer.Serialize(old));

        var store = NewStore();

        Assert.NotNull(store.UserForToken("USERTOKEN1", SessionKind.App));
        Assert.Null(store.UserForToken("OWNERTOKEN1", SessionKind.Admin));
        Assert.Null(store.UserForToken("OWNERTOKEN1", SessionKind.App));
        var text = File.ReadAllText(StorePath);
        Assert.DoesNotContain("USERTOKEN1", text);
        Assert.DoesNotContain("OWNERTOKEN1", text);
    }

    [Fact]
    public void Expired_sessions_are_removed()
    {
        var store = NewStore();
        store.Register("one@example.test", Password);
        store.Register("two@example.test", Password);
        Assert.Equal(2, store.SessionCount);

        _clock.Now += AccountStore.AppSessionLife + TimeSpan.FromMinutes(1);
        store.Register("three@example.test", Password);

        Assert.Equal(1, store.SessionCount);
        Assert.Equal(1, NewStore().SessionCount);
    }

    [Fact]
    public void App_tokens_and_admin_tokens_do_not_cross()
    {
        var store = NewStore();
        var admin = store.SetupOwner(store.SetupCode, "owner@example.test", Password).Token;
        var app = store.Register("person@example.test", Password).Token;

        Assert.Null(store.UserForToken(admin, SessionKind.App));
        Assert.Null(store.UserForToken(app, SessionKind.Admin));
    }

    [Fact]
    public void Sessions_per_user_are_capped()
    {
        var store = NewStore();
        store.Register("person@example.test", Password);
        for (int i = 0; i < 15; i++)
        {
            store.Login("person@example.test", Password);
        }

        Assert.Equal(10, store.SessionCount);
    }

    // ---- F12: 600,000 rounds and upgrade on login ----

    [Fact]
    public void New_hashes_use_600000_rounds()
    {
        Assert.Equal(600_000, AccountStore.DefaultIterations);
        var store = NewStore(AccountStore.DefaultIterations);
        store.Register("person@example.test", Password);

        var user = Assert.Single(store.Snapshot().Users);
        Assert.Equal(600_000, user.Iterations);
        var expected = Rfc2898DeriveBytes.Pbkdf2(Password, Convert.FromBase64String(user.Salt), 600_000, HashAlgorithmName.SHA256, 32);
        Assert.Equal(Convert.ToBase64String(expected), user.Hash);
    }

    [Fact]
    public void Old_100000_round_hash_signs_in_and_is_upgraded()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var oldHash = Rfc2898DeriveBytes.Pbkdf2(Password, salt, AccountStore.LegacyIterations, HashAlgorithmName.SHA256, 32);
        var old = new
        {
            Users = new[] { new { Id = "u1", Email = "person@example.test", Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(oldHash), Role = "user" } },
        };
        File.WriteAllText(StorePath, JsonSerializer.Serialize(old));
        var store = NewStore(AccountStore.DefaultIterations);

        Assert.False(store.Login("person@example.test", "wrong-password").Ok);
        Assert.Equal(0, store.Snapshot().Users.Single().Iterations);
        Assert.True(store.Login("person@example.test", Password).Ok);

        var upgraded = store.Snapshot().Users.Single();
        Assert.Equal("u1", upgraded.Id);
        Assert.Equal(600_000, upgraded.Iterations);
        Assert.NotEqual(Convert.ToBase64String(oldHash), upgraded.Hash);
        Assert.True(NewStore(AccountStore.DefaultIterations).Login("person@example.test", Password).Ok);
    }

    // ---- F13: headers, logout, 12 hour admin session ----

    [Theory]
    [InlineData("GET", "/api/me")]
    [InlineData("GET", "/quantumupdater/admin")]
    [InlineData("GET", "/nothing")]
    [InlineData("POST", "/api/register")]
    public async Task Every_reply_has_security_headers(string method, string path)
    {
        var handler = NewHandler();

        var reply = await handler.HandleAsync(Request(method, path, method == "POST" ? "{ bad" : null));

        Assert.Equal("nosniff", reply.Header("X-Content-Type-Options"));
        Assert.Equal("DENY", reply.Header("X-Frame-Options"));
        Assert.Contains("frame-ancestors 'none'", reply.Header("Content-Security-Policy"));
        Assert.Contains("default-src 'none'", reply.Header("Content-Security-Policy"));
        Assert.Equal("no-referrer", reply.Header("Referrer-Policy"));
        Assert.Equal("no-store", reply.Header("Cache-Control"));
    }

    [Fact]
    public async Task Admin_cookie_lasts_12_hours_and_is_http_only_strict()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        var reply = await handler.HandleAsync(Form(
            PortalHandler.AdminPath + "/setup",
            $"setupCode={store.SetupCode}&email=owner%40example.test&password={Encode(Password)}"));
        var cookie = reply.Header("Set-Cookie")!;
        var token = CookieToken(reply);

        Assert.Contains("HttpOnly", cookie);
        Assert.Contains("SameSite=Strict", cookie);
        Assert.Contains("Max-Age=43200", cookie);
        Assert.Contains("Path=/quantumupdater/admin", cookie);

        _clock.Now += TimeSpan.FromHours(11);
        Assert.NotNull(store.UserForToken(token, SessionKind.Admin));
        _clock.Now += TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1);
        Assert.Null(store.UserForToken(token, SessionKind.Admin));
    }

    [Fact]
    public async Task Admin_logout_ends_the_session_and_clears_the_cookie()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        var token = await SetupOwnerAsync(handler, store);

        var reply = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/logout", "", cookie: $"{PortalHandler.AdminCookie}={token}"));

        Assert.Equal(303, reply.Status);
        Assert.Contains("Max-Age=0", reply.Header("Set-Cookie"));
        Assert.Null(store.UserForToken(token, SessionKind.Admin));
        var page = await handler.HandleAsync(Request("GET", PortalHandler.AdminPath, cookie: $"{PortalHandler.AdminCookie}={token}"));
        Assert.DoesNotContain("Counts", page.BodyText);
    }

    [Fact]
    public async Task Admin_forms_from_another_site_are_refused()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        var token = await SetupOwnerAsync(handler, store);

        var reply = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/logout", "", cookie: $"{PortalHandler.AdminCookie}={token}", origin: "https://evil.example"));

        Assert.Equal(403, reply.Status);
        Assert.NotNull(store.UserForToken(token, SessionKind.Admin));
    }

    [Fact]
    public async Task Another_host_name_is_refused()
    {
        var handler = NewHandler();

        var reply = await handler.HandleAsync(Request("GET", "/api/me", host: "evil.example:8787"));

        Assert.Equal(400, reply.Status);
    }

    [Fact]
    public async Task Owner_cannot_use_the_admin_token_on_the_api_or_sign_in_to_the_app()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        var admin = await SetupOwnerAsync(handler, store);

        Assert.Equal(401, (await handler.HandleAsync(Request("GET", "/api/me", bearer: admin))).Status);
        var login = await handler.HandleAsync(Request("POST", "/api/login", Creds("owner@example.test")));
        Assert.Equal(403, login.Status);
        Assert.Null(TokenOf(login));
    }

    // ---- F14: generic errors, no enumeration ----

    [Fact]
    public async Task Bad_json_gets_a_generic_message_with_no_exception_text()
    {
        var handler = NewHandler();

        var reply = await handler.HandleAsync(Request("POST", "/api/login", "{\"email\": [1, 2"));

        Assert.Equal(400, reply.Status);
        Assert.Equal(PortalHandler.GenericError, MessageOf(reply));
        Assert.DoesNotContain("Exception", reply.BodyText);
        Assert.DoesNotContain("   at ", reply.BodyText);
        Assert.DoesNotContain("LineNumber", reply.BodyText);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_give_the_same_reply()
    {
        var handler = NewHandler();
        await RegisterAsync(handler, "person@example.test");

        var wrong = await handler.HandleAsync(Request("POST", "/api/login", Creds("person@example.test", "wrong-password")));
        var unknown = await handler.HandleAsync(Request("POST", "/api/login", Creds("nobody@example.test", "wrong-password")));

        Assert.Equal(401, wrong.Status);
        Assert.Equal(wrong.Status, unknown.Status);
        Assert.Equal(wrong.BodyText, unknown.BodyText);
    }

    [Fact]
    public async Task Admin_sign_in_failures_give_one_message()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        await SetupOwnerAsync(handler, store);
        await RegisterAsync(handler, "person@example.test");

        var wrong = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/login", "email=owner%40example.test&password=wrong-password"));
        var notOwner = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/login", $"email=person%40example.test&password={Encode(Password)}"));
        var unknown = await handler.HandleAsync(Form(PortalHandler.AdminPath + "/login", "email=nobody%40example.test&password=wrong-password"));

        Assert.Equal(wrong.BodyText, notOwner.BodyText);
        Assert.Equal(wrong.BodyText, unknown.BodyText);
        Assert.Contains(PortalHandler.AdminFailed, wrong.BodyText);
    }

    [Fact]
    public async Task Admin_page_encodes_stored_values()
    {
        var store = NewStore();
        var handler = NewHandler(store);
        var admin = await SetupOwnerAsync(handler, store);
        var token = await RegisterAsync(handler, "person@example.test");
        var activity = JsonSerializer.Serialize(new { installId = InstallId, action = "install", packageId = "<script>x</script>", success = true });
        await handler.HandleAsync(Request("POST", "/api/activity", activity, bearer: token));

        var page = await handler.HandleAsync(Request("GET", PortalHandler.AdminPath, cookie: $"{PortalHandler.AdminCookie}={admin}"));

        Assert.DoesNotContain("<script>x</script>", page.BodyText);
        Assert.Contains("&lt;script&gt;", page.BodyText);
    }

    [Fact]
    public void Same_secret_compares_bytes()
    {
        Assert.True(AccountStore.SameSecret("ABC", "ABC"));
        Assert.False(AccountStore.SameSecret("ABC", "ABD"));
        Assert.False(AccountStore.SameSecret("ABC", "ABCD"));
        Assert.False(AccountStore.SameSecret(null, "ABC"));
    }
}
