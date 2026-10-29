using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Scheduler.Models;
using CustomAlertManager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Services;

// Owner-scoped subscription rules. Every method takes the owner id, so users can only reach their own rows.
public sealed class SubscriptionService(AlertDbContext db)
{
    public async Task<List<AlertSubscription>> ListForOwnerAsync(int ownerId, CancellationToken cancellationToken) =>
        await db.AlertSubscriptions
            .Where(subscription => subscription.OwnerId == ownerId)
            .Include(subscription => subscription.Sources)
            .OrderBy(subscription => subscription.Name)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<AlertSubscription?> FindForOwnerAsync(int id, int ownerId, CancellationToken cancellationToken) =>
        db.AlertSubscriptions
            .Include(subscription => subscription.Sources)
            .Include(subscription => subscription.Keywords)
            .Include(subscription => subscription.Targets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(subscription => subscription.Id == id && subscription.OwnerId == ownerId, cancellationToken);

    public async Task<List<NewsSource>> ActiveSourcesAsync(CancellationToken cancellationToken) =>
        await db.NewsSources
            .Where(source => source.IsActive)
            .OrderBy(source => source.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    // Only targets that can actually receive alerts are offered when building a subscription.
    public async Task<List<NotificationTarget>> UsableTargetsAsync(int ownerId, CancellationToken cancellationToken) =>
        await db.NotificationTargets
            .Where(target => target.OwnerId == ownerId && target.IsEnabled && target.IsVerified)
            .OrderBy(target => target.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ValidateAsync(
        SubscriptionInput input,
        int ownerId,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (!AlertFrequency.PresetMinutes.Contains(input.FrequencyMinutes))
        {
            errors.Add("Choose one of the listed frequencies.");
        }

        if (input.SourceIds.Count == 0)
        {
            errors.Add("Choose at least one news source.");
        }
        else
        {
            var activeIds = await db.NewsSources
                .Where(source => source.IsActive)
                .Select(source => source.Id)
                .ToListAsync(cancellationToken);
            if (input.SourceIds.Any(id => !activeIds.Contains(id)))
            {
                errors.Add("One of the chosen sources is not available.");
            }
        }

        var targetIds = await db.NotificationTargets
            .Where(target => target.OwnerId == ownerId && target.IsEnabled && target.IsVerified)
            .Select(target => target.Id)
            .ToListAsync(cancellationToken);
        if (input.TargetIds.Any(id => !targetIds.Contains(id)))
        {
            errors.Add("One of the chosen targets is not available. Check that it is enabled and confirmed.");
        }

        var include = KeywordList.Parse(input.IncludeKeywords);
        var exclude = KeywordList.Parse(input.ExcludeKeywords);
        var includeError = KeywordList.Validate(include);
        var excludeError = KeywordList.Validate(exclude);
        if (includeError is not null)
        {
            errors.Add($"Include keywords: {includeError}");
        }

        if (excludeError is not null)
        {
            errors.Add($"Exclude keywords: {excludeError}");
        }

        return errors;
    }

    public async Task<AlertSubscription> CreateAsync(int ownerId, SubscriptionInput input, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = new AlertSubscription
        {
            OwnerId = ownerId,
            CreatedAt = now,
            LastAlertAt = now,
        };
        ApplyInput(subscription, input);

        db.AlertSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    // Changing a subscription keeps LastAlertAt, so items already alerted on are not sent again.
    // Link rows are replaced in bulk, because re-adding rows with the same composite keys in one save conflicts.
    public async Task UpdateAsync(int subscriptionId, int ownerId, SubscriptionInput input, CancellationToken cancellationToken)
    {
        // Earlier reads in this request may have tracked the old link rows, which would clash with the new ones.
        db.ChangeTracker.Clear();
        await db.SubscriptionSources.Where(link => link.AlertSubscriptionId == subscriptionId).ExecuteDeleteAsync(cancellationToken);
        await db.SubscriptionKeywords.Where(keyword => keyword.AlertSubscriptionId == subscriptionId).ExecuteDeleteAsync(cancellationToken);
        await db.SubscriptionTargets.Where(link => link.AlertSubscriptionId == subscriptionId).ExecuteDeleteAsync(cancellationToken);

        var subscription = await db.AlertSubscriptions
            .FirstAsync(entity => entity.Id == subscriptionId && entity.OwnerId == ownerId, cancellationToken);
        ApplyInput(subscription, input);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(AlertSubscription subscription, bool isEnabled, CancellationToken cancellationToken)
    {
        subscription.IsEnabled = isEnabled;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(AlertSubscription subscription, CancellationToken cancellationToken)
    {
        db.AlertSubscriptions.Remove(subscription);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static SubscriptionInput ToInput(AlertSubscription subscription) => new()
    {
        Name = subscription.Name,
        FrequencyMinutes = subscription.FrequencyMinutes,
        SourceIds = subscription.Sources.Select(link => link.NewsSourceId).ToList(),
        IncludeKeywords = string.Join(", ", subscription.Keywords
            .Where(keyword => keyword.Kind == KeywordKind.Include)
            .Select(keyword => keyword.Term)),
        ExcludeKeywords = string.Join(", ", subscription.Keywords
            .Where(keyword => keyword.Kind == KeywordKind.Exclude)
            .Select(keyword => keyword.Term)),
        TargetIds = subscription.Targets.Select(link => link.NotificationTargetId).ToList(),
    };

    private static void ApplyInput(AlertSubscription subscription, SubscriptionInput input)
    {
        subscription.Name = input.Name.Trim();
        subscription.FrequencyMinutes = input.FrequencyMinutes;

        foreach (var sourceId in input.SourceIds.Distinct())
        {
            subscription.Sources.Add(new SubscriptionSource { NewsSourceId = sourceId });
        }

        foreach (var term in KeywordList.Parse(input.IncludeKeywords))
        {
            subscription.Keywords.Add(new SubscriptionKeyword { Kind = KeywordKind.Include, Term = term });
        }

        foreach (var term in KeywordList.Parse(input.ExcludeKeywords))
        {
            subscription.Keywords.Add(new SubscriptionKeyword { Kind = KeywordKind.Exclude, Term = term });
        }

        foreach (var targetId in input.TargetIds.Distinct())
        {
            subscription.Targets.Add(new SubscriptionTarget { NotificationTargetId = targetId });
        }
    }
}
