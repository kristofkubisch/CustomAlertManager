using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Web.Models;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Pages.Targets;

public sealed class IndexModel(
    AlertDbContext db,
    UserManager<ApplicationUser> users,
    TargetVerificationTokens tokens,
    IEmailSender emailSender) : PageModel
{
    public List<NotificationTarget> Targets { get; private set; } = [];

    [BindProperty]
    public TargetInput Input { get; set; } = new();

    // Set when the add form was submitted with errors, so the modal opens again showing them.
    public bool ShowAddModal { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Targets = await ListAsync(cancellationToken);

    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        var ownerId = users.GetIntUserId(User);

        if (Input.Kind == ChannelKind.Email && !EmailAddressValidation.IsValid(Input.Destination))
        {
            ModelState.AddModelError("Input.Destination", "Enter a valid email address.");
        }

        if (Input.Kind == ChannelKind.Slack && !SlackAlertChannel.IsValidWebhook(Input.Destination))
        {
            ModelState.AddModelError("Input.Destination", "Enter a Slack webhook URL starting with https://hooks.slack.com/.");
        }

        if (!ModelState.IsValid)
        {
            Targets = await ListAsync(cancellationToken);
            ShowAddModal = true;
            return Page();
        }

        // Slack webhooks are secret URLs, so they are trusted on creation. Email must be confirmed first.
        var target = new NotificationTarget
        {
            OwnerId = ownerId,
            Name = Input.Name.Trim(),
            Kind = Input.Kind,
            Destination = Input.Destination.Trim(),
            IsVerified = Input.Kind == ChannelKind.Slack,
        };
        db.NotificationTargets.Add(target);
        await db.SaveChangesAsync(cancellationToken);

        if (target.IsVerified)
        {
            TempData["Status"] = $"Target '{target.Name}' added.";
            return RedirectToPage();
        }

        var sent = await SendVerificationAsync(target, cancellationToken);
        TempData[sent ? "Status" : "Error"] = sent
            ? $"Target '{target.Name}' added. Check {target.Destination} for a confirmation link."
            : $"Target '{target.Name}' added, but the confirmation email could not be sent. Use Resend to try again.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResendAsync(int id, CancellationToken cancellationToken)
    {
        var target = await FindOwnedAsync(id, cancellationToken);
        if (target is null)
        {
            return NotFound();
        }

        if (target.IsVerified)
        {
            TempData["Status"] = $"'{target.Name}' is already confirmed.";
            return RedirectToPage();
        }

        var sent = await SendVerificationAsync(target, cancellationToken);
        TempData[sent ? "Status" : "Error"] = sent
            ? "Confirmation email sent."
            : "The confirmation email could not be sent. Try again later.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken cancellationToken)
    {
        var target = await FindOwnedAsync(id, cancellationToken);
        if (target is null)
        {
            return NotFound();
        }

        target.IsEnabled = !target.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var target = await FindOwnedAsync(id, cancellationToken);
        if (target is null)
        {
            return NotFound();
        }

        // Remove the link from subscriptions and any undelivered attempts. Delivered history is kept, with the target cleared.
        await db.SubscriptionTargets.Where(link => link.NotificationTargetId == id).ExecuteDeleteAsync(cancellationToken);
        await db.AlertDeliveries
            .Where(delivery => delivery.NotificationTargetId == id && delivery.Status == CustomAlertManager.Scheduler.Models.DeliveryStatus.Pending)
            .ExecuteDeleteAsync(cancellationToken);
        db.NotificationTargets.Remove(target);
        await db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"Target '{target.Name}' deleted.";
        return RedirectToPage();
    }

    private async Task<bool> SendVerificationAsync(NotificationTarget target, CancellationToken cancellationToken)
    {
        var token = tokens.Create(target.Id, target.OwnerId!.Value);
        var link = Url.Page("/Targets/Verify", null, new { token }, Request.Scheme);
        var body = $"Confirm that you want CustomAlertManager to send alerts to this address:\n\n{link}\n\n" +
                   "The link expires in 24 hours. If you did not ask for this, you can ignore this email.";

        var result = await emailSender.SendAsync(
            target.Destination,
            "Confirm your CustomAlertManager email address",
            body,
            cancellationToken);
        return result.Outcome == DeliveryOutcome.Sent;
    }

    private Task<List<NotificationTarget>> ListAsync(CancellationToken cancellationToken)
    {
        var ownerId = users.GetIntUserId(User);
        return db.NotificationTargets
            .Where(target => target.OwnerId == ownerId)
            .OrderBy(target => target.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private Task<NotificationTarget?> FindOwnedAsync(int id, CancellationToken cancellationToken)
    {
        var ownerId = users.GetIntUserId(User);
        return db.NotificationTargets.FirstOrDefaultAsync(
            target => target.Id == id && target.OwnerId == ownerId,
            cancellationToken);
    }
}
