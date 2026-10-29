using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Data.Entities;

public sealed class AlertDelivery : TimestampedEntity
{
    public int Id { get; set; }

    public int AlertId { get; set; }

    // Cleared when the target is deleted, so the delivery history survives.
    public int? NotificationTargetId { get; set; }

    public DeliveryStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public string? LastError { get; set; }

    public Alert Alert { get; set; } = null!;

    public NotificationTarget? NotificationTarget { get; set; }
}
