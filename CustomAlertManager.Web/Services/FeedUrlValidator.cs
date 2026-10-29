using System.Net;
using CustomAlertManager.RssReader.Services;

namespace CustomAlertManager.Web.Services;

// Checks an address as it is typed, so the admin gets an immediate message. The connection guard in the
// RSS reader enforces the same rule again when the feed is actually fetched, including after redirects.
public static class FeedUrlValidator
{
    public static string? Validate(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Enter an absolute http or https address.";
        }

        return IsLocalOrPrivateHost(uri.Host)
            ? "Feeds must be on a public address, not localhost or a private network."
            : null;
    }

    public static bool IsLocalOrPrivateHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host.Trim('[', ']'), out var address) && PrivateAddressClassifier.IsPrivate(address);
    }
}
