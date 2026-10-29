using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Common.Models;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Tests.Data;

[Collection("SqlDatabase")]
public class SqlDeliveryStoreTests(SqlDatabaseFixture fixture)
{
    private static readonly DateTimeOffset RunAt = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlAlertStore _alerts = new(fixture.ContextFactory);
    private readonly SqlDeliveryStore _deliveries = new(fixture.ContextFactory);

    [Fact]
    public async Task CompleteRunAsync_WithMatches_CreatesDeliveryOnlyForEnabledTargets()
    {
        var destination = $"reader-{Guid.NewGuid():N}@example.com";
        var (subscriptionId, sourceId) = await CreateSubscriptionAsync();
        await AddTargetAsync(subscriptionId, destination, ChannelKind.Email, isEnabled: true);
        await AddTargetAsync(subscriptionId, $"disabled-{Guid.NewGuid():N}@example.com", ChannelKind.Email, isEnabled: false);
        var item = new SourcedItem(sourceId, Item(Guid.NewGuid().ToString("N")));
        await _alerts.SaveFetchedItemsAsync([item], CancellationToken.None);

        await _alerts.CompleteRunAsync(subscriptionId, RunAt, [item], CancellationToken.None);

        var due = await _deliveries.GetDueDeliveriesAsync(RunAt, 1000, CancellationToken.None);
        var delivery = Assert.Single(due, pending => pending.Destination == destination);
        Assert.Equal(ChannelKind.Email, delivery.Kind);
        Assert.Equal(0, delivery.Attempts);
        Assert.Single(delivery.Message.Items);
        Assert.Equal("BBC News", delivery.Message.Items.Single().SourceName);
        Assert.DoesNotContain(due, pending => pending.Destination.StartsWith("disabled-"));
    }

    [Fact]
    public async Task RecordAttemptAsync_Sent_RemovesDeliveryFromDueList()
    {
        var destination = $"sent-{Guid.NewGuid():N}@example.com";
        var delivery = await CreatePendingDeliveryAsync(destination);

        await _deliveries.RecordAttemptAsync(
            new DeliveryUpdate(delivery.DeliveryId, DeliveryStatus.Sent, 1, null, RunAt, null),
            CancellationToken.None);

        var due = await _deliveries.GetDueDeliveriesAsync(RunAt.AddDays(1), 1000, CancellationToken.None);
        Assert.DoesNotContain(due, pending => pending.DeliveryId == delivery.DeliveryId);
    }

    [Fact]
    public async Task RecordAttemptAsync_RetryLater_IsNotDueUntilNextAttemptTime()
    {
        var destination = $"retry-{Guid.NewGuid():N}@example.com";
        var delivery = await CreatePendingDeliveryAsync(destination);
        var nextAttemptAt = RunAt.AddMinutes(4);

        await _deliveries.RecordAttemptAsync(
            new DeliveryUpdate(delivery.DeliveryId, DeliveryStatus.Pending, 1, nextAttemptAt, null, "HTTP 503"),
            CancellationToken.None);

        var beforeRetry = await _deliveries.GetDueDeliveriesAsync(RunAt.AddMinutes(3), 1000, CancellationToken.None);
        Assert.DoesNotContain(beforeRetry, pending => pending.DeliveryId == delivery.DeliveryId);

        var afterRetry = await _deliveries.GetDueDeliveriesAsync(nextAttemptAt, 1000, CancellationToken.None);
        var retried = Assert.Single(afterRetry, pending => pending.DeliveryId == delivery.DeliveryId);
        Assert.Equal(1, retried.Attempts);
    }

    private async Task<PendingDelivery> CreatePendingDeliveryAsync(string destination)
    {
        var (subscriptionId, sourceId) = await CreateSubscriptionAsync();
        await AddTargetAsync(subscriptionId, destination, ChannelKind.Slack, isEnabled: true);
        var item = new SourcedItem(sourceId, Item(Guid.NewGuid().ToString("N")));
        await _alerts.SaveFetchedItemsAsync([item], CancellationToken.None);
        await _alerts.CompleteRunAsync(subscriptionId, RunAt, [item], CancellationToken.None);

        var due = await _deliveries.GetDueDeliveriesAsync(RunAt, 1000, CancellationToken.None);
        return Assert.Single(due, pending => pending.Destination == destination);
    }

    private static FeedItem Item(string itemId) =>
        new("BBC News", "Election results", "Counting finished.", $"https://{itemId}.example/", RunAt, itemId);

    private async Task<(int SubscriptionId, int SourceId)> CreateSubscriptionAsync()
    {
        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        var source = new NewsSource { Name = "BBC News", Url = $"https://{Guid.NewGuid():N}.example/rss" };
        var subscription = new AlertSubscription
        {
            Name = "Election watch",
            FrequencyMinutes = 1,
            CreatedAt = RunAt.AddDays(-1),
            LastAlertAt = RunAt.AddDays(-1),
        };
        subscription.Sources.Add(new SubscriptionSource { NewsSource = source });
        db.AlertSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return (subscription.Id, source.Id);
    }

    private async Task AddTargetAsync(int subscriptionId, string destination, ChannelKind kind, bool isEnabled)
    {
        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        var target = new NotificationTarget
        {
            Name = "Test target",
            Kind = kind,
            Destination = destination,
            IsEnabled = isEnabled,
        };
        db.SubscriptionTargets.Add(new SubscriptionTarget
        {
            AlertSubscriptionId = subscriptionId,
            NotificationTarget = target,
        });
        await db.SaveChangesAsync();
    }
}
