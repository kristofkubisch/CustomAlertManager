using System.Net.Http.Json;
using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.AlertSender.Services;

// Posts to a Slack incoming webhook. The webhook URL itself is the credential.
public sealed class SlackAlertChannel(HttpClient httpClient) : IAlertChannel
{
    public ChannelKind Kind => ChannelKind.Slack;

    public static bool IsValidWebhook(string destination) =>
        Uri.TryCreate(destination, UriKind.Absolute, out var webhook)
        && webhook.Scheme == Uri.UriSchemeHttps
        && webhook.Host == "hooks.slack.com";

    public async Task<DeliveryResult> SendAsync(string destination, AlertMessage message, CancellationToken cancellationToken)
    {
        if (!IsValidWebhook(destination))
        {
            return DeliveryResult.Permanent("Invalid Slack webhook URL. It must be an https://hooks.slack.com/ URL.");
        }

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                new Uri(destination),
                new { text = FormatText(message) },
                cancellationToken);
            return HttpResultClassifier.Classify(response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            return DeliveryResult.Transient(ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DeliveryResult.Transient("The request to Slack timed out.");
        }
    }

    private static string FormatText(AlertMessage message)
    {
        var lines = new List<string> { $"*{Escape(message.Title)}*" };
        lines.AddRange(message.Items.Select(item =>
            $"• <{Escape(item.Link)}|{Escape(item.Title)}> ({Escape(item.SourceName)})"));
        return string.Join('\n', lines);
    }

    // Slack requires these three characters to be escaped in message text.
    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
