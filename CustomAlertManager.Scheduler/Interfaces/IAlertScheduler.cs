using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Interfaces;

public interface IAlertScheduler
{
    Task<AlertRunResult> RunAsync(Subscription subscription, CancellationToken cancellationToken);
}
