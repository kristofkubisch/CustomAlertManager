using System.Net.Http.Headers;
using System.Net.Http.Json;
using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.AlertSender.Services;

// Sends through the SendGrid v3 mail API. The HttpClient base address must be https://api.sendgrid.com/.
public sealed class SendGridAlertChannel(HttpClient httpClient, SendGridOptions options) : IAlertChannel
{
    public ChannelKind Kind => ChannelKind.Email;

    public async Task<DeliveryResult> SendAsync(string destination, AlertMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return DeliveryResult.Permanent("The SendGrid API key is not configured.");
        }

        if (!IsValidAddress(destination))
        {
            return DeliveryResult.Permanent($"Invalid email address: '{destination}'.");
        }

        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = destination } } } },
            @from = new { email = options.FromAddress, name = options.FromName },
            subject = message.Title,
            content = new[] { new { type = "text/plain", value = AlertTextFormatter.ToPlainText(message) } },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/mail/send")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return HttpResultClassifier.Classify(response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            return DeliveryResult.Transient(ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DeliveryResult.Transient("The request to SendGrid timed out.");
        }
    }

    private static bool IsValidAddress(string address)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(address);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
