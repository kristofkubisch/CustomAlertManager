using CustomAlertManager.Common.Models;

namespace CustomAlertManager.Scheduler.Services;

public sealed class KeywordFilter(IEnumerable<string> includeTerms, IEnumerable<string> excludeTerms)
{
    private readonly string[] _includeTerms = includeTerms.ToArray();
    private readonly string[] _excludeTerms = excludeTerms.ToArray();

    // Exclude terms always win. With no include terms, every item that is not excluded matches.
    public bool IsMatch(FeedItem item)
    {
        var text = $"{item.Title} {item.Summary}";

        if (_excludeTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return _includeTerms.Length == 0
            || _includeTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
