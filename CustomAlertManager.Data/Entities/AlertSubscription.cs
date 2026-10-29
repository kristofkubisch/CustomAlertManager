namespace CustomAlertManager.Data.Entities;

public sealed class AlertSubscription : TimestampedEntity
{
    public int Id { get; set; }

    // Null for subscriptions created by the development seed.
    public int? OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int FrequencyMinutes { get; set; }


    public DateTimeOffset? LastRunAt { get; set; }

    public DateTimeOffset LastAlertAt { get; set; }

    public List<SubscriptionSource> Sources { get; } = [];

    public List<SubscriptionKeyword> Keywords { get; } = [];

    public List<SubscriptionTarget> Targets { get; } = [];

    public List<Alert> Alerts { get; } = [];
}
