namespace CustomAlertManager.Scheduler.Models;

public sealed record Subscription(
    IReadOnlyList<string> FeedUrls,
    IReadOnlyList<string> IncludeTerms,
    IReadOnlyList<string> ExcludeTerms,
    TimeSpan Since);
