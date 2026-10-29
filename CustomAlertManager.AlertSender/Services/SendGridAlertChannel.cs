using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.AlertSender.Services;

public sealed class SendGridAlertChannel(IEmailSender emailSender) : IAlertChannel
{
    public ChannelKind Kind => ChannelKind.Email;

    public Task<DeliveryResult> SendAsync(string destination, AlertMessage message, CancellationToken cancellationToken) =>
        emailSender.SendAsync(destination, message.Title, AlertTextFormatter.ToPlainText(message), cancellationToken);
}
