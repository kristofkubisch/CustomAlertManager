using CustomAlertManager.Prototype.Options;

namespace CustomAlertManager.Tests.Prototype;

public class ArgumentParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_AllOptions_ReadsEachValue()
    {
        var subscription = ArgumentParser.Parse(
        [
            "--feed", "https://feeds.example.com/a.xml",
            "--feed", "https://feeds.example.com/b.xml",
            "--include", "election, markets",
            "--exclude", "sport",
            "--since", "2h",
        ], Now);

        Assert.Equal(
            ["https://feeds.example.com/a.xml", "https://feeds.example.com/b.xml"],
            subscription.Sources.Select(source => source.Url));
        Assert.Equal(["election", "markets"], subscription.IncludeTerms);
        Assert.Equal(["sport"], subscription.ExcludeTerms);
        Assert.Equal(Now - TimeSpan.FromHours(2), subscription.LastAlertAt);
    }

    [Fact]
    public void Parse_NoSince_DefaultsToTwentyFourHours()
    {
        var subscription = ArgumentParser.Parse(["--feed", "https://feeds.example.com/a.xml"], Now);

        Assert.Equal(Now - TimeSpan.FromHours(24), subscription.LastAlertAt);
        Assert.Empty(subscription.IncludeTerms);
        Assert.Empty(subscription.ExcludeTerms);
    }

    [Fact]
    public void Parse_DuplicateFeedUrls_AreKeptOnce()
    {
        var subscription = ArgumentParser.Parse(
            ["--feed", "https://feeds.example.com/a.xml", "--feed", "https://FEEDS.example.com/a.xml"], Now);

        Assert.Single(subscription.Sources);
    }

    [Fact]
    public void Parse_NoFeed_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(["--include", "election"], Now));

        Assert.Contains("--feed", ex.Message);
    }

    [Fact]
    public void Parse_NonHttpFeedUrl_Throws()
    {
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(["--feed", "file:///etc/passwd"], Now));
    }

    [Fact]
    public void Parse_OptionWithoutValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(["--feed"], Now));
    }

    [Fact]
    public void Parse_UnknownOption_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(["--feed", "https://feeds.example.com/a.xml", "--bogus", "x"], Now));

        Assert.Contains("--bogus", ex.Message);
    }

    [Theory]
    [InlineData("30m", 30, 0, 0)]
    [InlineData("2h", 0, 2, 0)]
    [InlineData("1d", 0, 0, 1)]
    public void ParseDuration_SupportedUnits_ReturnsExpectedSpan(string value, int minutes, int hours, int days)
    {
        var expected = TimeSpan.FromMinutes(minutes) + TimeSpan.FromHours(hours) + TimeSpan.FromDays(days);

        Assert.Equal(expected, ArgumentParser.ParseDuration(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("h")]
    [InlineData("0h")]
    [InlineData("5x")]
    [InlineData("abc")]
    public void ParseDuration_InvalidValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => ArgumentParser.ParseDuration(value));
    }
}
