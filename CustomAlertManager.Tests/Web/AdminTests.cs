using System.Net;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using CustomAlertManager.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CustomAlertManager.Tests.Web;

[Collection("WebApp")]
public class AdminTests
{
    [Fact]
    public async Task AdminPages_RegularUser_IsDenied()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignUpAsync(client, factory);

        foreach (var path in new[] { "/Admin/Sources", "/Admin/Users", "/Admin/Subscriptions", "/Admin/AlertTests" })
        {
            var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task AdminPages_Admin_IsAllowed()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LogInAsAdminAsync(client, factory);

        var response = await client.GetAsync("/Admin/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_CannotLogIn()
    {
        await using var factory = new TestWebApplicationFactory();
        var username = await CreateUserAsync(factory);
        await DeactivateAsync(factory, username);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await WebForms.PostFormAsync(client, "/Identity/Account/Login", new()
        {
            ["Input.Username"] = username,
            ["Input.Password"] = WebForms.Password,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This account has been deactivated", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeactivatedUser_WithReturnUrl_SeesTheMessage()
    {
        await using var factory = new TestWebApplicationFactory();
        var username = await CreateUserAsync(factory);
        await DeactivateAsync(factory, username);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await WebForms.PostFormAsync(client, "/Identity/Account/Login?ReturnUrl=%2FSubscriptions", new()
        {
            ["Input.Username"] = username,
            ["Input.Password"] = WebForms.Password,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This account has been deactivated", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeactivatedUser_LosesAnExistingSession()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var username = await SignUpAsync(client, factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Subscriptions")).StatusCode);

        await DeactivateAsync(factory, username);

        var response = await client.GetAsync("/Subscriptions");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Admin_CannotDeactivateOrDeleteOwnAccount()
    {
        await using var factory = new TestWebApplicationFactory();
        var admin = await AdminAsync(factory);
        var administration = Scope(factory).ServiceProvider.GetRequiredService<UserAdministrationService>();

        Assert.NotNull(await administration.SetActiveAsync(admin.Id, admin.Id, false, CancellationToken.None));
        Assert.NotNull(await administration.DeleteAsync(admin.Id, admin.Id, CancellationToken.None));
        Assert.True((await AdminAsync(factory)).IsActive);
    }

    [Fact]
    public async Task DeleteUser_RemovesTheirSubscriptionsAndTargets()
    {
        await using var factory = new TestWebApplicationFactory();
        var username = await CreateUserAsync(factory);
        var user = await UserAsync(factory, username);
        var targetId = await AddTargetAsync(factory, user.Id);
        var subscriptionId = await AddSubscriptionAsync(factory, user.Id, targetId);
        var admin = await AdminAsync(factory);

        var error = await Scope(factory).ServiceProvider.GetRequiredService<UserAdministrationService>()
            .DeleteAsync(user.Id, admin.Id, CancellationToken.None);

        Assert.Null(error);
        using var scope = Scope(factory);
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        Assert.Null(await db.Users.FindAsync(user.Id));
        Assert.False(await db.AlertSubscriptions.AnyAsync(subscription => subscription.Id == subscriptionId));
        Assert.False(await db.NotificationTargets.AnyAsync(target => target.Id == targetId));
    }

    [Fact]
    public async Task Worker_SkipsSubscriptionsOfDeactivatedUsers()
    {
        await using var factory = new TestWebApplicationFactory();
        var username = await CreateUserAsync(factory);
        var user = await UserAsync(factory, username);
        var subscriptionId = await AddSubscriptionAsync(factory, user.Id, null);
        var store = new SqlAlertStore(factory.Services.GetRequiredService<IDbContextFactory<AlertDbContext>>());
        Assert.Contains((await store.GetEnabledSubscriptionsAsync(CancellationToken.None)), subscription => subscription.Id == subscriptionId);

        await DeactivateAsync(factory, username);

        var subscriptions = await store.GetEnabledSubscriptionsAsync(CancellationToken.None);
        Assert.DoesNotContain(subscriptions, subscription => subscription.Id == subscriptionId);
    }

    [Fact]
    public async Task Source_InUseBySubscription_CannotBeDeleted()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = Scope(factory);
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        var source = new NewsSource { Name = "In use", Url = $"https://{Guid.NewGuid():N}.example/rss" };
        db.NewsSources.Add(source);
        await db.SaveChangesAsync();
        db.SubscriptionSources.Add(new SubscriptionSource { NewsSourceId = source.Id, AlertSubscriptionId = await AddSubscriptionAsync(factory, await AdminIdAsync(factory), null) });
        await db.SaveChangesAsync();

        var deleted = await scope.ServiceProvider.GetRequiredService<NewsSourceService>().DeleteAsync(source, CancellationToken.None);

        Assert.False(deleted);
    }

    [Fact]
    public async Task Source_DuplicateUrl_IsRejected()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = Scope(factory);
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        var url = $"https://{Guid.NewGuid():N}.example/rss";
        db.NewsSources.Add(new NewsSource { Name = "Existing", Url = url });
        await db.SaveChangesAsync();

        var errors = await scope.ServiceProvider.GetRequiredService<NewsSourceService>().ValidateAsync(url, null, CancellationToken.None);

        Assert.Contains(errors, error => error.Message == "This feed is already a source.");
    }

    private static IServiceScope Scope(TestWebApplicationFactory factory) => factory.Services.CreateScope();

    // Signs up through the real page. The new account is signed in and has the User role.
    private static async Task<string> SignUpAsync(HttpClient client, TestWebApplicationFactory factory)
    {
        var username = $"adm{Guid.NewGuid():N}"[..20];
        var response = await WebForms.PostFormAsync(client, "/Identity/Account/Register",
            WebForms.SignUpFields(username, $"{username}@example.com"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return username;
    }

    private static async Task<string> CreateUserAsync(TestWebApplicationFactory factory)
    {
        var username = $"usr{Guid.NewGuid():N}"[..20];
        var users = Scope(factory).ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await users.CreateAsync(new ApplicationUser { UserName = username, Email = $"{username}@example.com" }, WebForms.Password);
        Assert.True(result.Succeeded);
        await users.AddToRoleAsync(await users.FindByNameAsync(username) ?? throw new InvalidOperationException(), IdentitySeeder.UserRole);
        return username;
    }

    private static async Task<ApplicationUser> UserAsync(TestWebApplicationFactory factory, string username) =>
        await Scope(factory).ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(username)
        ?? throw new InvalidOperationException($"No user {username}.");

    private static async Task<ApplicationUser> AdminAsync(TestWebApplicationFactory factory) =>
        await UserAsync(factory, "admin");

    private static async Task<int> AdminIdAsync(TestWebApplicationFactory factory) => (await AdminAsync(factory)).Id;

    private static async Task DeactivateAsync(TestWebApplicationFactory factory, string username)
    {
        var user = await UserAsync(factory, username);
        var admin = await AdminAsync(factory);
        var error = await Scope(factory).ServiceProvider.GetRequiredService<UserAdministrationService>()
            .SetActiveAsync(user.Id, admin.Id, false, CancellationToken.None);
        Assert.Null(error);
    }

    private static async Task<int> AddTargetAsync(TestWebApplicationFactory factory, int ownerId)
    {
        using var scope = Scope(factory);
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        var target = new NotificationTarget { OwnerId = ownerId, Name = "Mailbox", Kind = ChannelKind.Email, Destination = $"{Guid.NewGuid():N}@example.com", IsVerified = true };
        db.NotificationTargets.Add(target);
        await db.SaveChangesAsync();
        return target.Id;
    }

    private static async Task<int> AddSubscriptionAsync(TestWebApplicationFactory factory, int ownerId, int? targetId)
    {
        using var scope = Scope(factory);
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        var source = await db.NewsSources.FirstOrDefaultAsync(entity => entity.Url == "https://feeds.bbci.co.uk/news/rss.xml")
            ?? new NewsSource { Name = "BBC News", Url = "https://feeds.bbci.co.uk/news/rss.xml" };
        var subscription = new AlertSubscription { OwnerId = ownerId, Name = $"Sub {Guid.NewGuid():N}"[..20], FrequencyMinutes = 1, CreatedAt = DateTimeOffset.UtcNow, LastAlertAt = DateTimeOffset.UtcNow };
        subscription.Sources.Add(new SubscriptionSource { NewsSource = source });
        subscription.Keywords.Add(new SubscriptionKeyword { Kind = KeywordKind.Include, Term = "election" });
        if (targetId is int id)
        {
            subscription.Targets.Add(new SubscriptionTarget { NotificationTargetId = id });
        }

        db.AlertSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    // The admin password is read from configuration (user secrets), never written into the test.
    private static async Task LogInAsAdminAsync(HttpClient client, TestWebApplicationFactory factory)
    {
        var password = factory.Services.GetRequiredService<IConfiguration>()["Admin:Password"];
        Assert.False(string.IsNullOrEmpty(password), "Admin:Password is not configured.");
        var response = await WebForms.PostFormAsync(client, "/Identity/Account/Login", new()
        {
            ["Input.Username"] = "admin",
            ["Input.Password"] = password,
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
