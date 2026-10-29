using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Prototype.Options;

public static class ArgumentParser
{
    public const string Usage = """
        Usage:
          CustomAlertManager.Prototype --feed <url> [--feed <url>...]
                                        [--include <term,term>] [--exclude <term,term>]
                                        [--since <30m|2h|1d>]

        Defaults: --since 24h. Include terms match if any term appears in the title or summary.
        """;

    private static readonly string[] KnownOptions = ["--feed", "--include", "--exclude", "--since"];

    // The prototype runs once and stores nothing, so Id, Frequency and LastRunAt are placeholders.
    public static Subscription Parse(IReadOnlyList<string> args, DateTimeOffset now)
    {
        var feedUrls = new List<string>();
        var includeTerms = new List<string>();
        var excludeTerms = new List<string>();
        var since = TimeSpan.FromHours(24);

        for (var i = 0; i < args.Count; i++)
        {
            var option = args[i];
            if (!KnownOptions.Contains(option))
            {
                throw new ArgumentException($"Unknown option '{option}'.");
            }

            if (i + 1 >= args.Count)
            {
                throw new ArgumentException($"Option '{option}' needs a value.");
            }

            var value = args[++i];
            switch (option)
            {
                case "--feed":
                    feedUrls.Add(ValidateFeedUrl(value));
                    break;
                case "--include":
                    includeTerms.AddRange(SplitTerms(value));
                    break;
                case "--exclude":
                    excludeTerms.AddRange(SplitTerms(value));
                    break;
                case "--since":
                    since = ParseDuration(value);
                    break;
            }
        }

        if (feedUrls.Count == 0)
        {
            throw new ArgumentException("At least one --feed is required.");
        }

        var sources = feedUrls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((url, index) => new SourceRef(index + 1, url))
            .ToList();

        return new Subscription(
            Id: 0,
            Frequency: TimeSpan.FromMinutes(1),
            LastRunAt: null,
            LastAlertAt: now - since,
            Sources: sources,
            IncludeTerms: includeTerms,
            ExcludeTerms: excludeTerms);
    }

    public static TimeSpan ParseDuration(string value)
    {
        var message = $"Invalid --since value '{value}'. Use a whole number followed by m, h or d, e.g. 30m, 2h, 1d.";
        if (value.Length < 2 || !int.TryParse(value[..^1], out var amount) || amount <= 0)
        {
            throw new ArgumentException(message);
        }

        return value[^1] switch
        {
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            'd' => TimeSpan.FromDays(amount),
            _ => throw new ArgumentException(message),
        };
    }

    private static string ValidateFeedUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Feed URL must be an absolute http or https URL: '{value}'.");
        }

        return value;
    }

    private static IEnumerable<string> SplitTerms(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
