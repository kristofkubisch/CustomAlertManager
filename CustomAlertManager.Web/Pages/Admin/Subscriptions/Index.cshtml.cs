using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Pages.Admin.Subscriptions;

// Admins can see and switch off every user's subscriptions. Running them is limited to the owner, see Admin/AlertTests.
[Authorize(Roles = "Admin")]
public sealed class IndexModel(AlertDbContext db, ILogger<IndexModel> logger) : PageModel
{
    public List<SubscriptionRow> Rows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Rows = await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken cancellationToken)
    {
        var subscription = await db.AlertSubscriptions.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        subscription.IsEnabled = !subscription.IsEnabled;
        logger.LogInformation("Admin {Admin} {Action} subscription {Subscription}", User.Identity?.Name, subscription.IsEnabled ? "enabled" : "disabled", subscription.Name);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Status"] = $"'{subscription.Name}' {(subscription.IsEnabled ? "enabled" : "disabled")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var subscription = await db.AlertSubscriptions.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        var name = subscription.Name;
        db.AlertSubscriptions.Remove(subscription);
        logger.LogInformation("Admin {Admin} deleted subscription {Subscription}", User.Identity?.Name, name);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Status"] = $"Subscription '{name}' deleted.";
        return RedirectToPage();
    }

    private Task<List<SubscriptionRow>> LoadAsync(CancellationToken cancellationToken) =>
        db.AlertSubscriptions
            .OrderBy(subscription => subscription.Name)
            .Select(subscription => new SubscriptionRow(
                subscription.Id,
                subscription.Name,
                db.Users.Where(user => user.Id == subscription.OwnerId).Select(user => user.UserName).FirstOrDefault(),
                subscription.IsEnabled,
                subscription.FrequencyMinutes,
                subscription.Sources.Count(),
                subscription.LastRunAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public sealed record SubscriptionRow(
        int Id,
        string Name,
        string? Owner,
        bool IsEnabled,
        int FrequencyMinutes,
        int SourceCount,
        DateTimeOffset? LastRunAt);
}
