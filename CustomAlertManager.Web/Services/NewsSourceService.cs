using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Services;

public sealed class NewsSourceService(AlertDbContext db, ILogger<NewsSourceService> logger)
{
    public async Task<List<(string Field, string Message)>> ValidateAsync(
        string url,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        var errors = new List<(string Field, string Message)>();
        var urlError = FeedUrlValidator.Validate(url);
        if (urlError is not null)
        {
            errors.Add(("Input.Url", urlError));
        }
        else if (await db.NewsSources.AnyAsync(
            source => source.Url == url.Trim() && (excludingId == null || source.Id != excludingId),
            cancellationToken))
        {
            errors.Add(("Input.Url", "This feed is already a source."));
        }

        return errors;
    }

    public async Task CreateAsync(string name, string url, CancellationToken cancellationToken)
    {
        db.NewsSources.Add(new NewsSource { Name = name.Trim(), Url = url.Trim() });
        logger.LogInformation("Admin added news source {Name} ({Url})", name.Trim(), url.Trim());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(NewsSource source, string name, string url, CancellationToken cancellationToken)
    {
        source.Name = name.Trim();
        source.Url = url.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(NewsSource source, bool isActive, CancellationToken cancellationToken)
    {
        source.IsActive = isActive;
        logger.LogInformation("Admin {Action} news source {Name}", isActive ? "activated" : "deactivated", source.Name);
        await db.SaveChangesAsync(cancellationToken);
    }

    // A source used by a subscription cannot be deleted, so the subscriptions keep their history. Deactivate it instead.
    public async Task<bool> DeleteAsync(NewsSource source, CancellationToken cancellationToken)
    {
        if (await db.SubscriptionSources.AnyAsync(link => link.NewsSourceId == source.Id, cancellationToken))
        {
            return false;
        }

        db.NewsSources.Remove(source);
        logger.LogInformation("Admin deleted news source {Name}", source.Name);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
