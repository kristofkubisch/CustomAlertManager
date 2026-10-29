using System.Net;
using System.Net.Sockets;

namespace CustomAlertManager.RssReader.Services;

// Addresses inside the network or on the local machine. The server must never fetch these on a user's behalf.
public static class PrivateAddressClassifier
{
    public static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || IPAddress.IPv6None.Equals(address) || IPAddress.Any.Equals(address))
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
