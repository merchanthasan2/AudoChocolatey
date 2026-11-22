using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace QuantumUpdater.Services;

public sealed record AccountResult(bool Ok, string Message, string? Token = null);

/// <summary>What the start-up session check found.</summary>
public enum SessionCheck
{
    /// <summary>No session is stored. Nothing was sent.</summary>
    SignedOut,

    /// <summary>The service accepted the stored session.</summary>
    Valid,

    /// <summary>The service refused the session (401). The stored session was cleared.</summary>
    Rejected,

    /// <summary>The service could not be reached or gave another answer. The session is kept.</summary>
    Unreachable,
}

/// <summary>
/// Talks only to the account service. It never opens admin pages.
/// </summary>
public sealed class AccountClient
{
    public const string BaseUrl = "http://127.0.0.1:8787";

    private const string NotReachable = "The account service is not reachable. Try again later.";

    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly HttpClient _http;

    public AccountClient()
    {
        _http = SharedHttp;
    }

    /// <summary>Sends through the given handler. Tests pass a fake handler.</summary>
    internal AccountClient(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
    }

    /// <summary>
    /// F7: raised after any request with the stored token gets 401. The session is already cleared.
    /// May be raised on a background thread.
    /// </summary>
    public event Action? SessionRejected;

    public async Task<AccountResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return await SendAsync(HttpMethod.Post, "/api/register", new { email, password }, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return await SendAsync(HttpMethod.Post, "/api/login", new { email, password }, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>F7: asks the service whether the stored session is still good (GET /api/me).</summary>
    public async Task<SessionCheck> CheckSessionAsync(SessionStore session, CancellationToken cancellationToken = default)
    {
        if (!session.IsSignedIn)
        {
            return SessionCheck.SignedOut;
        }

        var result = await SendWithSessionAsync(HttpMethod.Get, "/api/me", null, session, cancellationToken).ConfigureAwait(false);
        if (result.Ok)
        {
            return SessionCheck.Valid;
        }

        return session.IsSignedIn ? SessionCheck.Unreachable : SessionCheck.Rejected;
    }

    public async Task ReportHeartbeatAsync(SessionStore session, MachineSummary summary, CancellationToken cancellationToken = default)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        await SendWithSessionAsync(
            HttpMethod.Post,
            "/api/heartbeat",
            // F21: no email. The service knows the user from the bearer token.
            new { installId = session.InstallId, summary = summary.Telemetry() },
            session,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ReportActivityAsync(
        SessionStore session,
        string action,
        string packageId,
        bool success,
        CancellationToken cancellationToken = default)
    {
        if (action is not ("install" or "upgrade" or "remove"))
        {
            return;
        }

        // F20: the local activity log is kept whether or not the user is signed in.
        ActivityLog.Add(action, packageId, success);
        if (!session.IsSignedIn)
        {
            return;
        }

        await SendWithSessionAsync(
            HttpMethod.Post,
            "/api/activity",
            new { installId = session.InstallId, action, packageId, success },
            session,
            cancellationToken).ConfigureAwait(false);
    }

    // Sends with the session token. A 401 clears that session (F7), but only when the token is still the one sent.
    private async Task<AccountResult> SendWithSessionAsync(
        HttpMethod method,
        string path,
        object? body,
        SessionStore session,
        CancellationToken cancellationToken)
    {
        var token = session.Token;
        var (result, status) = await SendCoreAsync(method, path, body, token, cancellationToken).ConfigureAwait(false);
        if (status == HttpStatusCode.Unauthorized && token is not null && session.Token == token)
        {
            session.Clear();
            SessionRejected?.Invoke();
        }

        return result;
    }

    private async Task<AccountResult> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        string? token,
        CancellationToken cancellationToken)
    {
        var (result, _) = await SendCoreAsync(method, path, body, token, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<(AccountResult Result, HttpStatusCode? Status)> SendCoreAsync(
        HttpMethod method,
        string path,
        object? body,
        string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, BaseUrl + path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            ApiReply? payload = null;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<ApiReply>(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
                // Not JSON (for example a proxy page). Use the status only.
            }

            var message = payload?.Message ?? (response.IsSuccessStatusCode ? "OK" : "The account service refused the request.");
            return (new AccountResult(response.IsSuccessStatusCode && payload?.Ok != false, message, payload?.Token), response.StatusCode);
        }
        catch (Exception)
        {
            // Plain text only. The exception text can hold the address and system details.
            return (new AccountResult(false, NotReachable), null);
        }
    }

    private sealed record ApiReply(bool Ok, string? Message, string? Token);
}
