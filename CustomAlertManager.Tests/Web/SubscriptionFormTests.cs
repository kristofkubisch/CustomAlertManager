using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CustomAlertManager.Tests.Web;

[Collection("WebApp")]
public class SubscriptionFormTests
{
    [Fact]
    public async Task CreateSubscription_WithEmptyExcludeKeywords_IsAccepted()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var username = $"sub{Guid.NewGuid():N}"[..20];
        await WebForms.PostFormAsync(client, "/Identity/Account/Register",
            WebForms.SignUpFields(username, $"{username}@example.com"));
        var sourceId = await WebForms.FirstCheckboxValueAsync(client, "/Subscriptions/Create", "SourceIds");

        var response = await WebForms.PostFormAsync(client, "/Subscriptions/Create", new()
        {
            ["Name"] = "Election watch",
            ["FrequencyMinutes"] = "15",
            ["SourceIds"] = sourceId,
            ["IncludeKeywords"] = "election",
            ["ExcludeKeywords"] = string.Empty,
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("field is required", body);
    }
}
