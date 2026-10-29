using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.Data.Entities;

public sealed class NotificationTarget : TimestampedEntity
{
    public int Id { get; set; }

    // Null for targets created by the development seed.
    public int? OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ChannelKind Kind { get; set; }

    // An email address for Email targets, a webhook URL for Slack targets.
    public string Destination { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    // Email targets must confirm ownership before they receive alerts. Slack webhooks are verified on creation.
    public bool IsVerified { get; set; }
}
