using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Interfaces;

public interface IAlertStore
{
    Task<IReadOnlyList<Subscription>> GetEnabledSubscriptionsAsync(CancellationToken cancellationToken);

    // Stores fetched items once per source and item, ignoring items already stored.
    Task SaveFetchedItemsAsync(IReadOnlyList<SourcedItem> items, CancellationToken cancellationToken);

    // Always records the run time. When there are matches, also records an alert and advances LastAlertAt.
    Task CompleteRunAsync(
        int subscriptionId,
        DateTimeOffset runStartedAt,
        IReadOnlyList<SourcedItem> matches,
        CancellationToken cancellationToken);
}
