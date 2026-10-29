using System.Xml;
using CustomAlertManager.Common.Models;
using CustomAlertManager.RssReader.Interfaces;

namespace CustomAlertManager.RssReader.Services;

public sealed class HttpFeedFetcher(HttpClient httpClient) : IFeedFetcher
{
    public async Task<IReadOnlyList<FeedItem>> FetchAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = XmlReader.Create(stream);
        return FeedParser.Parse(reader, url, DateTimeOffset.UtcNow);
    }
}
