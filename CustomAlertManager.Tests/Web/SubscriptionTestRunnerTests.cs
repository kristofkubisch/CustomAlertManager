using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Common.Models;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.RssReader.Interfaces;
using CustomAlertManager.Scheduler.Services;
using CustomAlertManager.Tests.Data;
using CustomAlertManager.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Tests.Web;

[Collection("SqlDatabase")]
public class SubscriptionTestRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunAsync_SendsOnlyMatchingItemsToConfirmedTargets_AndStoresNothing()
    {
        var url = $"https://{Guid.NewGuid():N}.example/rss";
        var name = $"Filter test {Guid.NewGuid():N}";
        var lastAlertAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ownerId = await CreateOwnerAsync();
        var subscriptionId = await CreateSubscriptionAsync(name, url, lastAlertAt, ownerId);
        var enabledDestination = await AddTargetAsync(subscriptionId, isEnabled: true, isVerified: true);
        var disabledDestination = await AddTargetAsync(subscriptionId, isEnabled: false, isVerified: true);
        var unconfirmedDestination = await AddTargetAsync(subscriptionId, isEnabled: true, isVerified: false);

        var fetcher = new FakeFeedFetcher(url,
        [
            new FeedItem("Source", "Election results announced", "", "https://x/1", Now.AddHours(-1), "1"),
            new FeedItem("Source", "Election sport special", "", "https://x/2", Now.AddHours(-1), "2"),
            new FeedItem("Source", "Markets close higher", "", "https://x/3", Now.AddHours(-1), "3"),
        ]);
        var channel = new RecordingChannel();
        var runner = new SubscriptionTestRunner(NewContext(), new AlertEngine(fetcher), [channel]);

        var result = await runner.RunAsync(subscriptionId, ownerId, Now, CancellationToken.None);

        // The "sport" item is excluded by the subscription's filter, and the "markets" item has no keyword.
        Assert.NotNull(result);
        Assert.Equal(1, result.MatchCount);
        var delivery = Assert.Single(result.Deliveries);
        Assert.True(delivery.Sent);
        Assert.Equal([enabledDestination], channel.Destinations);
        Assert.DoesNotContain(unconfirmedDestination, channel.Destinations);
        Assert.DoesNotContain(disabledDestination, channel.Destinations);
        Assert.Equal($"Alert: {name}", channel.Messages.Single().Title);
        Assert.Equal("Election results announced", Assert.Single(channel.Messages.Single().Items).Title);

        await using var db = NewContext();
        var subscription = await db.AlertSubscriptions.AsNoTracking().SingleAsync(entity => entity.Id == subscriptionId);
        Assert.Null(subscription.LastRunAt);
        Assert.Equal(lastAlertAt, subscription.LastAlertAt);
        Assert.False(await db.Alerts.AnyAsync(alert => alert.AlertSubscriptionId == subscriptionId));
        Assert.False(await db.AlertDeliveries.AnyAsync(delivery => delivery.Alert.AlertSubscriptionId == subscriptionId));
    }

    [Fact]
    public async Task RunAsync_NoMatches_SendsNothing()
    {
        var url = $"https://{Guid.NewGuid():N}.example/rss";
        var ownerId = await CreateOwnerAsync();
        var subscriptionId = await CreateSubscriptionAsync($"Quiet {Guid.NewGuid():N}", url, Now.AddDays(-1), ownerId);
        await AddTargetAsync(subscriptionId, isEnabled: true, isVerified: true);
        var fetcher = new FakeFeedFetcher(url, [new FeedItem("Source", "Weather only", "", "https://x/w", Now.AddHours(-1), "w")]);
        var channel = new RecordingChannel();
        var runner = new SubscriptionTestRunner(NewContext(), new AlertEngine(fetcher), [channel]);

        var result = await runner.RunAsync(subscriptionId, ownerId, Now, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, result.MatchCount);
        Assert.Empty(channel.Messages);
    }

    [Fact]
    public async Task RunAsync_UnknownSubscription_ReturnsNull()
    {
        var runner = new SubscriptionTestRunner(NewContext(), new AlertEngine(new FakeFeedFetcher("", [])), []);

        Assert.Null(await runner.RunAsync(int.MaxValue, 1, Now, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_OtherAdminsSubscription_IsNotFound()
    {
        var url = $"https://{Guid.NewGuid():N}.example/rss";
        var ownerId = await CreateOwnerAsync();
        var otherAdminId = await CreateOwnerAsync();
        var subscriptionId = await CreateSubscriptionAsync($"Owned {Guid.NewGuid():N}", url, Now.AddDays(-1), ownerId);
        await AddTargetAsync(subscriptionId, isEnabled: true, isVerified: true);
        var channel = new RecordingChannel();
        var runner = new SubscriptionTestRunner(NewContext(), new AlertEngine(new FakeFeedFetcher(url,
            [new FeedItem("Source", "Election today", "", "https://x/e", Now.AddHours(-1), "e")])), [channel]);

        var result = await runner.RunAsync(subscriptionId, otherAdminId, Now, CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(channel.Messages);
    }

    private async Task<int> CreateOwnerAsync()
    {
        await using var db = NewContext();
        var owner = new ApplicationUser { UserName = $"owner{Guid.NewGuid():N}"[..20], Email = $"{Guid.NewGuid():N}@example.com" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        return owner.Id;
    }

    private static AlertDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AlertDbContext>().UseSqlServer(SqlDatabaseFixture.ConnectionString).Options);

    private async Task<int> CreateSubscriptionAsync(string name, string url, DateTimeOffset lastAlertAt, int ownerId)
    {
        await using var db = NewContext();
        var source = new NewsSource { Name = "Test source", Url = url };
        var subscription = new AlertSubscription
        {
            Name = name,
            OwnerId = ownerId,
            FrequencyMinutes = 1,
            CreatedAt = lastAlertAt,
            LastAlertAt = lastAlertAt,
        };
        subscription.Sources.Add(new SubscriptionSource { NewsSource = source });
        subscription.Keywords.Add(new SubscriptionKeyword { Kind = KeywordKind.Include, Term = "election" });
        subscription.Keywords.Add(new SubscriptionKeyword { Kind = KeywordKind.Exclude, Term = "sport" });
        db.AlertSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    private async Task<string> AddTargetAsync(int subscriptionId, bool isEnabled, bool isVerified)
    {
        await using var db = NewContext();
        var destination = $"{Guid.NewGuid():N}@example.com";
        var target = new NotificationTarget
        {
            Name = "Target",
            Kind = ChannelKind.Email,
            Destination = destination,
            IsEnabled = isEnabled,
            IsVerified = isVerified,
        };
        db.SubscriptionTargets.Add(new SubscriptionTarget { AlertSubscriptionId = subscriptionId, NotificationTarget = target });
        await db.SaveChangesAsync();
        return destination;
    }

    private sealed class FakeFeedFetcher(string url, IReadOnlyList<FeedItem> items) : IFeedFetcher
    {
        public Task<IReadOnlyList<FeedItem>> FetchAsync(string requestedUrl, CancellationToken cancellationToken) =>
            Task.FromResult(requestedUrl == url ? items : (IReadOnlyList<FeedItem>)[]);
    }

    private sealed class RecordingChannel : IAlertChannel
    {
        public ChannelKind Kind => ChannelKind.Email;

        public List<string> Destinations { get; } = [];

        public List<AlertMessage> Messages { get; } = [];

        public Task<DeliveryResult> SendAsync(string destination, AlertMessage message, CancellationToken cancellationToken)
        {
            Destinations.Add(destination);
            Messages.Add(message);
            return Task.FromResult(DeliveryResult.Sent());
        }
    }
}
