using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.Scheduler.Models;

public sealed record PendingDelivery(
    int DeliveryId,
    ChannelKind Kind,
    string Destination,
    int Attempts,
    AlertMessage Message);
