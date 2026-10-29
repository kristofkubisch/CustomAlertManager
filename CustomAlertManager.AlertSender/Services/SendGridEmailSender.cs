using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace CustomAlertManager.AlertSender.Services;

// Sends plain-text mail through the SendGrid C# library. The client is created on first use, so a missing
// API key is reported as a delivery failure instead of stopping the application at startup.
public sealed class SendGridEmailSender(SendGridOptions options, ISendGridClient? client = null) : IEmailSender
{
    private ISendGridClient? _client = client;

    public async Task<DeliveryResult> SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return DeliveryResult.Permanent("The SendGrid API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            return DeliveryResult.Permanent("The SendGrid sender address is not configured.");
        }

        if (!EmailAddressValidation.IsValid(to))
        {
            return DeliveryResult.Permanent($"Invalid email address: '{to}'.");
        }

        var message = new SendGridMessage
        {
            From = new EmailAddress(options.FromAddress, options.FromName),
            Subject = subject,
            PlainTextContent = body,
        };
        message.AddTo(new EmailAddress(to));

        try
        {
            var response = await (_client ??= new SendGridClient(options.ApiKey)).SendEmailAsync(message, cancellationToken);
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
}
