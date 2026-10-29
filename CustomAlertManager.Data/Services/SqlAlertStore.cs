using CustomAlertManager.Data.Entities;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Data.Services;

public sealed class SqlAlertStore(IDbContextFactory<AlertDbContext> contextFactory) : IAlertStore
{
    public async Task<IReadOnlyList<Subscription>> GetEnabledSubscriptionsAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.AlertSubscriptions
            .Include(subscription => subscription.Sources)
                .ThenInclude(link => link.NewsSource)
            .Include(subscription => subscription.Keywords)
            .Where(subscription => subscription.IsEnabled)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities
            .Select(subscription => new Subscription(
                Id: subscription.Id,
                Frequency: TimeSpan.FromMinutes(subscription.FrequencyMinutes),
                LastRunAt: subscription.LastRunAt,
                LastAlertAt: subscription.LastAlertAt,
                Sources: subscription.Sources
                    .Where(link => link.NewsSource.IsActive)
                    .Select(link => new SourceRef(link.NewsSourceId, link.NewsSource.Url))
                    .ToList(),
                IncludeTerms: subscription.Keywords
                    .Where(keyword => keyword.Kind == KeywordKind.Include)
                    .Select(keyword => keyword.Term)
                    .ToList(),
                ExcludeTerms: subscription.Keywords
                    .Where(keyword => keyword.Kind == KeywordKind.Exclude)
                    .Select(keyword => keyword.Term)
                    .ToList()))
            .ToList();
    }

    public async Task SaveFetchedItemsAsync(IReadOnlyList<SourcedItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var candidates = items
            .Select(sourced => (sourced.SourceId, Key: ItemKey.Of(sourced.Item.ItemId), sourced.Item))
            .DistinctBy(candidate => (candidate.SourceId, candidate.Key))
            .ToList();

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var sourceIds = candidates.Select(candidate => candidate.SourceId).ToHashSet();
        var keys = candidates.Select(candidate => candidate.Key).ToHashSet();

        var existing = (await db.FeedItems
            .Where(stored => sourceIds.Contains(stored.NewsSourceId) && keys.Contains(stored.ItemKey))
            .Select(stored => new { stored.NewsSourceId, stored.ItemKey })
            .ToListAsync(cancellationToken))
            .Select(stored => (stored.NewsSourceId, stored.ItemKey))
            .ToHashSet();

        var now = DateTimeOffset.UtcNow;
        foreach (var candidate in candidates.Where(candidate => !existing.Contains((candidate.SourceId, candidate.Key))))
        {
            db.FeedItems.Add(new StoredFeedItem
            {
                NewsSourceId = candidate.SourceId,
                ItemKey = candidate.Key,
                Title = candidate.Item.Title,
                Summary = candidate.Item.Summary,
                Link = candidate.Item.Link,
                PublishedAt = candidate.Item.PublishedAt,
                FirstSeenAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteRunAsync(
        int subscriptionId,
        DateTimeOffset runStartedAt,
        IReadOnlyList<SourcedItem> matches,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var subscription = await db.AlertSubscriptions.SingleAsync(entity => entity.Id == subscriptionId, cancellationToken);
        subscription.LastRunAt = runStartedAt;

        if (matches.Count > 0)
        {
            var sourceIds = matches.Select(match => match.SourceId).ToHashSet();
            var keys = matches.Select(match => ItemKey.Of(match.Item.ItemId)).ToHashSet();
            var storedItems = await db.FeedItems
                .Where(stored => sourceIds.Contains(stored.NewsSourceId) && keys.Contains(stored.ItemKey))
                .ToListAsync(cancellationToken);

            var alert = new Alert
            {
                AlertSubscriptionId = subscriptionId,
                CreatedAt = runStartedAt,
            };

            foreach (var match in matches)
            {
                var key = ItemKey.Of(match.Item.ItemId);
                var storedItem = storedItems.Single(stored => stored.NewsSourceId == match.SourceId && stored.ItemKey == key);
                alert.Items.Add(new AlertItem { FeedItem = storedItem });
            }

            db.Alerts.Add(alert);
            subscription.LastAlertAt = runStartedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
