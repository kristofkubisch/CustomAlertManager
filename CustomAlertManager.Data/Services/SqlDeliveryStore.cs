using CustomAlertManager.Common.Models;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Data.Services;

public sealed class SqlDeliveryStore(IDbContextFactory<AlertDbContext> contextFactory) : IDeliveryStore
{
    private const int MaxErrorLength = 1000;

    public async Task<IReadOnlyList<PendingDelivery>> GetDueDeliveriesAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.AlertDeliveries
            .Where(delivery => delivery.Status == DeliveryStatus.Pending && delivery.NextAttemptAt <= now)
            .OrderBy(delivery => delivery.NextAttemptAt)
            .Take(limit)
            .Include(delivery => delivery.NotificationTarget)
            .Include(delivery => delivery.Alert)
                .ThenInclude(alert => alert.AlertSubscription)
            .Include(delivery => delivery.Alert)
                .ThenInclude(alert => alert.Items)
                .ThenInclude(item => item.FeedItem)
                .ThenInclude(stored => stored.NewsSource)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .Select(delivery => new PendingDelivery(
                DeliveryId: delivery.Id,
                Kind: delivery.NotificationTarget.Kind,
                Destination: delivery.NotificationTarget.Destination,
                Attempts: delivery.Attempts,
                Message: new AlertMessage(
                    Title: $"Alert: {delivery.Alert.AlertSubscription.Name}",
                    Items: delivery.Alert.Items
                        .OrderByDescending(item => item.FeedItem.PublishedAt)
                        .Select(item => ToFeedItem(item.FeedItem))
                        .ToList())))
            .ToList();
    }

    public async Task RecordAttemptAsync(DeliveryUpdate update, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var delivery = await db.AlertDeliveries.SingleAsync(entity => entity.Id == update.DeliveryId, cancellationToken);

        delivery.Status = update.Status;
        delivery.Attempts = update.Attempts;
        delivery.NextAttemptAt = update.NextAttemptAt;
        delivery.SentAt = update.SentAt;
        delivery.LastError = update.LastError is { Length: > MaxErrorLength } error ? error[..MaxErrorLength] : update.LastError;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static FeedItem ToFeedItem(StoredFeedItem stored) =>
        new(
            SourceName: stored.NewsSource.Name,
            Title: stored.Title,
            Summary: stored.Summary,
            Link: stored.Link,
            PublishedAt: stored.PublishedAt,
            ItemId: stored.ItemKey);
}
