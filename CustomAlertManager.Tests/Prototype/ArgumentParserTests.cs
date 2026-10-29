using CustomAlertManager.Prototype.Options;

namespace CustomAlertManager.Tests.Prototype;

public class ArgumentParserTests
{
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
        ]);

        Assert.Equal(["https://feeds.example.com/a.xml", "https://feeds.example.com/b.xml"], subscription.FeedUrls);
        Assert.Equal(["election", "markets"], subscription.IncludeTerms);
        Assert.Equal(["sport"], subscription.ExcludeTerms);
        Assert.Equal(TimeSpan.FromHours(2), subscription.Since);
    }

    [Fact]
    public void Parse_NoSince_DefaultsToTwentyFourHours()
    {
        var subscription = ArgumentParser.Parse(["--feed", "https://feeds.example.com/a.xml"]);

        Assert.Equal(TimeSpan.FromHours(24), subscription.Since);
        Assert.Empty(subscription.IncludeTerms);
        Assert.Empty(subscription.ExcludeTerms);
    }

    [Fact]
    public void Parse_NoFeed_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(["--include", "election"]));

        Assert.Contains("--feed", ex.Message);
    }

    [Fact]
    public void Parse_NonHttpFeedUrl_Throws()
    {
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(["--feed", "file:///etc/passwd"]));
    }

    [Fact]
    public void Parse_OptionWithoutValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(["--feed"]));
    }

    [Fact]
    public void Parse_UnknownOption_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(["--feed", "https://feeds.example.com/a.xml", "--bogus", "x"]));

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
