using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CustomAlertManager.Tests.Web;

[Collection("WebApp")]
public class HardeningTests
{
    [Fact]
    public async Task Health_ReportsDatabaseHealthy()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }
}
