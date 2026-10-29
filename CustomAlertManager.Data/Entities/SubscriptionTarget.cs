namespace CustomAlertManager.Data.Entities;

public sealed class SubscriptionTarget
{
    public int AlertSubscriptionId { get; set; }

    public int NotificationTargetId { get; set; }

    public AlertSubscription AlertSubscription { get; set; } = null!;

    public NotificationTarget NotificationTarget { get; set; } = null!;
}
