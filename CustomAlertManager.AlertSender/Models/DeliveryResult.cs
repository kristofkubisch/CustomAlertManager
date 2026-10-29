namespace CustomAlertManager.AlertSender.Models;

public enum DeliveryOutcome
{
    Sent,
    TransientFailure,
    PermanentFailure,
}

public sealed record DeliveryResult(DeliveryOutcome Outcome, string? Error = null)
{
    public static DeliveryResult Sent() => new(DeliveryOutcome.Sent);

    public static DeliveryResult Transient(string error) => new(DeliveryOutcome.TransientFailure, error);

    public static DeliveryResult Permanent(string error) => new(DeliveryOutcome.PermanentFailure, error);
}
