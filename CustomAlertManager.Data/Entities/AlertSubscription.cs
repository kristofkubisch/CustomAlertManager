namespace CustomAlertManager.Data.Entities;

public sealed class AlertSubscription
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int FrequencyMinutes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    public DateTimeOffset LastAlertAt { get; set; }

    public List<SubscriptionSource> Sources { get; } = [];

    public List<SubscriptionKeyword> Keywords { get; } = [];

    public List<Alert> Alerts { get; } = [];
}
