namespace CustomAlertManager.Data.Entities;

public sealed class AlertItem : TimestampedEntity
{
    public int Id { get; set; }

    public int AlertId { get; set; }

    public int FeedItemId { get; set; }

    public Alert Alert { get; set; } = null!;

    public StoredFeedItem FeedItem { get; set; } = null!;
}
