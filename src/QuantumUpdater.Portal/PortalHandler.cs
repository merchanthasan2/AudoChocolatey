using System.Net;
using System.Text;
using System.Text.Json;

namespace QuantumUpdater.Portal;

/// <summary>One request, read from the listener. Tests build it directly.</summary>
public sealed class PortalRequest
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    /// <summary>The address of the caller. Used only as a rate limit key.</summary>
    public string RemoteAddress { get; init; } = "unknown";

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Length from the Content-Length header, or -1 when not given.</summary>
    public long ContentLength { get; init; } = -1;

    public Stream Body { get; init; } = Stream.Null;

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>One reply. The listener copies it out.</summary>
public sealed class PortalResponse
{
    public int Status { get; set; } = 200;

    public string ContentType { get; set; } = "application/json";

    public byte[] Body { get; set; } = [];

    public List<KeyValuePair<string, string>> Headers { get; } = new();

    public string? Header(string name) =>
        Headers.Where(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value).LastOrDefault();

    public string BodyText => Encoding.UTF8.GetString(Body);
}

/// <summary>
/// Routes for the account service. The desktop app uses /api/* only. The admin site is /quantumupdater/admin.
/// Every reply carries security headers (F13). Error replies are plain and never hold exception text (F14).
/// </summary>
public sealed class PortalHandler
{
    public const string Authority = "127.0.0.1:8787";
    public const string Origin = "http://" + Authority;
    public const string AdminPath = "/quantumupdater/admin";
    public const string AdminCookie = "qu_admin";

    /// <summary>F9: largest JSON body.</summary>
    public const int MaxJsonBytes = 8 * 1024;

    /// <summary>F9: largest admin form body.</summary>
    public const int MaxFormBytes = 4 * 1024;

