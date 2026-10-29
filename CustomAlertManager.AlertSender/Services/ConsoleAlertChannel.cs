using CustomAlertManager.Common.Models;

namespace CustomAlertManager.AlertSender.Services;

// Prints matches to the console. Used by the prototype host, not as a delivery target.
public sealed class ConsoleAlertChannel
{
    public Task SendAsync(IReadOnlyList<FeedItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            Console.WriteLine("No matching items.");
            return Task.CompletedTask;
        }

        foreach (var item in items)
        {
            Console.WriteLine($"[{item.PublishedAt.ToLocalTime():g}] {item.SourceName}");
            Console.WriteLine($"  {item.Title}");
            Console.WriteLine($"  {item.Link}");
            Console.WriteLine();
        }

        return Task.CompletedTask;
    }
}
