using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Data.Entities;

public sealed class AlertDelivery
{
    public int Id { get; set; }

    public int AlertId { get; set; }

    public int NotificationTargetId { get; set; }

    public DeliveryStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public string? LastError { get; set; }

    public Alert Alert { get; set; } = null!;

    public NotificationTarget NotificationTarget { get; set; } = null!;
}
