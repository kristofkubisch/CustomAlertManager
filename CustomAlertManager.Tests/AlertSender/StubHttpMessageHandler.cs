using System.Net;

namespace CustomAlertManager.Tests.AlertSender;

// Records each request and its body, then answers with the supplied response.
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    public static HttpResponseMessage Status(HttpStatusCode statusCode) => new(statusCode);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return respond(request);
    }
}
