using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace CustomAlertManager.RssReader.Services;

// Runs for every connection the feed client opens, including those made after redirects. The address is checked
// on the connection itself, so a name that resolves to a private address (or changes between lookups) is refused.
public static class PublicOnlyConnector
{
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(PrivateAddressClassifier.IsPrivate))
        {
            throw new HttpRequestException($"The feed host '{host}' resolves to a private or local address, which is not allowed.");
        }

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                lastError = ex;
                socket.Dispose();
            }
        }

        throw new HttpRequestException($"Could not connect to '{host}'.", lastError);
    }

    // Use this handler for the feed client so the check above applies to every request.
    public static SocketsHttpHandler CreateHandler() => new()
    {
        ConnectCallback = ConnectAsync,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };
}
