using System.Net;
using CustomAlertManager.AlertSender.Models;

namespace CustomAlertManager.AlertSender.Services;

public static class HttpResultClassifier
{
    // Server errors, timeouts and rate limits may succeed later. Other client errors will not.
    public static DeliveryResult Classify(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        if (code is >= 200 and < 300)
        {
            return DeliveryResult.Sent();
        }

        var error = $"HTTP {code}";
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || code >= 500
            ? DeliveryResult.Transient(error)
            : DeliveryResult.Permanent(error);
    }
}
