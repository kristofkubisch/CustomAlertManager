namespace CustomAlertManager.Data.Entities;

public sealed class Alert : TimestampedEntity
{
    public int Id { get; set; }

    public int AlertSubscriptionId { get; set; }


    public AlertSubscription AlertSubscription { get; set; } = null!;

    public List<AlertItem> Items { get; } = [];

    public List<AlertDelivery> Deliveries { get; } = [];
}
