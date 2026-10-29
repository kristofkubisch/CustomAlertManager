using CustomAlertManager.Common.Models;
using CustomAlertManager.RssReader.Interfaces;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Models;
using System.Xml;

namespace CustomAlertManager.Scheduler.Services;

public sealed class AlertEngine(IFeedFetcher feedFetcher) : IAlertEngine
{
    public async Task<EngineResult> ProcessAsync(
        IReadOnlyList<Subscription> subscriptions,
        CancellationToken cancellationToken)
    {
        // Each source is downloaded once, even when several subscriptions use it.
        var sources = subscriptions
            .SelectMany(subscription => subscription.Sources)
            .DistinctBy(source => source.Id)
            .ToList();

        var downloads = sources.Select(async source =>
            (SourceId: source.Id, Items: await TryFetchAsync(source.Url, cancellationToken)));
        var fetched = await Task.WhenAll(downloads);

        var fetchedItems = fetched
            .SelectMany(result => result.Items.Select(item => new SourcedItem(result.SourceId, item)))
            .ToList();

        var matches = subscriptions
            .Select(subscription => new SubscriptionMatches(subscription.Id, FindMatches(subscription, fetchedItems)))
            .ToList();

        return new EngineResult(fetchedItems, matches);
    }

    private static IReadOnlyList<SourcedItem> FindMatches(Subscription subscription, IReadOnlyList<SourcedItem> fetchedItems)
    {
        var sourceIds = subscription.Sources.Select(source => source.Id).ToHashSet();
        var filter = new KeywordFilter(subscription.IncludeTerms, subscription.ExcludeTerms);

        return fetchedItems
            .Where(sourced => sourceIds.Contains(sourced.SourceId))
            .Where(sourced => sourced.Item.PublishedAt > subscription.LastAlertAt)
            .Where(sourced => filter.IsMatch(sourced.Item))
            .OrderByDescending(sourced => sourced.Item.PublishedAt)
            .ToList();
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
