using System.Net;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomAlertManager.Tests.Web;

[Collection("WebApp")]
public class VerificationTests
{
    [Fact]
    public async Task ConfirmationLink_WorksWithoutSigningIn()
    {
        await using var factory = new TestWebApplicationFactory();
        var (targetId, token) = await CreatePendingEmailTargetAsync(factory);

        // A client that has never logged in, the way someone opens the link from their email.
        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/Targets/Verify?token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("is confirmed and can now receive alerts", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        Assert.True((await db.NotificationTargets.AsNoTracking().SingleAsync(target => target.Id == targetId)).IsVerified);
    }

    [Fact]
    public async Task ConfirmationLink_Tampered_IsRejected()
    {
        await using var factory = new TestWebApplicationFactory();
        var (_, token) = await CreatePendingEmailTargetAsync(factory);
        var tampered = token[..(token.Length / 2)] + (token[token.Length / 2] == 'A' ? 'B' : 'A') + token[(token.Length / 2 + 1)..];

        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/Targets/Verify?token={Uri.EscapeDataString(tampered)}");

        Assert.Contains("is invalid or has expired", await response.Content.ReadAsStringAsync());
    }

    private static async Task<(int TargetId, string Token)> CreatePendingEmailTargetAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = provider.GetRequiredService<AlertDbContext>();

        var username = $"verify{Guid.NewGuid():N}"[..20];
        var user = new ApplicationUser { UserName = username, Email = $"{username}@example.com" };
        await users.CreateAsync(user, WebForms.Password);

        // Written directly rather than through the form, so no real email is sent during the test.
        var target = new NotificationTarget
        {
            OwnerId = user.Id,
            Name = "Mailbox",
            Kind = ChannelKind.Email,
            Destination = $"{username}@example.com",
            IsVerified = false,
        };
        db.NotificationTargets.Add(target);
        await db.SaveChangesAsync();

        var token = provider.GetRequiredService<TargetVerificationTokens>().Create(target.Id, user.Id);
        return (target.Id, token);
    }
}
