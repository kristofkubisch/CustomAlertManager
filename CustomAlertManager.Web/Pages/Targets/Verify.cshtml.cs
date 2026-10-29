using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Web.Pages.Targets;

// Reached from the link in the confirmation email, so no sign-in is needed. The signed token names the
// target and its owner, and only someone who received the email can use it.
public sealed class VerifyModel(AlertDbContext db, TargetVerificationTokens tokens) : PageModel
{
    public bool Confirmed { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public async Task OnGetAsync(string token, CancellationToken cancellationToken)
    {
        if (!tokens.TryRead(token, out var targetId, out var ownerId))
        {
            Message = "This confirmation link is invalid or has expired. Log in and use Resend on the targets page to get a new one.";
            return;
        }

        var target = await db.NotificationTargets.FirstOrDefaultAsync(
            entity => entity.Id == targetId && entity.OwnerId == ownerId,
            cancellationToken);
        if (target is null || target.Kind != ChannelKind.Email)
        {
            Message = "That target no longer exists.";
            return;
        }

        if (!target.IsVerified)
        {
            target.IsVerified = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        Confirmed = true;
        Message = $"{target.Destination} is confirmed and can now receive alerts.";
    }
}
