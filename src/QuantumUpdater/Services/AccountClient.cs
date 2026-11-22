using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace QuantumUpdater.Services;

public sealed record AccountResult(bool Ok, string Message, string? Token = null);

/// <summary>
/// Talks only to the account service. It never opens admin pages.
/// </summary>
public sealed class AccountClient
{
    public const string BaseUrl = "http://127.0.0.1:8787";

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

    public async Task<AccountResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return await PostAsync("/api/register", new { email, password }, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return await PostAsync("/api/login", new { email, password }, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReportHeartbeatAsync(SessionStore session, MachineSummary summary, CancellationToken cancellationToken = default)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        await PostAsync(
            "/api/heartbeat",
            // F21: no email. The service knows the user from the bearer token.
            new { installId = session.InstallId, summary = summary.Telemetry() },
            session.Token,
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

        await PostAsync(
            "/api/activity",
            new { installId = session.InstallId, action, packageId, success },
            session.Token,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<AccountResult> PostAsync(
        string path,
        object body,
        string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path)
            {
                Content = JsonContent.Create(body),
            };
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadFromJsonAsync<ApiReply>(cancellationToken).ConfigureAwait(false);
            var message = payload?.Message ?? (response.IsSuccessStatusCode ? "OK" : "The account service refused the request.");
            return new AccountResult(response.IsSuccessStatusCode && payload?.Ok != false, message, payload?.Token);
        }
        catch (Exception ex)
        {
            return new AccountResult(false, "Account service is not reachable. " + ex.Message);
        }
    }

    private sealed record ApiReply(bool Ok, string? Message, string? Token);
}
