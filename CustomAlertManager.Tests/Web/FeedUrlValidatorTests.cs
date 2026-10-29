using CustomAlertManager.Web.Services;

namespace CustomAlertManager.Tests.Web;

public class FeedUrlValidatorTests
{
    [Theory]
    [InlineData("https://feeds.bbci.co.uk/news/rss.xml")]
    [InlineData("http://example.com/feed")]
    [InlineData("https://8.8.8.8/rss")]
    [InlineData("https://172.32.0.1/rss")]
    public void Validate_PublicAddress_IsAccepted(string url)
    {
        Assert.Null(FeedUrlValidator.Validate(url));
    }

    [Theory]
    [InlineData("ftp://example.com/feed")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url")]
    [InlineData("")]
    public void Validate_NonHttpOrMalformed_IsRejected(string url)
    {
        Assert.NotNull(FeedUrlValidator.Validate(url));
    }

    [Theory]
    [InlineData("http://localhost/feed")]
    [InlineData("http://intranet.localhost/feed")]
    [InlineData("http://printer.local/feed")]
    [InlineData("http://127.0.0.1/feed")]
    [InlineData("http://[::1]/feed")]
    [InlineData("http://10.1.2.3/feed")]
    [InlineData("http://192.168.1.1/feed")]
    [InlineData("http://172.20.0.1/feed")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://100.64.0.1/feed")]
    [InlineData("http://[fd00::1]/feed")]
    public void Validate_LocalOrPrivateAddress_IsRejected(string url)
    {
        Assert.NotNull(FeedUrlValidator.Validate(url));
    }
}
