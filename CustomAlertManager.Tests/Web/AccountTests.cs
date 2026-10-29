using System.Net;
using System.Text.RegularExpressions;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CustomAlertManager.Tests.Web;

[Collection("WebApp")]
public class AccountTests
{
    private const string Password = WebForms.Password;

    [Fact]
    public async Task SignUp_NewAccount_GetsUserRoleAndCanLogInWithUsername()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var username = $"user{Guid.NewGuid():N}"[..20];
        var email = $"{username}@example.com";

        var signUp = await WebForms.PostFormAsync(client, "/Identity/Account/Register", new()
        {
            ["Input.Username"] = username,
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
            ["Input.ConfirmPassword"] = Password,
        });

        Assert.Equal(HttpStatusCode.Redirect, signUp.StatusCode);
        var users = factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByNameAsync(username);
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.True(await users.IsInRoleAsync(user, IdentitySeeder.UserRole));
        Assert.False(await users.IsInRoleAsync(user, IdentitySeeder.AdminRole));

        using var loginClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await WebForms.PostFormAsync(loginClient, "/Identity/Account/Login", new()
        {
            ["Input.Username"] = username,
            ["Input.Password"] = Password,
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    [Fact]
    public async Task SignUp_DuplicateEmail_IsRejected()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await WebForms.PostFormAsync(client, "/Identity/Account/Register", WebForms.SignUpFields($"first{Guid.NewGuid():N}"[..20], email));

        var second = await WebForms.PostFormAsync(client, "/Identity/Account/Register", WebForms.SignUpFields($"second{Guid.NewGuid():N}"[..20], email));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("An account with this email address already exists.", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WithEmailInsteadOfUsername_IsRejected()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var username = $"mail{Guid.NewGuid():N}"[..20];
        var email = $"{username}@example.com";
        await WebForms.PostFormAsync(client, "/Identity/Account/Register", WebForms.SignUpFields(username, email));

        var login = await WebForms.PostFormAsync(client, "/Identity/Account/Login", new()
        {
            ["Input.Username"] = email,
            ["Input.Password"] = Password,
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("Invalid username or password.", await login.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DefaultAdmin_IsSeededFromConfiguration()
    {
        await using var factory = new TestWebApplicationFactory();
        var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await users.FindByNameAsync("admin");

        Assert.NotNull(admin);
        Assert.Equal("kristof.kubisch@gmail.com", admin.Email);
        // The password itself lives in user secrets, so it is not checked here.
        Assert.True(await users.IsInRoleAsync(admin, IdentitySeeder.AdminRole));
    }

}
