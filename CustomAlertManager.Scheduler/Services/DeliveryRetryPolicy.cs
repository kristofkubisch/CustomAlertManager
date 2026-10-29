using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Services;

public static class DeliveryRetryPolicy
{
    public const int MaxAttempts = 5;

    public static DeliveryUpdate Apply(PendingDelivery delivery, DeliveryResult result, DateTimeOffset attemptedAt)
    {
        var attempts = delivery.Attempts + 1;
        var id = delivery.DeliveryId;

        return result.Outcome switch
        {
            DeliveryOutcome.Sent => new DeliveryUpdate(id, DeliveryStatus.Sent, attempts, null, attemptedAt, null),
            DeliveryOutcome.PermanentFailure => new DeliveryUpdate(id, DeliveryStatus.Failed, attempts, null, null, result.Error),
            _ when attempts >= MaxAttempts => new DeliveryUpdate(id, DeliveryStatus.Failed, attempts, null, null, result.Error),
            _ => new DeliveryUpdate(id, DeliveryStatus.Pending, attempts, attemptedAt + Backoff(attempts), null, result.Error),
        };
    }

    // Waits 1, 2, 4 and 8 minutes after the first, second, third and fourth failed attempts.
    public static TimeSpan Backoff(int attempts) => TimeSpan.FromMinutes(Math.Pow(2, attempts - 1));
}
