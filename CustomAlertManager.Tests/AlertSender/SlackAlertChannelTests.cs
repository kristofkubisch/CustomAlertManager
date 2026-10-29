using System.Net;
using System.Text.Json;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Common.Models;

namespace CustomAlertManager.Tests.AlertSender;

public class SlackAlertChannelTests
{
    private const string Webhook = "https://hooks.slack.com/services/T000/B000/XXXX";

    private static readonly AlertMessage Message = new(
        "Alert: Election watch",
        [new FeedItem("BBC News", "Votes & <counts>", "", "https://example.com/a?x=1&y=2", DateTimeOffset.UtcNow, "a")]);

    [Fact]
    public async Task SendAsync_Success_PostsEscapedTextAndReturnsSent()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var channel = new SlackAlertChannel(new HttpClient(handler));

        var result = await channel.SendAsync(Webhook, Message, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Sent, result.Outcome);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Webhook, request.RequestUri!.ToString());
        // The JSON encoder escapes &, < and > itself, so compare the decoded text Slack will receive.
        var text = JsonDocument.Parse(handler.Bodies.Single()).RootElement.GetProperty("text").GetString();
        Assert.Contains("Votes &amp; &lt;counts&gt;", text);
        // Slack expects the URL escaped too, and decodes &amp; back to & when it renders the link.
        Assert.Contains("<https://example.com/a?x=1&amp;y=2|Votes &amp; &lt;counts&gt;>", text);
    }

    [Theory]
    [InlineData("http://hooks.slack.com/services/x")]
    [InlineData("https://example.com/services/x")]
    [InlineData("not a url")]
    public async Task SendAsync_InvalidWebhook_FailsPermanentlyWithoutCallingSlack(string destination)
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var channel = new SlackAlertChannel(new HttpClient(handler));

        var result = await channel.SendAsync(destination, Message, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.PermanentFailure, result.Outcome);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, DeliveryOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.TooManyRequests, DeliveryOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.NotFound, DeliveryOutcome.PermanentFailure)]
    public async Task SendAsync_ErrorStatus_IsClassified(HttpStatusCode statusCode, DeliveryOutcome expected)
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(statusCode));
        var channel = new SlackAlertChannel(new HttpClient(handler));

        var result = await channel.SendAsync(Webhook, Message, CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task SendAsync_NetworkError_IsTransient()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection reset"));
        var channel = new SlackAlertChannel(new HttpClient(handler));

        var result = await channel.SendAsync(Webhook, Message, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.TransientFailure, result.Outcome);
    }
}
