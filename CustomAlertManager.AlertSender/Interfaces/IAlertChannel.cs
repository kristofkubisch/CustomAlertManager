using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.AlertSender.Interfaces;

public interface IAlertChannel
{
    ChannelKind Kind { get; }

    // Delivery problems are reported through the result, not thrown, so the dispatcher can decide whether to retry.
    Task<DeliveryResult> SendAsync(string destination, AlertMessage message, CancellationToken cancellationToken);
}
