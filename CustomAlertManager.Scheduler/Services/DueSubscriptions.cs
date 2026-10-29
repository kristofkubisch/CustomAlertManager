using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Services;

public static class DueSubscriptions
{
    // The worker runs on a one-minute timer, and each run records the moment it started. The gap between
    // two runs therefore varies slightly around the exact interval. Half a poll interval of tolerance keeps
    // a subscription from slipping a whole cycle (for example, a 15-minute subscription running every 16).
    public static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(30);

    // A subscription is due when it has never run, or its frequency has elapsed since the last run.
    public static IReadOnlyList<Subscription> Select(IEnumerable<Subscription> subscriptions, DateTimeOffset now) =>
        subscriptions
            .Where(subscription => subscription.LastRunAt is null
                || now - subscription.LastRunAt.Value >= subscription.Frequency - Tolerance)
            .ToList();
}
