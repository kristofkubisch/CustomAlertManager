namespace CustomAlertManager.Data.Entities;

public sealed class SubscriptionKeyword : TimestampedEntity
{
    public int Id { get; set; }

    public int AlertSubscriptionId { get; set; }

    public KeywordKind Kind { get; set; }

    public string Term { get; set; } = string.Empty;
}

public enum KeywordKind
{
    Include = 0,
    Exclude = 1,
}
