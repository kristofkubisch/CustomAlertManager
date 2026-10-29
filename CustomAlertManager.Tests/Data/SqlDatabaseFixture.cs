using CustomAlertManager.Data;
using CustomAlertManager.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomAlertManager.Tests.Data;

// Recreates a dedicated test database on LocalDB once per test class, then applies the migrations.
public sealed class SqlDatabaseFixture : IAsyncLifetime
{
    public const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=CustomAlertManager_Tests;Trusted_Connection=True;TrustServerCertificate=True";

    private ServiceProvider _services = null!;

    public IDbContextFactory<AlertDbContext> ContextFactory =>
        _services.GetRequiredService<IDbContextFactory<AlertDbContext>>();

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddAlertData(ConnectionString);
        _services = services.BuildServiceProvider();

        await using var db = await ContextFactory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
    }
}
