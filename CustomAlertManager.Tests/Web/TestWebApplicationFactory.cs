using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using CustomAlertManager.Web;

namespace CustomAlertManager.Tests.Web;

// Hosts the real web app against a dedicated LocalDB database.
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "ConnectionStrings:AlertDatabase",
            @"Server=(localdb)\MSSQLLocalDB;Database=CustomAlertManager_WebTests;Trusted_Connection=True;TrustServerCertificate=True");
    }
}
