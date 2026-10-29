using System.Net;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Common.Models;
using SendGrid;

namespace CustomAlertManager.Tests.AlertSender;

public class SendGridEmailSenderTests
{
    private static readonly SendGridOptions Options = new("SG.test-key", "alerts@example.com", "CustomAlertManager");

    [Fact]
    public async Task SendAsync_Accepted_SendsMailThroughSendGridWithApiKey()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.Accepted));
        var sender = CreateSender(handler, Options);

        var result = await sender.SendAsync("reader@example.com", "Confirm", "Click here", CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Sent, result.Outcome);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/v3/mail/send", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("SG.test-key", request.Headers.Authorization.Parameter);

        var body = handler.Bodies.Single();
        Assert.Contains("\"email\":\"reader@example.com\"", body);
        Assert.Contains("\"email\":\"alerts@example.com\"", body);
        Assert.Contains("\"subject\":\"Confirm\"", body);
        Assert.Contains("Click here", body);
    }

    [Fact]
    public async Task SendAsync_MissingApiKey_FailsPermanentlyWithoutCallingSendGrid()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.Accepted));
        var sender = CreateSender(handler, Options with { ApiKey = string.Empty });

        var result = await sender.SendAsync("reader@example.com", "Subject", "Body", CancellationToken.None);

        Assert.Equal(DeliveryOutcome.PermanentFailure, result.Outcome);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_MissingSenderAddress_FailsPermanentlyWithoutCallingSendGrid()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.Accepted));
        var sender = CreateSender(handler, Options with { FromAddress = string.Empty });

        var result = await sender.SendAsync("reader@example.com", "Subject", "Body", CancellationToken.None);

        Assert.Equal(DeliveryOutcome.PermanentFailure, result.Outcome);
        Assert.Contains("sender address", result.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_InvalidAddress_FailsPermanentlyWithoutCallingSendGrid()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.Accepted));
        var sender = CreateSender(handler, Options);

        var result = await sender.SendAsync("not-an-address", "Subject", "Body", CancellationToken.None);

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
        var sender = CreateSender(handler, Options);

        var result = await sender.SendAsync("reader@example.com", "Subject", "Body", CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task SendAsync_NetworkError_IsTransient()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection reset"));
        var sender = CreateSender(handler, Options);

        var result = await sender.SendAsync("reader@example.com", "Subject", "Body", CancellationToken.None);

        Assert.Equal(DeliveryOutcome.TransientFailure, result.Outcome);
    }

    [Fact]
    public async Task SendGridAlertChannel_DelegatesToSenderWithAlertTitleAndText()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Status(HttpStatusCode.Accepted));
        var channel = new SendGridAlertChannel(CreateSender(handler, Options));
        var message = new AlertMessage(
            "Alert: Election watch",
            [new FeedItem("BBC News", "Votes counted", "", "https://example.com/a", DateTimeOffset.UtcNow, "a")]);

        var result = await channel.SendAsync("reader@example.com", message, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Sent, result.Outcome);
        var body = handler.Bodies.Single();
        Assert.Contains("\"subject\":\"Alert: Election watch\"", body);
        Assert.Contains("Votes counted", body);
    }

    private static SendGridEmailSender CreateSender(StubHttpMessageHandler handler, SendGridOptions options) =>
        new(options, string.IsNullOrWhiteSpace(options.ApiKey)
            ? null
            : new SendGridClient(new HttpClient(handler), options.ApiKey));
}
