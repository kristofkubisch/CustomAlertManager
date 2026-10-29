using CustomAlertManager.Data.Entities;
using CustomAlertManager.Web.Models;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomAlertManager.Web.Pages.Subscriptions;

public sealed class EditModel(SubscriptionService subscriptions, UserManager<ApplicationUser> users) : PageModel
{
    public SubscriptionInput Input { get; private set; } = new();

    public List<NewsSource> Sources { get; private set; } = [];

    public List<NotificationTarget> Targets { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var subscription = await subscriptions.FindForOwnerAsync(id, users.GetIntUserId(User), cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        Input = SubscriptionService.ToInput(subscription);
        await LoadOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, SubscriptionInput input, CancellationToken cancellationToken)
    {
        var ownerId = users.GetIntUserId(User);
        if (await subscriptions.FindForOwnerAsync(id, ownerId, cancellationToken) is null)
        {
            return NotFound();
        }

        Input = input;
        if (ModelState.IsValid)
        {
            var errors = await subscriptions.ValidateAsync(input, ownerId, cancellationToken);
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(cancellationToken);
            return Page();
        }

        await subscriptions.UpdateAsync(id, ownerId, input, cancellationToken);
        TempData["Status"] = $"Subscription '{input.Name}' saved.";
        return RedirectToPage("Index");
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        Sources = await subscriptions.ActiveSourcesAsync(cancellationToken);
        Targets = await subscriptions.UsableTargetsAsync(users.GetIntUserId(User), cancellationToken);
        ViewData["Sources"] = Sources;
        ViewData["Targets"] = Targets;
    }
}
