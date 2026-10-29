using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Common.Models;

namespace CustomAlertManager.AlertSender.Services;

// The one place alert messages are built, so a sample alert is laid out exactly like a real one.
public static class AlertMessageFactory
{
    public static AlertMessage Create(string subscriptionName, IReadOnlyList<FeedItem> items) =>
        new($"Alert: {subscriptionName}", items);

    // Sample content for the admin test page. Nothing is stored; it is only sent through the channel.
    public static AlertMessage CreateSample(DateTimeOffset now, string siteUrl) =>
        Create(
            "Sample subscription",
            [
                new FeedItem(
                    SourceName: "Sample News",
                    Title: "Sample headline: rail services resume after overnight maintenance",
                    Summary: "This is a sample article used to preview how alerts are delivered.",
                    Link: siteUrl,
                    PublishedAt: now,
                    ItemId: $"sample-{now.ToUnixTimeSeconds()}"),
            ]);
}
