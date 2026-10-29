namespace CustomAlertManager.Data.Entities;

public sealed class Alert
{
    public int Id { get; set; }

    public int AlertSubscriptionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public AlertSubscription AlertSubscription { get; set; } = null!;

    public List<AlertItem> Items { get; } = [];
}
