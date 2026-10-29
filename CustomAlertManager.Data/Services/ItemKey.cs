using System.Security.Cryptography;
using System.Text;

namespace CustomAlertManager.Data.Services;

public static class ItemKey
{
    public static string Of(string itemId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(itemId))).ToLowerInvariant();
}
