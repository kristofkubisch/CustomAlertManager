using CustomAlertManager.Data.Entities;
using CustomAlertManager.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Data.Services;

public static class AlertDatabaseInitializer
{
    // Applies pending migrations. When seeding is enabled and no subscriptions exist, adds one
    // BBC News subscription so the worker has something to run against.
    public static async Task InitializeAsync(
        IDbContextFactory<AlertDbContext> contextFactory,
        bool seedDevelopmentData,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        if (!seedDevelopmentData || await db.AlertSubscriptions.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var source = new NewsSource
        {
            Name = "BBC News",
            Url = "https://feeds.bbci.co.uk/news/rss.xml",
        };

        var subscription = new AlertSubscription
        {
            Name = "Election watch",
            FrequencyMinutes = AlertFrequency.PresetMinutes[0],
            CreatedAt = now,
            LastAlertAt = now,
        };
        subscription.Sources.Add(new SubscriptionSource { NewsSource = source });
        subscription.Keywords.Add(new SubscriptionKeyword { Kind = KeywordKind.Include, Term = "election" });

        db.AlertSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
    }
}
