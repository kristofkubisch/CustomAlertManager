using System.ServiceModel.Syndication;
using System.Xml;
using CustomAlertManager.Common.Models;

namespace CustomAlertManager.RssReader.Services;

public static class FeedParser
{
    // Items without a publication date take the fallback date, which is the time we first saw them.
    public static IReadOnlyList<FeedItem> Parse(XmlReader reader, string sourceUrl, DateTimeOffset fallbackDate)
    {
        var feed = SyndicationFeed.Load(reader);
        var sourceName = feed.Title?.Text is { Length: > 0 } title ? title : sourceUrl;

        return feed.Items
            .Select(item => new FeedItem(
                SourceName: sourceName,
                Title: item.Title?.Text ?? string.Empty,
                Summary: item.Summary?.Text ?? string.Empty,
                Link: item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty,
                PublishedAt: item.PublishDate == default ? fallbackDate : item.PublishDate))
            .ToList();
    }
}
