using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.AlertSender.Interfaces;

// Sends a plain-text email. Like IAlertChannel, failures are returned rather than thrown.
public interface IEmailSender
{
    Task<DeliveryResult> SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}
