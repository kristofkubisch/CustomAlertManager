using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.DataProtection;

namespace CustomAlertManager.Tests.Web;

public class TargetVerificationTokensTests
{
    [Fact]
    public void TryRead_RoundTrip_ReturnsTargetAndOwner()
    {
        var tokens = new TargetVerificationTokens(new EphemeralDataProtectionProvider());

        var token = tokens.Create(42, 1);

        Assert.True(tokens.TryRead(token, out var targetId, out var ownerId));
        Assert.Equal(42, targetId);
        Assert.Equal(1, ownerId);
    }

    [Fact]
    public void TryRead_TamperedToken_IsRejected()
    {
        var tokens = new TargetVerificationTokens(new EphemeralDataProtectionProvider());
        var token = tokens.Create(42, 1);

        // Change a character in the middle, so the payload itself changes rather than only base64 padding.
        var middle = token.Length / 2;
        var tampered = token[..middle] + (token[middle] == 'A' ? 'B' : 'A') + token[(middle + 1)..];

        Assert.False(tokens.TryRead(tampered, out _, out _));
    }

    [Fact]
    public void TryRead_TokenFromAnotherKeyRing_IsRejected()
    {
        var issuer = new TargetVerificationTokens(new EphemeralDataProtectionProvider());
        var other = new TargetVerificationTokens(new EphemeralDataProtectionProvider());

        Assert.False(other.TryRead(issuer.Create(42, 1), out _, out _));
    }

    [Fact]
    public void TryRead_Garbage_IsRejected()
    {
        var tokens = new TargetVerificationTokens(new EphemeralDataProtectionProvider());

        Assert.False(tokens.TryRead("not-a-token", out _, out _));
    }
}
