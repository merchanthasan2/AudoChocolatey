using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var store = new AccountStore();
var listener = new HttpListener();
listener.Prefixes.Add("http://127.0.0.1:8787/");
listener.Start();
Console.WriteLine("DeskVerve account service at http://127.0.0.1:8787/quantumupdater/admin");

while (listener.IsListening)
{
    var context = await listener.GetContextAsync().ConfigureAwait(false);
    _ = Task.Run(() => Handle(context, store));
}

static async Task Handle(HttpListenerContext context, AccountStore store)
{
    try
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var method = context.Request.HttpMethod;
        if (method == "GET" && path == "/quantumupdater/admin")
        {
            await WriteHtml(context, store.AdminPage(ReadCookie(context))).ConfigureAwait(false);
            return;
        }

        if (method == "POST" && path == "/quantumupdater/admin/login")
        {
            var form = await ReadForm(context).ConfigureAwait(false);
            var (ok, token, message) = store.AdminLogin(form.GetValueOrDefault("email"), form.GetValueOrDefault("password"));
            if (ok && token is not null)
            {
                context.Response.Headers.Add("Set-Cookie", $"qu_admin={token}; HttpOnly; SameSite=Strict; Path=/quantumupdater/admin");
                Redirect(context, "/quantumupdater/admin");
                return;
            }

            await WriteHtml(context, store.AdminPage(null, message)).ConfigureAwait(false);
            return;
        }

        if (method == "POST" && path == "/api/register")
        {
            var body = await ReadJson(context).ConfigureAwait(false);
            var (ok, token, message) = store.Register(body.Email, body.Password, owner: false);
            await WriteJson(context, ok ? 200 : 400, new { ok, message, token }).ConfigureAwait(false);
            return;
        }

        if (method == "POST" && path == "/api/login")
        {
            var body = await ReadJson(context).ConfigureAwait(false);
            var (ok, token, message) = store.Login(body.Email, body.Password);
            await WriteJson(context, ok ? 200 : 401, new { ok, message, token }).ConfigureAwait(false);
            return;
        }

        if (method == "GET" && path == "/api/me")
        {
            // F7: the app checks its stored session at start. Any 401 makes it sign out.
            var me = store.UserForToken(ReadBearer(context));
            if (me is null || me.Role == "owner")
            {
                await WriteJson(context, 401, new { ok = false, message = "Sign in required." }).ConfigureAwait(false);
                return;
            }

            await WriteJson(context, 200, new { ok = true, message = "Signed in.", email = me.Email }).ConfigureAwait(false);
            return;
        }

        if (method == "POST" && path is "/api/heartbeat" or "/api/activity")
        {
            var user = store.UserForToken(ReadBearer(context));
            if (user is null || user.Role == "owner")
            {
                await WriteJson(context, 401, new { ok = false, message = "Sign in required." }).ConfigureAwait(false);
                return;
            }

            var body = await ReadJson(context).ConfigureAwait(false);
            if (path == "/api/heartbeat")
            {
                store.Heartbeat(user, body.InstallId, body.Summary);
            }
            else
            {
                store.Activity(user, body.InstallId, body.Action, body.PackageId, body.Success);
            }

            await WriteJson(context, 200, new { ok = true, message = "Saved." }).ConfigureAwait(false);
            return;
        }

        await WriteJson(context, 404, new { ok = false, message = "Not found." }).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        try
        {
            await WriteJson(context, 400, new { ok = false, message = ex.Message }).ConfigureAwait(false);
        }
        catch
        {
            context.Response.Abort();
        }
    }
}

static string? ReadCookie(HttpListenerContext context)
{
    var header = context.Request.Headers["Cookie"];
    if (string.IsNullOrEmpty(header))
    {
        return null;
    }

    foreach (var part in header.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var pair = part.Split('=', 2);
        if (pair.Length == 2 && pair[0].Trim() == "qu_admin")
        {
            return pair[1].Trim();
        }
    }

    return null;
}

static string? ReadBearer(HttpListenerContext context)
{
    var header = context.Request.Headers["Authorization"];
    if (header is null || !header.StartsWith("Bearer ", StringComparison.Ordinal))
    {
        return null;
    }

    return header["Bearer ".Length..].Trim();
}

static async Task<ApiBody> ReadJson(HttpListenerContext context)
{
    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
    var text = await reader.ReadToEndAsync().ConfigureAwait(false);
    if (text.Length > 8192)
    {
        throw new InvalidOperationException("Request is too large.");
    }

    return JsonSerializer.Deserialize<ApiBody>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Missing body.");
}

static async Task<Dictionary<string, string>> ReadForm(HttpListenerContext context)
{
    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
    var text = await reader.ReadToEndAsync().ConfigureAwait(false);
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var pair = part.Split('=', 2);
        var key = Uri.UnescapeDataString(pair[0]);
        var value = pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace("+", " ")) : "";
        result[key] = value;
    }

    return result;
}

static void Redirect(HttpListenerContext context, string path)
{
    context.Response.StatusCode = 303;
    context.Response.Headers.Add("Location", path);
    context.Response.Close();
}

