using CustomAlertManager.Common.Models;

namespace CustomAlertManager.AlertSender.Interfaces;

public interface IAlertChannel
{
    Task SendAsync(IReadOnlyList<FeedItem> items, CancellationToken cancellationToken);
}
