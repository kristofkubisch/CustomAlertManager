using System.Xml;
using CustomAlertManager.Common.Models;
using CustomAlertManager.RssReader.Services;

namespace CustomAlertManager.Tests.RssReader;

public class FeedParserTests
{
    private static readonly DateTimeOffset FallbackDate = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_RssFeed_ReadsTitleSummaryLinkAndDate()
    {
        var items = ParseSample("RssReader/Samples/sample-rss.xml");

        var election = items.Single(item => item.Title == "Election results announced");
        Assert.Equal("Sample News", election.SourceName);
        Assert.Equal("Counting has finished in the national vote.", election.Summary);
        Assert.Equal("https://news.example.com/election", election.Link);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 9, 30, 0, TimeSpan.Zero), election.PublishedAt);
    }

    [Fact]
    public void Parse_ItemWithoutPubDate_UsesFallbackDate()
    {
        var items = ParseSample("RssReader/Samples/sample-rss.xml");

        var undated = items.Single(item => item.Title == "Undated weather note");
        Assert.Equal(FallbackDate, undated.PublishedAt);
    }

    [Fact]
    public void Parse_AtomFeed_IsSupported()
    {
        var items = ParseSample("RssReader/Samples/sample-atom.xml");

        var entry = Assert.Single(items);
        Assert.Equal("Sample Atom News", entry.SourceName);
        Assert.Equal("Atom entry about rail services", entry.Title);
        Assert.Equal("Timetable changes start Monday.", entry.Summary);
        Assert.Equal("https://atom.example.com/rail", entry.Link);
    }

    private static IReadOnlyList<FeedItem> ParseSample(string relativePath)
    {
        using var reader = XmlReader.Create(Path.Combine(AppContext.BaseDirectory, relativePath));
        return FeedParser.Parse(reader, "test-source", FallbackDate);
    }
}
