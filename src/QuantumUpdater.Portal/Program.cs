using System.Net;
using QuantumUpdater.Portal;

// DeskVerve account service. Listens on this PC only. The routes live in PortalHandler; this file copies
// requests in and replies out.
var store = new AccountStore(AccountStore.DefaultFolder);
var handler = new PortalHandler(store);
var listener = new HttpListener();
listener.Prefixes.Add($"http://{PortalHandler.Authority}/");
listener.Start();
Console.WriteLine($"DeskVerve account service at http://{PortalHandler.Authority}{PortalHandler.AdminPath}");

// F5: the first owner needs this code. It is shown here only, never written to a file or a reply.
var setupCode = store.SetupCode;
if (setupCode is not null)
{
    Console.WriteLine("No owner account yet. Owner setup code (this run only): " + AccountStore.FormatCode(setupCode));
}

while (listener.IsListening)
{
    var context = await listener.GetContextAsync().ConfigureAwait(false);
    _ = Task.Run(() => Serve(context, handler));
}

static async Task Serve(HttpListenerContext context, PortalHandler handler)
{
    try
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? name in context.Request.Headers.AllKeys)
        {
            if (name is not null && context.Request.Headers[name] is { } value)
            {
                headers[name] = value;
            }
        }

        var request = new PortalRequest
        {
            Method = context.Request.HttpMethod,
            Path = context.Request.Url?.AbsolutePath ?? "/",
            RemoteAddress = context.Request.RemoteEndPoint?.Address.ToString() ?? "unknown",
            Headers = headers,
            ContentLength = context.Request.HasEntityBody ? context.Request.ContentLength64 : 0,
            Body = context.Request.InputStream,
        };

        var reply = await handler.HandleAsync(request).ConfigureAwait(false);
        context.Response.StatusCode = reply.Status;
        context.Response.ContentType = reply.ContentType;
        foreach (var (name, value) in reply.Headers)
        {
            context.Response.AppendHeader(name, value);
        }

        context.Response.ContentLength64 = reply.Body.Length;
        await context.Response.OutputStream.WriteAsync(reply.Body).ConfigureAwait(false);
        context.Response.Close();
    }
    catch (Exception ex)
    {
        // F14: the type only. Messages can hold request data.
        Console.WriteLine("Request failed: " + ex.GetType().Name);
        context.Response.Abort();
    }
}
