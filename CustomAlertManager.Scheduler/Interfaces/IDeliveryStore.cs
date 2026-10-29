using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Interfaces;

public interface IDeliveryStore
{
    // Pending deliveries whose next attempt time has arrived, oldest first.
    Task<IReadOnlyList<PendingDelivery>> GetDueDeliveriesAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    Task RecordAttemptAsync(DeliveryUpdate update, CancellationToken cancellationToken);
}
