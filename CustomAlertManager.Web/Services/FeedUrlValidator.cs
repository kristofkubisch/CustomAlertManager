using System.Net;
using System.Net.Sockets;

namespace CustomAlertManager.Web.Services;

// Feeds are fetched by the server, so addresses that point inside the network are refused.
// This checks the address as written. It cannot see a public name that resolves to a private address.
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

        if (!IPAddress.TryParse(host.Trim('[', ']'), out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var first = address.GetAddressBytes()[0];
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || (first & 0xFE) == 0xFC;
        }

        var octets = address.GetAddressBytes();
        return octets[0] switch
        {
            0 or 10 or 127 => true,
            169 => octets[1] == 254,
            172 => octets[1] is >= 16 and <= 31,
            192 => octets[1] == 168,
            100 => octets[1] is >= 64 and <= 127,
            _ => false,
        };
    }
}
