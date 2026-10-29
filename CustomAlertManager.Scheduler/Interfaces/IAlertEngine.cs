using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Interfaces;

public interface IAlertEngine
{
    Task<EngineResult> ProcessAsync(IReadOnlyList<Subscription> subscriptions, CancellationToken cancellationToken);
}
