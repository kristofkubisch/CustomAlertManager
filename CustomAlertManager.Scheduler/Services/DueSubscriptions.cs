using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Services;

public static class DueSubscriptions
{
    // A subscription is due when it has never run, or its frequency has elapsed since the last run.
    public static IReadOnlyList<Subscription> Select(IEnumerable<Subscription> subscriptions, DateTimeOffset now) =>
        subscriptions
            .Where(subscription => subscription.LastRunAt is null || now - subscription.LastRunAt.Value >= subscription.Frequency)
            .ToList();
}
