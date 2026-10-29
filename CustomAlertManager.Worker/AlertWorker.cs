using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Services;

namespace CustomAlertManager.Worker;

public sealed class AlertWorker(
    IAlertEngine engine,
    IAlertStore store,
    IRunLock runLock,
    IAlertChannel channel,
    ILogger<AlertWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alert cycle failed; the next cycle will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        await using var lease = await runLock.TryAcquireAsync(cancellationToken);
        if (lease is null)
        {
            logger.LogInformation("Another alert run holds the lock; skipping this cycle.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var due = DueSubscriptions.Select(await store.GetEnabledSubscriptionsAsync(cancellationToken), now);
        if (due.Count == 0)
        {
            return;
        }

        var result = await engine.ProcessAsync(due, cancellationToken);
        await store.SaveFetchedItemsAsync(result.FetchedItems, cancellationToken);

        // Each alert is stored before it is sent, so a failed send never loses it.
        foreach (var subscriptionMatches in result.Matches)
        {
            await store.CompleteRunAsync(subscriptionMatches.SubscriptionId, now, subscriptionMatches.Items, cancellationToken);

            if (subscriptionMatches.Items.Count > 0)
            {
                var items = subscriptionMatches.Items.Select(sourced => sourced.Item).ToList();
                await channel.SendAsync(items, cancellationToken);
            }
        }

        logger.LogInformation(
            "Ran {DueCount} due subscription(s); fetched {FetchedCount} item(s).",
            due.Count,
            result.FetchedItems.Count);
    }
}
