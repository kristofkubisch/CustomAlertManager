using CustomAlertManager.Common.Models;

namespace CustomAlertManager.RssReader.Interfaces;

public interface IFeedFetcher
{
    Task<IReadOnlyList<FeedItem>> FetchAsync(string url, CancellationToken cancellationToken);
}
