using CustomAlertManager.Common.Models;

namespace CustomAlertManager.Scheduler.Models;

public sealed record SourcedItem(int SourceId, FeedItem Item);
