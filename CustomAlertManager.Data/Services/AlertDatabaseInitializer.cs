using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Data.Services;

// Development-only seed values. Null or blank targets are skipped.
public sealed record DevSeedOptions(string? SlackWebhookUrl, string? EmailAddress);

public static class AlertDatabaseInitializer
{
    // Applies pending migrations. When seed options are given and no subscriptions exist, adds one
    // BBC News subscription, plus any targets that were configured.
    public static async Task InitializeAsync(
        IDbContextFactory<AlertDbContext> contextFactory,
        DevSeedOptions? seed,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        if (seed is null || await db.AlertSubscriptions.AnyAsync(cancellationToken))
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

        AddTarget(subscription, "Dev Slack", ChannelKind.Slack, seed.SlackWebhookUrl);
        AddTarget(subscription, "Dev email", ChannelKind.Email, seed.EmailAddress);

        db.AlertSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddTarget(AlertSubscription subscription, string name, ChannelKind kind, string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        var target = new NotificationTarget { Name = name, Kind = kind, Destination = destination };
        subscription.Targets.Add(new SubscriptionTarget { NotificationTarget = target });
    }
}
