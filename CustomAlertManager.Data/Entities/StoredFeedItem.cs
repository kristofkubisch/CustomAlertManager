namespace CustomAlertManager.Data.Entities;

public sealed class StoredFeedItem
{
    public int Id { get; set; }

    public int NewsSourceId { get; set; }

    // SHA-256 of the feed's item id, so long links fit in a unique index.
    public string ItemKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Link { get; set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public NewsSource NewsSource { get; set; } = null!;
}
