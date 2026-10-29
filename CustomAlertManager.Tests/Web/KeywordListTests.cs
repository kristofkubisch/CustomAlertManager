using CustomAlertManager.Web.Services;

namespace CustomAlertManager.Tests.Web;

public class KeywordListTests
{
    [Fact]
    public void Parse_SplitsOnCommasAndNewLinesAndTrims()
    {
        var terms = KeywordList.Parse(" election , markets\nsport\r\n ");

        Assert.Equal(["election", "markets", "sport"], terms);
    }

    [Fact]
    public void Parse_IgnoresDuplicatesRegardlessOfCase()
    {
        var terms = KeywordList.Parse("Election, election, ELECTION, markets");

        Assert.Equal(["Election", "markets"], terms);
    }

    [Fact]
    public void Parse_NullOrBlank_ReturnsEmpty()
    {
        Assert.Empty(KeywordList.Parse(null));
        Assert.Empty(KeywordList.Parse(" , \n "));
    }

    [Fact]
    public void Validate_TooManyTerms_ReturnsError()
    {
        var terms = Enumerable.Range(1, KeywordList.MaxTermsPerList + 1).Select(i => $"term{i}").ToList();

        Assert.NotNull(KeywordList.Validate(terms));
    }

    [Fact]
    public void Validate_TermTooLong_ReturnsError()
    {
        var terms = new List<string> { new('x', KeywordList.MaxTermLength + 1) };

        Assert.NotNull(KeywordList.Validate(terms));
    }

    [Fact]
    public void Validate_ValidList_ReturnsNull()
    {
        Assert.Null(KeywordList.Validate(["election", "markets"]));
    }
}
