using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Common.Models;
using CustomAlertManager.Scheduler.Models;
using CustomAlertManager.Scheduler.Services;

namespace CustomAlertManager.Tests.Scheduler;

public class DeliveryRetryPolicyTests
{
    private static readonly DateTimeOffset AttemptedAt = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_Sent_MarksDeliverySent()
    {
        var update = DeliveryRetryPolicy.Apply(Pending(attempts: 0), DeliveryResult.Sent(), AttemptedAt);

        Assert.Equal(DeliveryStatus.Sent, update.Status);
        Assert.Equal(1, update.Attempts);
        Assert.Equal(AttemptedAt, update.SentAt);
        Assert.Null(update.NextAttemptAt);
    }

    [Fact]
    public void Apply_PermanentFailure_FailsImmediately()
    {
        var update = DeliveryRetryPolicy.Apply(Pending(attempts: 0), DeliveryResult.Permanent("HTTP 404"), AttemptedAt);

        Assert.Equal(DeliveryStatus.Failed, update.Status);
        Assert.Equal("HTTP 404", update.LastError);
        Assert.Null(update.NextAttemptAt);
    }

    [Fact]
    public void Apply_TransientFailure_SchedulesRetryWithBackoff()
    {
        var update = DeliveryRetryPolicy.Apply(Pending(attempts: 1), DeliveryResult.Transient("HTTP 503"), AttemptedAt);

        Assert.Equal(DeliveryStatus.Pending, update.Status);
        Assert.Equal(2, update.Attempts);
        Assert.Equal(AttemptedAt + TimeSpan.FromMinutes(2), update.NextAttemptAt);
        Assert.Equal("HTTP 503", update.LastError);
    }

    [Fact]
    public void Apply_TransientFailureOnLastAttempt_GivesUp()
    {
        var update = DeliveryRetryPolicy.Apply(
            Pending(attempts: DeliveryRetryPolicy.MaxAttempts - 1),
            DeliveryResult.Transient("HTTP 500"),
            AttemptedAt);

        Assert.Equal(DeliveryStatus.Failed, update.Status);
        Assert.Equal(DeliveryRetryPolicy.MaxAttempts, update.Attempts);
        Assert.Null(update.NextAttemptAt);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    public void Backoff_DoublesEachAttempt(int attempts, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), DeliveryRetryPolicy.Backoff(attempts));
    }

    private static PendingDelivery Pending(int attempts) =>
        new(DeliveryId: 1, Kind: ChannelKind.Slack, Destination: "https://hooks.slack.com/x", Attempts: attempts,
            Message: new AlertMessage("Alert", Array.Empty<FeedItem>()));
}
