using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.Data.Entities;

public sealed class NotificationTarget
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ChannelKind Kind { get; set; }

    // An email address for Email targets, a webhook URL for Slack targets.
    public string Destination { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
