using System.Net;
using System.Text;

namespace QuantumUpdater.Tests;

/// <summary>Answers account requests in memory and keeps what was sent. Nothing leaves the test.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    public sealed record Sent(HttpMethod Method, string Path, string? Bearer, string Body);

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _reply;

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage>? reply = null)
    {
        _reply = reply ?? (_ => Json(HttpStatusCode.OK, """{"ok":true}"""));
    }

    public List<Sent> Requests { get; } = new();

    public static HttpResponseMessage Json(HttpStatusCode status, string json)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests)
        {
            Requests.Add(new Sent(request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Parameter, body));
        }

        return _reply(request);
    }
}
