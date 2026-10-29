namespace CustomAlertManager.Scheduler.Interfaces;

public interface IAlertDispatcher
{
    // Attempts every delivery that is due and returns how many attempts were made.
    Task<int> DispatchDueAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
