using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Services;

// Runs a subscription's sources and keyword filters once, right now, and sends the matches to its confirmed targets.
// Read-only: no alerts, deliveries, or check times are written, so the schedule is not affected.
public sealed class SubscriptionTestRunner(
    AlertDbContext db,
    IAlertEngine engine,
    IEnumerable<IAlertChannel> channels)
{
    // Items published in this window count as matches. The normal schedule uses the time since the last alert instead.
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    // Only the owner of a subscription can test it, even when the caller is an admin.
    public async Task<TestRunResult?> RunAsync(int subscriptionId, int ownerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entity = await db.AlertSubscriptions
            .Include(subscription => subscription.Sources)
                .ThenInclude(link => link.NewsSource)
            .Include(subscription => subscription.Keywords)
            .Include(subscription => subscription.Targets)
                .ThenInclude(link => link.NotificationTarget)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(subscription => subscription.Id == subscriptionId && subscription.OwnerId == ownerId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var subscription = new Subscription(
            Id: entity.Id,
            Frequency: TimeSpan.FromMinutes(entity.FrequencyMinutes),
            LastRunAt: null,
            LastAlertAt: now - Window,
            Sources: entity.Sources
                .Where(link => link.NewsSource.IsActive)
                .Select(link => new SourceRef(link.NewsSourceId, link.NewsSource.Url))
                .ToList(),
            IncludeTerms: entity.Keywords.Where(keyword => keyword.Kind == KeywordKind.Include).Select(keyword => keyword.Term).ToList(),
            ExcludeTerms: entity.Keywords.Where(keyword => keyword.Kind == KeywordKind.Exclude).Select(keyword => keyword.Term).ToList());

        var engineResult = await engine.ProcessAsync([subscription], cancellationToken);
        var items = engineResult.Matches.Single().Items.Select(sourced => sourced.Item).ToList();
        if (items.Count == 0)
        {
            return new TestRunResult(entity.Name, 0, []);
        }

        // Same message as a real alert, so the recipient sees exactly what the schedule would send.
        var message = AlertMessageFactory.Create(entity.Name, items);
        var deliveries = new List<TargetResult>();
        foreach (var target in entity.Targets.Select(link => link.NotificationTarget).Where(target => target.IsEnabled && target.IsVerified))
        {
            var channel = channels.FirstOrDefault(candidate => candidate.Kind == target.Kind);
            var result = channel is null
                ? DeliveryResult.Permanent($"No {target.Kind} channel is configured.")
                : await channel.SendAsync(target.Destination, message, cancellationToken);
            deliveries.Add(new TargetResult(target.Name, result.Outcome == DeliveryOutcome.Sent, result.Error));
        }

        return new TestRunResult(entity.Name, items.Count, deliveries);
    }
}

public sealed record TestRunResult(string SubscriptionName, int MatchCount, IReadOnlyList<TargetResult> Deliveries);

public sealed record TargetResult(string TargetName, bool Sent, string? Error);
