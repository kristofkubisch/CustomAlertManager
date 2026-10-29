using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Services;

namespace CustomAlertManager.Worker;

public sealed class AlertWorker(
    IServiceScopeFactory scopeFactory,
    IAlertStore store,
    IRunLock runLock,
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

        // A scope per cycle gives the engine and channels fresh HTTP clients each time.
        await using var scope = scopeFactory.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IAlertEngine>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IAlertDispatcher>();

        var now = DateTimeOffset.UtcNow;
        var due = DueSubscriptions.Select(await store.GetEnabledSubscriptionsAsync(cancellationToken), now);
        if (due.Count > 0)
        {
            var result = await engine.ProcessAsync(due, cancellationToken);
            await store.SaveFetchedItemsAsync(result.FetchedItems, cancellationToken);

            // Alerts and their pending deliveries are stored before anything is sent.
            foreach (var subscriptionMatches in result.Matches)
            {
                await store.CompleteRunAsync(subscriptionMatches.SubscriptionId, now, subscriptionMatches.Items, cancellationToken);
            }

            logger.LogInformation(
                "Ran {DueCount} due subscription(s); fetched {FetchedCount} item(s).",
                due.Count,
                result.FetchedItems.Count);
        }

        // Runs every cycle so retries progress even when no subscription is due.
        var attempts = await dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, cancellationToken);
        if (attempts > 0)
        {
            logger.LogInformation("Made {AttemptCount} delivery attempt(s).", attempts);
        }
    }
}
