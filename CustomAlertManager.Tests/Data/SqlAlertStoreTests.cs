using CustomAlertManager.Common.Models;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using CustomAlertManager.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Tests.Data;

[Collection("SqlDatabase")]
public class SqlAlertStoreTests(SqlDatabaseFixture fixture)
{
    private static readonly DateTimeOffset RunAt = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlAlertStore _store = new(fixture.ContextFactory);

    [Fact]
    public async Task SaveFetchedItemsAsync_SameItemTwice_StoresOneRow()
    {
        var source = await AddSourceAsync();
        var item = new SourcedItem(source.Id, Item("item-1", RunAt));

        await _store.SaveFetchedItemsAsync([item], CancellationToken.None);
        await _store.SaveFetchedItemsAsync([item], CancellationToken.None);

        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await db.FeedItems.CountAsync(stored => stored.NewsSourceId == source.Id));
    }

    [Fact]
    public async Task CompleteRunAsync_WithMatches_CreatesAlertAndAdvancesLastAlertAt()
    {
        var source = await AddSourceAsync();
        var subscriptionId = await AddSubscriptionAsync(source, RunAt.AddDays(-1));
        var item = new SourcedItem(source.Id, Item("item-1", RunAt));
        await _store.SaveFetchedItemsAsync([item], CancellationToken.None);

        await _store.CompleteRunAsync(subscriptionId, RunAt, [item], CancellationToken.None);

        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        var subscription = await db.AlertSubscriptions.SingleAsync(entity => entity.Id == subscriptionId);
        Assert.Equal(RunAt, subscription.LastRunAt);
        Assert.Equal(RunAt, subscription.LastAlertAt);

        var alert = await db.Alerts.Include(entity => entity.Items)
            .SingleAsync(entity => entity.AlertSubscriptionId == subscriptionId);
        Assert.Single(alert.Items);
    }

    [Fact]
    public async Task CompleteRunAsync_WithoutMatches_OnlyUpdatesLastRunAt()
    {
        var source = await AddSourceAsync();
        var lastAlertAt = RunAt.AddDays(-1);
        var subscriptionId = await AddSubscriptionAsync(source, lastAlertAt);

        await _store.CompleteRunAsync(subscriptionId, RunAt, [], CancellationToken.None);

        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        var subscription = await db.AlertSubscriptions.SingleAsync(entity => entity.Id == subscriptionId);
        Assert.Equal(RunAt, subscription.LastRunAt);
        Assert.Equal(lastAlertAt, subscription.LastAlertAt);
        Assert.False(await db.Alerts.AnyAsync(entity => entity.AlertSubscriptionId == subscriptionId));
    }

    [Fact]
    public async Task GetEnabledSubscriptionsAsync_ReturnsOnlyActiveSourcesAndSplitKeywords()
    {
        var activeSource = await AddSourceAsync(isActive: true);
        var inactiveSource = await AddSourceAsync(isActive: false);
        var subscriptionId = await AddSubscriptionAsync(activeSource, RunAt.AddDays(-1));
        await AddSourceLinkAsync(subscriptionId, inactiveSource.Id);
        await AddKeywordAsync(subscriptionId, KeywordKind.Include, "election");
        await AddKeywordAsync(subscriptionId, KeywordKind.Exclude, "sport");

        var subscriptions = await _store.GetEnabledSubscriptionsAsync(CancellationToken.None);

        var subscription = subscriptions.Single(entity => entity.Id == subscriptionId);
        Assert.Equal([activeSource.Url], subscription.Sources.Select(source => source.Url));
        Assert.Equal(["election"], subscription.IncludeTerms);
        Assert.Equal(["sport"], subscription.ExcludeTerms);
        Assert.Equal(TimeSpan.FromMinutes(1), subscription.Frequency);
    }

    private static FeedItem Item(string itemId, DateTimeOffset publishedAt) =>
        new("Test", "Election results", "Counting finished.", $"https://{itemId}.example/", publishedAt, itemId);

    private async Task<NewsSource> AddSourceAsync(bool isActive = true)
    {
        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        var source = new NewsSource
        {
            Name = "Test source",
            Url = $"https://{Guid.NewGuid():N}.example/rss",
            IsActive = isActive,
        };
        db.NewsSources.Add(source);
        await db.SaveChangesAsync();
        return source;
    }

    private async Task<int> AddSubscriptionAsync(NewsSource source, DateTimeOffset lastAlertAt)
    {
        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        var subscription = new AlertSubscription
        {
            Name = "Test subscription",
            FrequencyMinutes = 1,
            CreatedAt = lastAlertAt,
            LastAlertAt = lastAlertAt,
        };
        subscription.Sources.Add(new SubscriptionSource { NewsSourceId = source.Id });
        db.AlertSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    private async Task AddSourceLinkAsync(int subscriptionId, int newsSourceId)
    {
        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        db.SubscriptionSources.Add(new SubscriptionSource
        {
            AlertSubscriptionId = subscriptionId,
            NewsSourceId = newsSourceId,
        });
        await db.SaveChangesAsync();
    }

    private async Task AddKeywordAsync(int subscriptionId, KeywordKind kind, string term)
    {
        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        db.SubscriptionKeywords.Add(new SubscriptionKeyword
        {
            AlertSubscriptionId = subscriptionId,
            Kind = kind,
            Term = term,
        });
        await db.SaveChangesAsync();
    }
}
