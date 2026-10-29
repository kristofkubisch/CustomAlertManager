namespace CustomAlertManager.Web.Services;

public static class KeywordList
{
    public const int MaxTermLength = 200;
    public const int MaxTermsPerList = 50;

    // Terms are separated by commas or new lines. Duplicates are ignored regardless of case.
    public static IReadOnlyList<string> Parse(string? input) =>
        (input ?? string.Empty)
            .Split([',', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .DistinctBy(term => term, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string? Validate(IReadOnlyList<string> terms)
    {
        if (terms.Count > MaxTermsPerList)
        {
            return $"Use at most {MaxTermsPerList} keywords in each list.";
        }

        return terms.Any(term => term.Length > MaxTermLength)
            ? $"Each keyword can be at most {MaxTermLength} characters."
            : null;
    }
}
