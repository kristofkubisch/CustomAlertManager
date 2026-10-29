namespace CustomAlertManager.AlertSender.Models;

public sealed record SendGridOptions(string ApiKey, string FromAddress, string FromName);
