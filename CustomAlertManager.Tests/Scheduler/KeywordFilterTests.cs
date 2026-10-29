using CustomAlertManager.Common.Models;
using CustomAlertManager.Scheduler.Services;

namespace CustomAlertManager.Tests.Scheduler;

public class KeywordFilterTests
{
    private static FeedItem Item(string title, string summary = "") =>
        new("Source", title, summary, "https://example.com", DateTimeOffset.UtcNow);

    [Fact]
    public void IsMatch_IncludeTermInTitle_IsCaseInsensitive()
    {
        var filter = new KeywordFilter(["ELECTION"], []);

        Assert.True(filter.IsMatch(Item("Election results announced")));
    }

    [Fact]
    public void IsMatch_IncludeTermOnlyInSummary_Matches()
    {
        var filter = new KeywordFilter(["election"], []);

        Assert.True(filter.IsMatch(Item("Markets close higher", "after the election outlook")));
    }

    [Fact]
    public void IsMatch_AnyIncludeTermIsEnough()
    {
        var filter = new KeywordFilter(["rail", "election"], []);

        Assert.True(filter.IsMatch(Item("Election results announced")));
    }

    [Fact]
    public void IsMatch_NoIncludeTermPresent_DoesNotMatch()
    {
        var filter = new KeywordFilter(["rail", "election"], []);

        Assert.False(filter.IsMatch(Item("Local football roundup")));
    }

    [Fact]
    public void IsMatch_ExcludeTermWinsOverIncludeTerm()
    {
        var filter = new KeywordFilter(["election"], ["sport"]);

        Assert.False(filter.IsMatch(Item("Election sport special")));
    }

    [Fact]
    public void IsMatch_NoIncludeTerms_MatchesEverythingNotExcluded()
    {
        var filter = new KeywordFilter([], ["football"]);

        Assert.True(filter.IsMatch(Item("Election results announced")));
        Assert.False(filter.IsMatch(Item("Local football roundup")));
    }
}
