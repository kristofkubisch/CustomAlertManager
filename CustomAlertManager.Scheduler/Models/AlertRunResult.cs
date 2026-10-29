using CustomAlertManager.Common.Models;

namespace CustomAlertManager.Scheduler.Models;

public sealed record AlertRunResult(
    int FeedCount,
    DateTimeOffset Cutoff,
    IReadOnlyList<FeedItem> Matches);
