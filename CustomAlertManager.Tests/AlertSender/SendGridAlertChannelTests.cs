using System.Net;
using System.Net.Http.Headers;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Common.Models;

namespace CustomAlertManager.Tests.AlertSender;

public class SendGridAlertChannelTests
{
    private static readonly SendGridOptions Options = new("SG.test-key", "alerts@example.com", "CustomAlertManager");

    private static readonly AlertMessage Message = new(
        "Alert: Election watch",
        [new FeedItem("BBC News", "Votes counted", "", "https://example.com/a", new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero), "a")]);

    [Fact]
    public async Task SendAsync_Accepted_SendsBearerAuthorizedMailAndReturnsSent()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.Accepted));
        var channel = CreateChannel(handler, Options);

        var result = await channel.SendAsync("reader@example.com", Message, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Sent, result.Outcome);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.sendgrid.com/v3/mail/send", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "SG.test-key"), request.Headers.Authorization);

        var body = handler.Bodies.Single();
        Assert.Contains("\"email\":\"reader@example.com\"", body);
        Assert.Contains("\"email\":\"alerts@example.com\"", body);
        Assert.Contains("\"subject\":\"Alert: Election watch\"", body);
        Assert.Contains("Votes counted", body);
    }

    [Fact]
    public async Task SendAsync_MissingApiKey_FailsPermanentlyWithoutCallingSendGrid()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.Accepted));
        var channel = CreateChannel(handler, Options with { ApiKey = string.Empty });

        var result = await channel.SendAsync("reader@example.com", Message, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.PermanentFailure, result.Outcome);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_InvalidAddress_FailsPermanentlyWithoutCallingSendGrid()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.Accepted));
        var channel = CreateChannel(handler, Options);

        var result = await channel.SendAsync("not-an-address", Message, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.PermanentFailure, result.Outcome);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, DeliveryOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.ServiceUnavailable, DeliveryOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.Unauthorized, DeliveryOutcome.PermanentFailure)]
    [InlineData(HttpStatusCode.BadRequest, DeliveryOutcome.PermanentFailure)]
    public async Task SendAsync_ErrorStatus_IsClassified(HttpStatusCode statusCode, DeliveryOutcome expected)
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(statusCode));
        var channel = CreateChannel(handler, Options);

        var result = await channel.SendAsync("reader@example.com", Message, CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
    }

    private static SendGridAlertChannel CreateChannel(StubHttpMessageHandler handler, SendGridOptions options) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.sendgrid.com/") }, options);
}
