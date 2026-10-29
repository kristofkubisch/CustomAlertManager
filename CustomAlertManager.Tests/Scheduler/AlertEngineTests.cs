using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.Common.Models;
using CustomAlertManager.RssReader.Interfaces;
using CustomAlertManager.Scheduler.Models;
using CustomAlertManager.Scheduler.Services;

namespace CustomAlertManager.Tests.Scheduler;

public class AlertEngineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task ProcessAsync_SharedSource_IsFetchedOnceAndFeedsEverySubscription()
    {
        var fetcher = new FakeFeedFetcher(
            ("https://a.example/rss", [Item("Election today", "https://a.example/1", Now.AddMinutes(-5))]));
        var engine = new AlertEngine(fetcher);
        var first = Subscription(1, [new SourceRef(10, "https://a.example/rss")], ["election"], Now.AddHours(-1));
        var second = Subscription(2, [new SourceRef(10, "https://a.example/rss")], [], Now.AddHours(-1));

        var result = await engine.ProcessAsync([first, second], CancellationToken.None);

        Assert.Equal(["https://a.example/rss"], fetcher.RequestedUrls);
        Assert.Single(result.FetchedItems);
        Assert.Single(result.Matches.Single(match => match.SubscriptionId == 1).Items);
        Assert.Single(result.Matches.Single(match => match.SubscriptionId == 2).Items);
    }

    [Fact]
    public async Task ProcessAsync_OnlyItemsPublishedAfterLastAlertAt_Match()
    {
        var fetcher = new FakeFeedFetcher(
            ("https://a.example/rss",
            [
                Item("Election today", "https://a.example/1", Now.AddMinutes(-10)),
                Item("Election last week", "https://a.example/2", Now.AddDays(-7)),
                Item("Football today", "https://a.example/3", Now.AddMinutes(-5)),
            ]));
        var engine = new AlertEngine(fetcher);
        var subscription = Subscription(1, [new SourceRef(10, "https://a.example/rss")], ["election"], Now.AddHours(-1));

        var result = await engine.ProcessAsync([subscription], CancellationToken.None);

        var match = Assert.Single(result.Matches.Single().Items);
        Assert.Equal("Election today", match.Item.Title);
        Assert.Equal(10, match.SourceId);
    }

    [Fact]
    public async Task ProcessAsync_ItemFromSourceNotInSubscription_DoesNotMatch()
    {
        var fetcher = new FakeFeedFetcher(
            ("https://other.example/rss", [Item("Election today", "https://other.example/1", Now)]));
        var engine = new AlertEngine(fetcher);
        var subscription = Subscription(1, [new SourceRef(10, "https://a.example/rss")], [], Now.AddHours(-1));

        var result = await engine.ProcessAsync([subscription], CancellationToken.None);

        Assert.Empty(result.Matches.Single().Items);
    }

    [Fact]
    public async Task ProcessAsync_FailingFeed_IsSkippedAndOthersStillRun()
    {
        var fetcher = new FakeFeedFetcher(
            ("https://ok.example/rss", [Item("Election", "https://ok.example/1", Now)]));
        var engine = new AlertEngine(fetcher);
        var subscription = Subscription(
            1,
            [new SourceRef(10, "https://broken.example/rss"), new SourceRef(11, "https://ok.example/rss")],
            [],
            Now.AddHours(-1));

        var result = await engine.ProcessAsync([subscription], CancellationToken.None);

        Assert.Single(result.FetchedItems);
        Assert.Single(result.Matches.Single().Items);
    }

    [Fact]
    public void DueSubscriptions_NeverRun_IsDue()
    {
        var subscription = Subscription(1, [], [], Now.AddDays(-1), frequency: TimeSpan.FromMinutes(15));

        Assert.Single(DueSubscriptions.Select([subscription], Now));
    }

    [Fact]
    public void DueSubscriptions_RanWithinFrequency_IsNotDue()
    {
        var subscription = Subscription(1, [], [], Now.AddDays(-1), frequency: TimeSpan.FromMinutes(15), lastRunAt: Now.AddMinutes(-10));

        Assert.Empty(DueSubscriptions.Select([subscription], Now));
    }

    [Fact]
    public void DueSubscriptions_FrequencyElapsed_IsDue()
    {
        var subscription = Subscription(1, [], [], Now.AddDays(-1), frequency: TimeSpan.FromMinutes(15), lastRunAt: Now.AddMinutes(-15));

        Assert.Single(DueSubscriptions.Select([subscription], Now));
    }

    // The worker wakes on a timer, so the gap between two runs is a little under the exact interval.
    // The subscription must still count as due, otherwise it slips a whole poll interval.
    [Fact]
    public void DueSubscriptions_RanJustBeforeFrequency_IsDue()
    {
        var subscription = Subscription(1, [], [], Now.AddDays(-1), frequency: TimeSpan.FromMinutes(15), lastRunAt: Now.AddMinutes(-15).AddSeconds(1));

        Assert.Single(DueSubscriptions.Select([subscription], Now));
    }

    [Fact]
    public void DueSubscriptions_RanWellBeforeFrequency_IsNotDue()
    {
        var subscription = Subscription(1, [], [], Now.AddDays(-1), frequency: TimeSpan.FromMinutes(15), lastRunAt: Now.AddMinutes(-14));

        Assert.Empty(DueSubscriptions.Select([subscription], Now));
    }

    private static FeedItem Item(string title, string link, DateTimeOffset publishedAt) =>
        new("Source", title, string.Empty, link, publishedAt, link);

    private static Subscription Subscription(
        int id,
        IReadOnlyList<SourceRef> sources,
        IReadOnlyList<string> includeTerms,
        DateTimeOffset lastAlertAt,
        TimeSpan? frequency = null,
        DateTimeOffset? lastRunAt = null) =>
        new(id, frequency ?? TimeSpan.FromMinutes(1), lastRunAt, lastAlertAt, sources, includeTerms, []);

    private sealed class FakeFeedFetcher(params (string Url, FeedItem[] Items)[] feeds) : IFeedFetcher
    {
        public List<string> RequestedUrls { get; } = [];

        public Task<IReadOnlyList<FeedItem>> FetchAsync(string url, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(url);
            var feed = feeds.FirstOrDefault(feed => feed.Url == url);
            if (feed.Url is null)
            {
                throw new HttpRequestException($"No fake feed for {url}");
            }

            return Task.FromResult<IReadOnlyList<FeedItem>>(feed.Items);
        }
    }
}
