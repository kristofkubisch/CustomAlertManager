using CustomAlertManager.Data.Entities;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomAlertManager.Web.Pages.Subscriptions;

public sealed class IndexModel(SubscriptionService subscriptions, UserManager<ApplicationUser> users) : PageModel
{
    public List<CustomAlertManager.Data.Entities.AlertSubscription> Subscriptions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Subscriptions = await subscriptions.ListForOwnerAsync(users.GetIntUserId(User), cancellationToken);

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken cancellationToken)
    {
        var subscription = await subscriptions.FindForOwnerAsync(id, users.GetIntUserId(User), cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        await subscriptions.SetEnabledAsync(subscription, !subscription.IsEnabled, cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var subscription = await subscriptions.FindForOwnerAsync(id, users.GetIntUserId(User), cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        var name = subscription.Name;
        await subscriptions.DeleteAsync(subscription, cancellationToken);
        TempData["Status"] = $"Subscription '{name}' deleted.";
        return RedirectToPage();
    }
}