static async Task WriteJson(HttpListenerContext context, int status, object body)
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
    context.Response.StatusCode = status;
    context.Response.ContentType = "application/json";
    context.Response.ContentLength64 = bytes.Length;
    await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    context.Response.Close();
}

static async Task WriteHtml(HttpListenerContext context, string html)
{
    var bytes = Encoding.UTF8.GetBytes(html);
    context.Response.StatusCode = 200;
    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.ContentLength64 = bytes.Length;
    await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    context.Response.Close();
}

sealed record ApiBody(string? Email, string? Password, string? InstallId, string? Action, string? PackageId, bool Success, JsonElement Summary);

sealed class AccountStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private Database _db;

    public AccountStore()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantumUpdater");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "account-store.json");
        _db = File.Exists(_path)
            ? JsonSerializer.Deserialize<Database>(File.ReadAllText(_path)) ?? new Database()
            : new Database();
    }

    public (bool Ok, string? Token, string Message) Register(string? email, string? password, bool owner)
    {
        var (okEmail, normal) = CleanEmail(email);
        var passwordError = CleanPassword(password);
        if (!okEmail)
        {
            return (false, null, "Enter a valid email.");
        }

        if (passwordError is not null)
        {
            return (false, null, passwordError);
        }

        lock (_gate)
        {
            if (_db.Users.Any(user => user.Email.Equals(normal, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, null, "That account already exists. Sign in.");
            }

            var user = NewUser(normal, password!, owner ? "owner" : "user");
            _db.Users.Add(user);
            var token = NewSession(user);
            Save();
            return (true, token, "Account created.");
        }
    }

    public (bool Ok, string? Token, string Message) Login(string? email, string? password)
    {
        lock (_gate)
        {
            var user = Find(email, password);
            if (user is null)
            {
                return (false, null, "Email or password is wrong.");
            }

            var token = NewSession(user);
            _db.Events.Add(new TrackEvent(DateTimeOffset.UtcNow, user.Id, "", "sign-in", "", true));
            Save();
            return (true, token, "Signed in.");
        }
    }

    public (bool Ok, string? Token, string Message) AdminLogin(string? email, string? password)
    {
        lock (_gate)
        {
            if (_db.Users.All(user => user.Role != "owner"))
            {
                var (okEmail, normal) = CleanEmail(email);
                var passwordError = CleanPassword(password);
                if (!okEmail || passwordError is not null)
                {
                    return (false, null, passwordError ?? "Enter the first owner email and a password of 8 or more characters.");
                }

                var created = NewUser(normal, password!, "owner");
                _db.Users.Add(created);
                var createdToken = NewSession(created);
                Save();
                return (true, createdToken, "Owner account created.");
            }

            var user = Find(email, password);
            if (user is null || user.Role != "owner")
            {
                return (false, null, "Owner sign-in failed.");
            }

            var token = NewSession(user);
            Save();
            return (true, token, "Signed in.");
        }
    }

    public User? UserForToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        lock (_gate)
        {
            var session = _db.Sessions.FirstOrDefault(item => item.Token == token && item.Expires > DateTimeOffset.UtcNow);
            return session is null ? null : _db.Users.FirstOrDefault(user => user.Id == session.UserId);
        }
    }

    public void Heartbeat(User user, string? installId, JsonElement summary)
    {
        var id = CleanId(installId);
        var safe = SafeSummary(summary);
        lock (_gate)
        {
            var existing = _db.Installs.FirstOrDefault(item => item.InstallId == id);
            if (existing is null)
            {
                _db.Installs.Add(new Install(id, user.Id, user.Email, safe, DateTimeOffset.UtcNow));
            }
            else
            {
                _db.Installs.Remove(existing);
                _db.Installs.Add(existing with { Email = user.Email, Summary = safe, LastSeen = DateTimeOffset.UtcNow });
            }

            Save();
        }
    }

    public void Activity(User user, string? installId, string? action, string? packageId, bool success)
    {
        if (action is not ("install" or "upgrade" or "remove"))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(packageId) || packageId.Length > 180)
        {
            return;
        }

        lock (_gate)
        {
            _db.Events.Add(new TrackEvent(DateTimeOffset.UtcNow, user.Id, CleanId(installId), action, packageId.Trim(), success));
            Save();
        }
    }

    public string AdminPage(string? token, string? error = null)
    {
        var user = UserForToken(token);
        if (user is null || user.Role != "owner")
        {
            var note = error is null ? "The first visit creates the owner account." : error;
            return Page($"""
                <h1>DeskVerve admin</h1>
                <p>{Encode(note)}</p>
                <form method="post" action="/quantumupdater/admin/login">
                  <label>Email <input name="email" type="email" required></label>
                  <label>Password <input name="password" type="password" required minlength="8"></label>
                  <button type="submit">Sign in</button>
                </form>
                """);
        }

        List<Install> installs;
        List<TrackEvent> events;
        List<User> users;
        lock (_gate)
        {
            installs = _db.Installs.ToList();
            events = _db.Events.ToList();
            users = _db.Users.ToList();
        }

        var now = DateTimeOffset.UtcNow;
        var active = installs.Where(item => item.LastSeen > now.AddMinutes(-5)).Select(item => item.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var byDay = events.Where(item => item.Kind == "sign-in")
            .GroupBy(item => item.At.UtcDateTime.ToString("yyyy-MM-dd"))
            .OrderByDescending(group => group.Key)
            .Take(14)
            .Select(group => $"<li>{Encode(group.Key)} · {group.Count()}</li>");
        var updates = events.Where(item => item.Kind == "upgrade").TakeLast(40).Reverse()
            .Select(item => $"<li>{Encode(item.At.ToString("u"))} · {Encode(EmailOf(users, item.UserId))} · {Encode(item.PackageId)} · {(item.Success ? "succeeded" : "failed")}</li>");
        var apps = events.Where(item => item.Kind is "install" or "upgrade" or "remove").TakeLast(40).Reverse()
            .Select(item => $"<li>{Encode(item.At.ToString("u"))} · {Encode(item.Kind)} · {Encode(item.PackageId)} · {Encode(EmailOf(users, item.UserId))}</li>");
        var systems = installs.Select(item => $"<li>{Encode(item.Email)} · {Encode(item.Summary)}</li>");

        return Page($"""
            <h1>DeskVerve admin</h1>
            <section><h2>Counts</h2><p>Installs {installs.Count}. Signed in now {active}. Accounts {users.Count(item => item.Role == "user")}.</p><ul>{string.Join("", byDay)}</ul></section>
            <section><h2>Updates</h2><ul>{string.Join("", updates)}</ul></section>
            <section><h2>Apps</h2><ul>{string.Join("", apps)}</ul></section>
            <section><h2>Systems</h2><ul>{string.Join("", systems)}</ul></section>
            """);
    }

    private static string EmailOf(List<User> users, string userId) =>
        users.FirstOrDefault(user => user.Id == userId)?.Email ?? "unknown";

    private User? Find(string? email, string? password)
    {
        var (okEmail, normal) = CleanEmail(email);
        if (!okEmail || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var user = _db.Users.FirstOrDefault(item => item.Email.Equals(normal, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            return null;
        }

        var hash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(user.Salt), 100_000, HashAlgorithmName.SHA256, 32));
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(user.Hash), Convert.FromBase64String(hash)) ? user : null;
    }

    private static User NewUser(string email, string password, string role)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return new User(Guid.NewGuid().ToString("D"), email, Convert.ToBase64String(salt), Convert.ToBase64String(hash), role);
    }

    private string NewSession(User user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _db.Sessions.Add(new Session(token, user.Id, DateTimeOffset.UtcNow.AddDays(14)));
        return token;
    }

    private static (bool Ok, string Email) CleanEmail(string? email)
    {
        var normal = (email ?? "").Trim().ToLowerInvariant();
        var ok = normal.Length is >= 6 and <= 120 && normal.Contains('@') && !normal.Contains(' ');
        return (ok, normal);
    }

    private static string? CleanPassword(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < 8 ? "Use a password of 8 or more characters." : null;

    private static string CleanId(string? installId) =>
        Guid.TryParse(installId, out var id) ? id.ToString("D") : "unknown";

    private static string SafeSummary(JsonElement summary)
    {
        if (summary.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        string Field(string name) =>
            summary.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? "").Replace('\n', ' ').Trim()
                : "";

        var text = $"Windows {Field("windows")}; CPU {Field("cpu")}; Memory {Field("memory")}; Disk {Field("disk")}";
        return text.Length > 400 ? text[..400] : text;
    }

    private void Save()
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(_db));
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string Page(string body) => $$"""
        <!doctype html>
        <html><head><meta charset="utf-8"><title>DeskVerve admin</title>
        <style>
          body { margin: 32px auto; max-width: 880px; background: #131313; color: #fff; font: 15px Segoe UI, sans-serif; }
          h1 { font-size: 28px; } section { background: #2c2c2c; border: 1px solid #383838; border-radius: 8px; padding: 16px; margin: 12px 0; }
          input { display: block; margin: 8px 0 12px; padding: 8px; width: 280px; }
          button { background: #0078d4; color: #fff; border: 0; border-radius: 4px; padding: 8px 14px; }
          li { margin: 4px 0; color: #d1d1d1; }
        </style></head><body>{{body}}</body></html>
        """;

    private sealed class Database
    {
        public List<User> Users { get; set; } = [];
        public List<Session> Sessions { get; set; } = [];
        public List<Install> Installs { get; set; } = [];
        public List<TrackEvent> Events { get; set; } = [];
    }
}

sealed record User(string Id, string Email, string Salt, string Hash, string Role);
sealed record Session(string Token, string UserId, DateTimeOffset Expires);
sealed record Install(string InstallId, string UserId, string Email, string Summary, DateTimeOffset LastSeen);
sealed record TrackEvent(DateTimeOffset At, string UserId, string InstallId, string Kind, string PackageId, bool Success);
