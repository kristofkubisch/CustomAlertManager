namespace CustomAlertManager.Scheduler.Interfaces;

public interface IRunLock
{
    // Returns null when another run already holds the lock. Disposing the result releases it.
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken);
}
