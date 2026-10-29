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

        return feed.Items.Select(item =>
        {
            var itemTitle = item.Title?.Text ?? string.Empty;
            var link = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty;
            var itemId = !string.IsNullOrWhiteSpace(item.Id) ? item.Id
                : link.Length > 0 ? link
                : itemTitle;

            return new FeedItem(
                SourceName: sourceName,
                Title: itemTitle,
                Summary: item.Summary?.Text ?? string.Empty,
                Link: link,
                PublishedAt: item.PublishDate == default ? fallbackDate : item.PublishDate,
                ItemId: itemId);
        }).ToList();
    }
}
