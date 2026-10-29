using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Common.Models;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Models;
using CustomAlertManager.Scheduler.Services;

namespace CustomAlertManager.Tests.Scheduler;

public class AlertDispatcherTests
{
    [Fact]
    public async Task DispatchDueAsync_SuccessfulSend_RecordsSent()
    {
        var store = new FakeDeliveryStore(Pending(1, ChannelKind.Slack));
        var channel = new FakeChannel(ChannelKind.Slack, (_, _) => DeliveryResult.Sent());
        var dispatcher = new AlertDispatcher(store, [channel]);

        var attempts = await dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(1, attempts);
        var update = Assert.Single(store.Updates);
        Assert.Equal(DeliveryStatus.Sent, update.Status);
        Assert.Equal("https://hooks.slack.com/test", channel.Destinations.Single());
    }

    [Fact]
    public async Task DispatchDueAsync_TransientFailure_SchedulesRetry()
    {
        var store = new FakeDeliveryStore(Pending(1, ChannelKind.Email));
        var channel = new FakeChannel(ChannelKind.Email, (_, _) => DeliveryResult.Transient("HTTP 429"));
        var dispatcher = new AlertDispatcher(store, [channel]);

        await dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        var update = Assert.Single(store.Updates);
        Assert.Equal(DeliveryStatus.Pending, update.Status);
        Assert.Equal(1, update.Attempts);
        Assert.NotNull(update.NextAttemptAt);
    }

    [Fact]
    public async Task DispatchDueAsync_NoChannelForKind_FailsPermanently()
    {
        var store = new FakeDeliveryStore(Pending(1, ChannelKind.Email));
        var dispatcher = new AlertDispatcher(store, [new FakeChannel(ChannelKind.Slack, (_, _) => DeliveryResult.Sent())]);

        await dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        var update = Assert.Single(store.Updates);
        Assert.Equal(DeliveryStatus.Failed, update.Status);
        Assert.Contains("Email", update.LastError);
    }

    [Fact]
    public async Task DispatchDueAsync_ChannelThrows_IsTreatedAsTransient()
    {
        var store = new FakeDeliveryStore(Pending(1, ChannelKind.Slack));
        var dispatcher = new AlertDispatcher(store, [new FakeChannel(ChannelKind.Slack, (_, _) => throw new InvalidOperationException("boom"))]);

        await dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        var update = Assert.Single(store.Updates);
        Assert.Equal(DeliveryStatus.Pending, update.Status);
        Assert.Equal("boom", update.LastError);
    }

    [Fact]
    public async Task DispatchDueAsync_OneFailureDoesNotStopOthers()
    {
        var store = new FakeDeliveryStore(Pending(1, ChannelKind.Slack), Pending(2, ChannelKind.Slack));
        var calls = 0;
        var channel = new FakeChannel(ChannelKind.Slack, (_, _) => ++calls == 1
            ? throw new InvalidOperationException("first fails")
            : DeliveryResult.Sent());
        var dispatcher = new AlertDispatcher(store, [channel]);

        await dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(2, store.Updates.Count);
        Assert.Equal(DeliveryStatus.Sent, store.Updates[1].Status);
    }

    private static PendingDelivery Pending(int id, ChannelKind kind) =>
        new(id, kind, "https://hooks.slack.com/test", 0, new AlertMessage("Alert", Array.Empty<FeedItem>()));

    private sealed class FakeDeliveryStore(params PendingDelivery[] due) : IDeliveryStore
    {
        public List<DeliveryUpdate> Updates { get; } = [];

        public Task<IReadOnlyList<PendingDelivery>> GetDueDeliveriesAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PendingDelivery>>(due);

        public Task RecordAttemptAsync(DeliveryUpdate update, CancellationToken cancellationToken)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeChannel(ChannelKind kind, Func<string, AlertMessage, DeliveryResult> send) : IAlertChannel
    {
        public ChannelKind Kind => kind;

        public List<string> Destinations { get; } = [];

        public Task<DeliveryResult> SendAsync(string destination, AlertMessage message, CancellationToken cancellationToken)
        {
            Destinations.Add(destination);
            return Task.FromResult(send(destination, message));
        }
    }
}
