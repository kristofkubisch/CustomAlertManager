namespace CustomAlertManager.Data.Entities;

public sealed class SubscriptionSource
{
    public int AlertSubscriptionId { get; set; }

    public int NewsSourceId { get; set; }

    public AlertSubscription AlertSubscription { get; set; } = null!;

    public NewsSource NewsSource { get; set; } = null!;
}
