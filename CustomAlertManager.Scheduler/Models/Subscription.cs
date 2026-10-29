namespace CustomAlertManager.Scheduler.Models;

public sealed record Subscription(
    int Id,
    TimeSpan Frequency,
    DateTimeOffset? LastRunAt,
    DateTimeOffset LastAlertAt,
    IReadOnlyList<SourceRef> Sources,
    IReadOnlyList<string> IncludeTerms,
    IReadOnlyList<string> ExcludeTerms);
