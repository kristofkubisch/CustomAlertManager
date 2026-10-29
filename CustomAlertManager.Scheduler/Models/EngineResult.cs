namespace CustomAlertManager.Scheduler.Models;

public sealed record EngineResult(
    IReadOnlyList<SourcedItem> FetchedItems,
    IReadOnlyList<SubscriptionMatches> Matches);

public sealed record SubscriptionMatches(int SubscriptionId, IReadOnlyList<SourcedItem> Items);