    internal const string GenericError = "The request could not be handled.";
    internal const string TooMany = "Too many tries. Wait a few minutes and try again.";
    internal const string SignInRequired = "Sign in required.";
    internal const string LoginFailed = "Email or password is wrong.";
    internal const string RegisterFailed = "Enter a valid email and a password of 8 to 256 characters.";
    internal const string RegisterExists = "Could not create the account. If you already have one, sign in.";
    internal const string OwnerInApp = "Use a user account in the app. The owner account is for the admin site.";
    internal const string AdminFailed = "Owner sign-in failed.";
    internal const string SetupFailed = "Owner setup failed. Check the setup code, the email and a password of 8 or more characters.";
    internal const string SetupClosed = "Owner setup is closed. Restart the account service to show a new setup code.";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 8,
    };

    private static readonly TimeSpan ShortWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongWindow = TimeSpan.FromMinutes(15);

    private readonly AccountStore _store;
    private readonly RateLimiter _limits;

    public PortalHandler(AccountStore store, RateLimiter? limits = null)
    {
        _store = store;
        _limits = limits ?? new RateLimiter();
    }

    public async Task<PortalResponse> HandleAsync(PortalRequest request)
    {
        PortalResponse response;
        try
        {
            response = await RouteAsync(request).ConfigureAwait(false);
        }
        catch (BodyTooLargeException)
        {
            response = Json(413, false, "The request is too large.");
        }
        catch (Exception ex) when (ex is JsonException or BadRequestException or DecoderFallbackException)
        {
            response = Json(400, false, GenericError);
        }
        catch (Exception)
        {
            // F14: no exception text, no stack trace.
            response = Json(500, false, GenericError);
        }

        AddSecurityHeaders(response);
        return response;
    }

    private async Task<PortalResponse> RouteAsync(PortalRequest request)
    {
        // A page on another site can reach 127.0.0.1 through a renamed host (DNS rebinding). Only our own host name is served.
        if (!string.Equals(request.Header("Host"), Authority, StringComparison.OrdinalIgnoreCase))
        {
            return Json(400, false, GenericError);
        }

        var method = request.Method.ToUpperInvariant();
        var path = request.Path;

        if (path == AdminPath || path.StartsWith(AdminPath + "/", StringComparison.Ordinal))
        {
            return await AdminAsync(request, method, path).ConfigureAwait(false);
        }

        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return await ApiAsync(request, method, path).ConfigureAwait(false);
        }

        return Json(404, false, "Not found.");
    }

    private async Task<PortalResponse> ApiAsync(PortalRequest request, string method, string path)
    {
        if (method == "GET" && path == "/api/me")
        {
            // F7: the app checks its stored session at start. Any 401 makes it sign out.
            var me = _store.UserForToken(ReadBearer(request), SessionKind.App);
            return me is null
                ? Json(401, false, SignInRequired)
                : Reply(200, new { ok = true, message = "Signed in.", email = me.Email });
        }

        if (method != "POST")
        {
            return Json(404, false, "Not found.");
        }

        if (path is not ("/api/register" or "/api/login" or "/api/logout" or "/api/heartbeat" or "/api/activity"))
        {
            return Json(404, false, "Not found.");
        }

        // A web page cannot send JSON here without a preflight, which this service never answers.
        if (!IsJson(request.Header("Content-Type")))
        {
            return Json(415, false, GenericError);
        }

        if (path == "/api/register")
        {
            if (!_limits.TryAcquire("register-ip:" + request.RemoteAddress, 10, TimeSpan.FromHours(1)))
            {
                return Json(429, false, TooMany);
            }

            var body = await ReadJsonAsync(request).ConfigureAwait(false);
            var result = _store.Register(body.Email, body.Password);
            return result.Status switch
            {
                AuthStatus.Ok => Reply(200, new { ok = true, message = "Account created.", token = result.Token }),
                AuthStatus.Exists => Json(400, false, RegisterExists),
                _ => Json(400, false, RegisterFailed),
            };
        }

        if (path == "/api/login")
        {
            var body = await ReadJsonAsync(request).ConfigureAwait(false);
            var emailKey = "login-email:" + KeyPart(body.Email);
            if (!_limits.TryAcquire("login-ip:" + request.RemoteAddress, 30, ShortWindow)
                || !_limits.TryAcquire(emailKey, 10, LongWindow))
            {
                return Json(429, false, TooMany);
            }

            var result = _store.Login(body.Email, body.Password);
            switch (result.Status)
            {
                case AuthStatus.Ok:
                    _limits.Reset(emailKey);
                    return Reply(200, new { ok = true, message = "Signed in.", token = result.Token });
                case AuthStatus.OwnerNotAllowed:
                    return Json(403, false, OwnerInApp);
                default:
                    return Json(401, false, LoginFailed);
            }
        }

        if (path == "/api/logout")
        {
            _store.Logout(ReadBearer(request), SessionKind.App);
            return Json(200, true, "Signed out.");
        }

        var user = _store.UserForToken(ReadBearer(request), SessionKind.App);
        if (user is null)
        {
            return Json(401, false, SignInRequired);
        }

        var data = await ReadJsonAsync(request).ConfigureAwait(false);
        bool saved = path == "/api/heartbeat"
            ? _store.Heartbeat(user, data.InstallId, data.Summary)
            : _store.Activity(user, data.InstallId, data.Action, data.PackageId, data.Success);
        return saved ? Json(200, true, "Saved.") : Json(400, false, GenericError);
    }

    private async Task<PortalResponse> AdminAsync(PortalRequest request, string method, string path)
    {
        if (method == "GET" && path == AdminPath)
        {
            var owner = _store.UserForToken(ReadCookie(request), SessionKind.Admin);
            return owner is { Role: "owner" } ? Html(200, AdminPages.Dashboard(_store.Snapshot())) : Html(200, SignInPage(null));
        }

        if (method != "POST" || path is not (AdminPath + "/login" or AdminPath + "/setup" or AdminPath + "/logout"))
        {
            return Html(404, AdminPages.Page("<h1>Not found</h1>"));
        }

        // Admin forms are posted only from this site.
        if (!SameOrigin(request))
        {
            return Html(403, AdminPages.Page("<h1>DeskVerve admin</h1><p>That request came from another site.</p>"));
        }

        if (path == AdminPath + "/logout")
        {
            _store.Logout(ReadCookie(request), SessionKind.Admin);
            var bye = Redirect(AdminPath);
            bye.Headers.Add(new("Set-Cookie", $"{AdminCookie}=; HttpOnly; SameSite=Strict; Path={AdminPath}; Max-Age=0"));
            return bye;
        }

        var form = await ReadFormAsync(request).ConfigureAwait(false);
        var email = form.GetValueOrDefault("email");
        var emailKey = "admin-email:" + KeyPart(email);
        if (!_limits.TryAcquire("admin-ip:" + request.RemoteAddress, 10, LongWindow)
            || !_limits.TryAcquire(emailKey, 5, LongWindow))
        {
            return Html(429, SignInPage(TooMany));
        }

        AuthResult result = path == AdminPath + "/setup"
            ? _store.SetupOwner(form.GetValueOrDefault("setupCode"), email, form.GetValueOrDefault("password"))
            : _store.AdminLogin(email, form.GetValueOrDefault("password"));
        if (result.Ok && result.Token is not null)
        {
            _limits.Reset(emailKey);
            var signedIn = Redirect(AdminPath);
            int maxAge = (int)AccountStore.AdminSessionLife.TotalSeconds;
            signedIn.Headers.Add(new("Set-Cookie", $"{AdminCookie}={result.Token}; HttpOnly; SameSite=Strict; Path={AdminPath}; Max-Age={maxAge}"));
            return signedIn;
        }

        string message = result.Status switch
        {
            AuthStatus.SetupClosed => SetupClosed,
            _ when path == AdminPath + "/setup" => SetupFailed,
            _ => AdminFailed,
        };
        return Html(200, SignInPage(message));
    }

    private string SignInPage(string? message) =>
        _store.HasOwner ? AdminPages.Login(message) : AdminPages.Setup(message, _store.SetupCode is not null);

    // Rate limit keys stay short so a long email cannot grow the table.
    private static string KeyPart(string? email)
    {
        var normal = (email ?? "").Trim().ToLowerInvariant();
        return normal.Length > 130 ? normal[..130] : normal;
    }

    private static bool SameOrigin(PortalRequest request)
    {
        var origin = request.Header("Origin");
        if (origin is not null)
        {
            return string.Equals(origin, Origin, StringComparison.OrdinalIgnoreCase);
        }

        // No Origin (older browsers, tools): fall back to Referer when there is one.
        var referer = request.Header("Referer");
        return referer is null || referer.StartsWith(Origin + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJson(string? contentType)
    {
        if (contentType is null)
        {
            return false;
        }

        var media = contentType.Split(';', 2)[0].Trim();
        return media.Equals("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadBearer(PortalRequest request)
    {
        var header = request.Header("Authorization");
        if (header is null || !header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return null;
        }

        return header["Bearer ".Length..].Trim();
    }

    private static string? ReadCookie(PortalRequest request)
    {
        var header = request.Header("Cookie");
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        foreach (var part in header.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim() == AdminCookie)
            {
                return pair[1].Trim();
            }
        }

        return null;
    }

    // F9: refuse by the declared length first, then stop reading one byte past the cap.
    private static async Task<string> ReadBodyAsync(PortalRequest request, int max)
    {
        if (request.ContentLength > max)
        {
            throw new BodyTooLargeException();
        }

        var buffer = new byte[max + 1];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await request.Body.ReadAsync(buffer.AsMemory(total, buffer.Length - total)).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total > max)
        {
            throw new BodyTooLargeException();
        }

        return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(buffer, 0, total);
    }

    private static async Task<ApiBody> ReadJsonAsync(PortalRequest request)
    {
        var text = await ReadBodyAsync(request, MaxJsonBytes).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ApiBody>(text, ReadOptions) ?? throw new BadRequestException();
    }

    private static async Task<Dictionary<string, string>> ReadFormAsync(PortalRequest request)
    {
        var text = await ReadBodyAsync(request, MaxFormBytes).ConfigureAwait(false);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = WebUtility.UrlDecode(pair[0]);
            var value = pair.Length == 2 ? WebUtility.UrlDecode(pair[1]) : "";
            result[key] = value;
        }

        return result;
    }

    private static PortalResponse Json(int status, bool ok, string message) => Reply(status, new { ok, message });

    private static PortalResponse Reply(int status, object body) => new()
    {
        Status = status,
        ContentType = "application/json; charset=utf-8",
        Body = JsonSerializer.SerializeToUtf8Bytes(body),
    };

    private static PortalResponse Html(int status, string html) => new()
    {
        Status = status,
        ContentType = "text/html; charset=utf-8",
        Body = Encoding.UTF8.GetBytes(html),
    };

    private static PortalResponse Redirect(string path)
    {
        var response = new PortalResponse { Status = 303, ContentType = "text/plain; charset=utf-8" };
        response.Headers.Add(new("Location", path));
        return response;
    }

    // F13: on every reply.
    private static void AddSecurityHeaders(PortalResponse response)
    {
        response.Headers.Add(new("X-Content-Type-Options", "nosniff"));
        response.Headers.Add(new("X-Frame-Options", "DENY"));
        response.Headers.Add(new("Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'"));
        response.Headers.Add(new("Referrer-Policy", "no-referrer"));
        response.Headers.Add(new("Cross-Origin-Opener-Policy", "same-origin"));
        response.Headers.Add(new("Cross-Origin-Resource-Policy", "same-origin"));
        response.Headers.Add(new("Cache-Control", "no-store"));
    }

    private sealed record ApiBody(string? Email, string? Password, string? InstallId, string? Action, string? PackageId, bool Success, JsonElement Summary);

    private sealed class BodyTooLargeException : Exception
    {
    }

    private sealed class BadRequestException : Exception
    {
    }
}

