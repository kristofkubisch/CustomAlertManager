namespace CustomAlertManager.Common.Models;

public sealed record FeedItem(
    string SourceName,
    string Title,
    string Summary,
    string Link,
    DateTimeOffset PublishedAt);
