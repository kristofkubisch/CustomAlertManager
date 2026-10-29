namespace CustomAlertManager.Scheduler.Models;

public sealed record DeliveryUpdate(
    int DeliveryId,
    DeliveryStatus Status,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? SentAt,
    string? LastError);
