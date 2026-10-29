using CustomAlertManager.RssReader.Services;

namespace CustomAlertManager.Tests.RssReader;

public class PublicOnlyConnectorTests
{
    [Theory]
    [InlineData("http://127.0.0.1:9/feed")]
    [InlineData("http://localhost:9/feed")]
    public async Task FetchAsync_PrivateTarget_IsRefusedBeforeConnecting(string url)
    {
        using var client = new HttpClient(PublicOnlyConnector.CreateHandler()) { Timeout = TimeSpan.FromSeconds(10) };
        var fetcher = new HttpFeedFetcher(client);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => fetcher.FetchAsync(url, CancellationToken.None));

        Assert.Contains("private or local address", ex.Message);
    }
}