/// <summary>The admin site HTML. Every value from the store is HTML-encoded.</summary>
internal static class AdminPages
{
    public static string Login(string? message) => Page($"""
        <h1>DeskVerve admin</h1>
        <p>{Encode(message ?? "Sign in with the owner account.")}</p>
        <form method="post" action="{PortalHandler.AdminPath}/login">
          <label>Email <input name="email" type="email" required maxlength="120"></label>
          <label>Password <input name="password" type="password" required minlength="8" maxlength="256"></label>
          <button type="submit">Sign in</button>
        </form>
        """);

    public static string Setup(string? message, bool open)
    {
        if (!open)
        {
            return Page($"""
                <h1>DeskVerve admin</h1>
                <p>{Encode(message ?? PortalHandler.SetupClosed)}</p>
                """);
        }

        return Page($"""
            <h1>DeskVerve admin</h1>
            <p>{Encode(message ?? "No owner account yet. Enter the setup code shown in the account service window.")}</p>
            <form method="post" action="{PortalHandler.AdminPath}/setup">
              <label>Setup code <input name="setupCode" required autocomplete="off" maxlength="64"></label>
              <label>Owner email <input name="email" type="email" required maxlength="120"></label>
              <label>Password <input name="password" type="password" required minlength="8" maxlength="256"></label>
              <button type="submit">Create owner account</button>
            </form>
            """);
    }

