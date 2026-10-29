using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.Common.Models;
using CustomAlertManager.RssReader.Interfaces;
using CustomAlertManager.Scheduler.Models;
using CustomAlertManager.Scheduler.Services;

namespace CustomAlertManager.Tests.Scheduler;

public class AlertSchedulerTests
{
    [Fact]
    public async Task RunAsync_FetchesEachDistinctFeedOnce()
    {
        var fetcher = new FakeFeedFetcher();
        var scheduler = new AlertScheduler(fetcher, new RecordingChannel());
        var subscription = new Subscription(["https://a.example/rss", "https://A.example/rss"], [], [], TimeSpan.FromDays(1));

        var result = await scheduler.RunAsync(subscription, CancellationToken.None);

        Assert.Equal(1, result.FeedCount);
        Assert.Equal(["https://a.example/rss"], fetcher.RequestedUrls);
    }

    [Fact]
    public async Task RunAsync_FiltersByPublicationCutoffAndKeywords_ThenSendsMatches()
    {
        var now = DateTimeOffset.UtcNow;
        var fetcher = new FakeFeedFetcher(
            ("https://a.example/rss",
            [
                new FeedItem("A", "Election today", "", "https://a.example/1", now.AddMinutes(-10)),
                new FeedItem("A", "Election last week", "", "https://a.example/2", now.AddDays(-7)),
                new FeedItem("A", "Football today", "", "https://a.example/3", now.AddMinutes(-5)),
            ]));
        var channel = new RecordingChannel();
        var scheduler = new AlertScheduler(fetcher, channel);
        var subscription = new Subscription(["https://a.example/rss"], ["election"], [], TimeSpan.FromHours(1));

        var result = await scheduler.RunAsync(subscription, CancellationToken.None);

        var match = Assert.Single(result.Matches);
        Assert.Equal("Election today", match.Title);
        Assert.Equal(result.Matches, channel.Sent);
    }

    [Fact]
    public async Task RunAsync_FailingFeed_IsSkippedAndOthersStillRun()
    {
        var now = DateTimeOffset.UtcNow;
        var fetcher = new FakeFeedFetcher(
            ("https://ok.example/rss", [new FeedItem("OK", "Election", "", "https://ok.example/1", now)]));
        var scheduler = new AlertScheduler(fetcher, new RecordingChannel());
        var subscription = new Subscription(
            ["https://broken.example/rss", "https://ok.example/rss"], [], [], TimeSpan.FromHours(1));

        var result = await scheduler.RunAsync(subscription, CancellationToken.None);

        Assert.Equal(2, result.FeedCount);
        Assert.Single(result.Matches);
    }

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

    private sealed class RecordingChannel : IAlertChannel
    {
        public IReadOnlyList<FeedItem> Sent { get; private set; } = [];

        public Task SendAsync(IReadOnlyList<FeedItem> items, CancellationToken cancellationToken)
        {
            Sent = items;
            return Task.CompletedTask;
        }
    }
}
