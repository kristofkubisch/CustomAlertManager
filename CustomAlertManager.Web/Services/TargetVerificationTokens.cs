using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CustomAlertManager.Web.Services;

// Confirmation links for email targets. The token carries the target and its owner, and expires after 24 hours.
public sealed class TargetVerificationTokens(IDataProtectionProvider dataProtectionProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector("CustomAlertManager.TargetVerification")
        .ToTimeLimitedDataProtector();

    public string Create(int targetId, int ownerId) =>
        _protector.Protect($"{targetId}|{ownerId}", Lifetime);

    public bool TryRead(string token, out int targetId, out int ownerId)
    {
        targetId = 0;
        ownerId = 0;

        try
        {
            var parts = _protector.Unprotect(token).Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out targetId) || !int.TryParse(parts[1], out ownerId))
            {
                return false;
            }

            return true;
        }
        catch (CryptographicException)
        {
            // Tampered, malformed, or expired tokens all end up here.
            return false;
        }
    }
}
