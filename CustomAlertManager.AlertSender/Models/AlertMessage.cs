using CustomAlertManager.Common.Models;

namespace CustomAlertManager.AlertSender.Models;

public sealed record AlertMessage(string Title, IReadOnlyList<FeedItem> Items);
