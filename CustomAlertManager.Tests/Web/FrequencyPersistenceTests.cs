using System.Net;
using CustomAlertManager.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomAlertManager.Tests.Web;

[Collection("WebApp")]
public class FrequencyPersistenceTests
{
    [Theory]
    [InlineData("15")]
    [InlineData("60")]
    public async Task CreateSubscription_SavesChosenFrequency(string frequency)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var username = $"freq{Guid.NewGuid():N}"[..20];
        await WebForms.PostFormAsync(client, "/Identity/Account/Register", WebForms.SignUpFields(username, $"{username}@example.com"));
        var sourceId = await WebForms.FirstCheckboxValueAsync(client, "/Subscriptions/Create", "SourceIds");
        var name = $"Freq {frequency} {Guid.NewGuid():N}"[..30];

        var response = await WebForms.PostFormAsync(client, "/Subscriptions/Create", new()
        {
            ["Name"] = name,
            ["FrequencyMinutes"] = frequency,
            ["SourceIds"] = sourceId,
            ["IncludeKeywords"] = "election",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        Assert.Equal(int.Parse(frequency), (await db.AlertSubscriptions.SingleAsync(s => s.Name == name)).FrequencyMinutes);
    }

    [Fact]
    public async Task EditSubscription_SavesChangedFrequency()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var username = $"edit{Guid.NewGuid():N}"[..20];
        await WebForms.PostFormAsync(client, "/Identity/Account/Register", WebForms.SignUpFields(username, $"{username}@example.com"));
        var sourceId = await WebForms.FirstCheckboxValueAsync(client, "/Subscriptions/Create", "SourceIds");
        var name = $"Edit {Guid.NewGuid():N}"[..30];
        await WebForms.PostFormAsync(client, "/Subscriptions/Create", new()
        {
            ["Name"] = name,
            ["FrequencyMinutes"] = "1",
            ["SourceIds"] = sourceId,
            ["IncludeKeywords"] = "election",
        });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
        var id = (await db.AlertSubscriptions.SingleAsync(s => s.Name == name)).Id;

        var response = await WebForms.PostFormAsync(client, $"/Subscriptions/Edit/{id}", new()
        {
            ["Name"] = name,
            ["FrequencyMinutes"] = "60",
            ["SourceIds"] = sourceId,
            ["IncludeKeywords"] = "election",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await db.AlertSubscriptions.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Equal(60, saved.FrequencyMinutes);
    }
}
