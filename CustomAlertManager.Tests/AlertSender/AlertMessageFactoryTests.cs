using CustomAlertManager.AlertSender.Services;

namespace CustomAlertManager.Tests.AlertSender;

public class AlertMessageFactoryTests
{
    [Fact]
    public void Create_UsesAlertTitleFromSubscriptionName()
    {
        var message = AlertMessageFactory.Create("Election watch", []);

        Assert.Equal("Alert: Election watch", message.Title);
    }

    [Fact]
    public void CreateSample_IsLaidOutLikeARealAlert()
    {
        var now = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

        var sample = AlertMessageFactory.CreateSample(now, "https://example.com/");

        Assert.StartsWith("Alert: ", sample.Title);
        var item = Assert.Single(sample.Items);
        Assert.False(string.IsNullOrWhiteSpace(item.SourceName));
        Assert.False(string.IsNullOrWhiteSpace(item.Title));
        Assert.Equal("https://example.com/", item.Link);
        Assert.Equal(now, item.PublishedAt);
    }
}
