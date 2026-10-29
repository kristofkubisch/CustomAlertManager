using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.AlertSender.Services;

public static class AlertTextFormatter
{
    public static string ToPlainText(AlertMessage message)
    {
        var lines = new List<string> { message.Title, string.Empty };
        foreach (var item in message.Items)
        {
            lines.Add($"- {item.Title}");
            lines.Add($"  {item.Link}");
            lines.Add($"  {item.SourceName}, {item.PublishedAt.ToUniversalTime():yyyy-MM-dd HH:mm} UTC");
        }

        return string.Join('\n', lines);
    }
}
