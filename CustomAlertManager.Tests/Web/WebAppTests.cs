using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using CustomAlertManager.Web;

namespace CustomAlertManager.Tests.Web;

[Collection("WebApp")]
public class WebAppTests
{
    [Fact]
    public async Task ProtectedPages_AnonymousUser_RedirectsToLogin()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var path in new[] { "/Subscriptions", "/Subscriptions/Create", "/Targets" })
        {
            var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task Home_AnonymousUser_IsAvailable()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LoginPage_IsAvailable()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/Identity/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

}
