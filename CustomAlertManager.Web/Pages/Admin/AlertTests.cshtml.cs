using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Pages.Admin;

// Runs one of the signed-in admin's own subscriptions on demand and sends the matches to its targets, without storing anything.
[Authorize(Roles = "Admin")]
public sealed class AlertTestsModel(
    AlertDbContext db,
    UserManager<ApplicationUser> users,
    SubscriptionTestRunner runner) : PageModel
{
    public List<SubscriptionRow> Subscriptions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Subscriptions = await LoadAsync(users.GetIntUserId(User), cancellationToken);

    public async Task<IActionResult> OnPostRunAsync(int id, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(id, users.GetIntUserId(User), DateTimeOffset.UtcNow, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var window = SubscriptionTestRunner.Window.TotalHours;
        if (result.MatchCount == 0)
        {
            TempData["Error"] = $"No items matched '{result.SubscriptionName}' in the last {window} hours, so no test alert was sent.";
        }
        else if (result.Deliveries.Count == 0)
        {
            TempData["Error"] = $"{result.MatchCount} item(s) matched '{result.SubscriptionName}', but it has no confirmed targets to send to.";
        }
        else
        {
            var summary = string.Join("; ", result.Deliveries.Select(delivery =>
                delivery.Sent ? $"{delivery.TargetName}: sent" : $"{delivery.TargetName}: failed ({delivery.Error})"));
            TempData[result.Deliveries.All(delivery => delivery.Sent) ? "Status" : "Error"] =
                $"Test alert for '{result.SubscriptionName}' ({result.MatchCount} matching item(s)). {summary}";
        }

        return RedirectToPage();
    }

    private Task<List<SubscriptionRow>> LoadAsync(int ownerId, CancellationToken cancellationToken) =>
        db.AlertSubscriptions
            .Where(subscription => subscription.OwnerId == ownerId)
            .OrderBy(subscription => subscription.Name)
            .Select(subscription => new SubscriptionRow(
                subscription.Id,
                subscription.Name,
                subscription.FrequencyMinutes,
                subscription.Sources.Count(),
                subscription.Targets.Count(link => link.NotificationTarget.IsEnabled && link.NotificationTarget.IsVerified),
                subscription.LastRunAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public sealed record SubscriptionRow(
        int Id,
        string Name,
        int FrequencyMinutes,
        int SourceCount,
        int TargetCount,
        DateTimeOffset? LastRunAt);
}
