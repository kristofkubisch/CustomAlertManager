namespace CustomAlertManager.Common.Models;

// ItemId is the feed's own identifier for the entry (guid or id), falling back to the link.
public sealed record FeedItem(
    string SourceName,
    string Title,
    string Summary,
    string Link,
    DateTimeOffset PublishedAt,
    string ItemId);