    public static string Dashboard((List<User> Users, List<Install> Installs, List<TrackEvent> Events) data)
    {
        var (users, installs, events) = data;
        var now = DateTimeOffset.UtcNow;
        var active = installs.Where(item => item.LastSeen > now.AddMinutes(-5)).Select(item => item.UserId).Distinct().Count();
        var byDay = events.Where(item => item.Kind == "sign-in")
            .GroupBy(item => item.At.UtcDateTime.ToString("yyyy-MM-dd"))
            .OrderByDescending(group => group.Key)
            .Take(14)
            .Select(group => $"<li>{Encode(group.Key)} · {group.Count()}</li>");
        var updates = events.Where(item => item.Kind == "upgrade").TakeLast(40).Reverse()
            .Select(item => $"<li>{Encode(item.At.ToString("u"))} · {Encode(EmailOf(users, item.UserId))} · {Encode(item.PackageId)} · {(item.Success ? "succeeded" : "failed")}</li>");
        var apps = events.Where(item => item.Kind is "install" or "upgrade" or "remove").TakeLast(40).Reverse()
            .Select(item => $"<li>{Encode(item.At.ToString("u"))} · {Encode(item.Kind)} · {Encode(item.PackageId)} · {Encode(EmailOf(users, item.UserId))}</li>");
        var systems = installs.Select(item => $"<li>{Encode(EmailOf(users, item.UserId))} · {Encode(item.Summary)}</li>");

        return Page($"""
            <h1>DeskVerve admin</h1>
            <form method="post" action="{PortalHandler.AdminPath}/logout"><button type="submit">Sign out</button></form>
            <section><h2>Counts</h2><p>Installs {installs.Count}. Signed in now {active}. Accounts {users.Count(item => item.Role == "user")}.</p><ul>{string.Join("", byDay)}</ul></section>
            <section><h2>Updates</h2><ul>{string.Join("", updates)}</ul></section>
            <section><h2>Apps</h2><ul>{string.Join("", apps)}</ul></section>
            <section><h2>Systems</h2><ul>{string.Join("", systems)}</ul></section>
            """);
    }

    public static string Page(string body) => $$"""
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

    private static string EmailOf(List<User> users, string userId) =>
        users.FirstOrDefault(user => user.Id == userId)?.Email ?? "unknown";

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
