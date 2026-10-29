using System.Net;
using System.Text.RegularExpressions;
using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.Web.Services;

// Lets ASP.NET Core Identity (password reset and similar mail) send through the same SendGrid sender.
public sealed class IdentityEmailSender(CustomAlertManager.AlertSender.Interfaces.IEmailSender emailSender)
    : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(htmlMessage, "<[^>]+>", string.Empty));
        var result = await emailSender.SendAsync(email, subject, text, CancellationToken.None);
        if (result.Outcome != DeliveryOutcome.Sent)
        {
            throw new InvalidOperationException($"Could not send email: {result.Error}");
        }
    }
}
