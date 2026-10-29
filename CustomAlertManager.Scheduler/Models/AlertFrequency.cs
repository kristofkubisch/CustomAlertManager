namespace CustomAlertManager.Scheduler.Models;

public static class AlertFrequency
{
    // The only frequencies a subscription may use, in minutes.
    public static IReadOnlyList<int> PresetMinutes { get; } = [1, 5, 15, 30, 60];
}
