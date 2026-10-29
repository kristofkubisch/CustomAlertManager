using System.Xml;
using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.Common.Models;
using CustomAlertManager.RssReader.Interfaces;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Services;

public sealed class AlertScheduler(IFeedFetcher feedFetcher, IAlertChannel alertChannel) : IAlertScheduler
{
    public async Task<AlertRunResult> RunAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        // Each distinct feed URL is downloaded once, even if it was given twice.
        var feedUrls = subscription.FeedUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var downloads = feedUrls.Select(url => TryFetchAsync(url, cancellationToken));
        var fetchedItems = await Task.WhenAll(downloads);

        var cutoff = DateTimeOffset.UtcNow - subscription.Since;
        var filter = new KeywordFilter(subscription.IncludeTerms, subscription.ExcludeTerms);
        var matches = fetchedItems
            .SelectMany(items => items)
            .Where(item => item.PublishedAt >= cutoff)
            .Where(filter.IsMatch)
            .OrderByDescending(item => item.PublishedAt)
            .ToList();

        await alertChannel.SendAsync(matches, cancellationToken);
        return new AlertRunResult(feedUrls.Count, cutoff, matches);
    }

    // A failing feed is reported and skipped so the other feeds still run.
    private async Task<IReadOnlyList<FeedItem>> TryFetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await feedFetcher.FetchAsync(url, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Failed to load {url}: {ex.Message}");
            return [];
        }
    }
}
