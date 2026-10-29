using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomAlertManager.Tests.Data;

[Collection("SqlDatabase")]
public class SchemaTests(SqlDatabaseFixture fixture)
{
    [Fact]
    public async Task Schema_IdentityTables_DoNotUseAspNetPrefix()
    {
        await using var db = await fixture.ContextFactory.CreateDbContextAsync();

        var tables = await db.Database.SqlQueryRaw<string>("SELECT name AS [Value] FROM sys.tables").ToListAsync();

        Assert.Contains("Users", tables);
        Assert.Contains("Roles", tables);
        Assert.Contains("UserRoles", tables);
        Assert.DoesNotContain(tables, name => name.StartsWith("AspNet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Insert_WithoutCreatedAt_DatabaseFillsCreationTimestamp()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        int sourceId;
        await using (var db = await fixture.ContextFactory.CreateDbContextAsync())
        {
            var source = new NewsSource { Name = "Timestamp check", Url = $"https://{Guid.NewGuid():N}.example/rss" };
            db.NewsSources.Add(source);
            await db.SaveChangesAsync();
            sourceId = source.Id;
        }

        await using var readDb = await fixture.ContextFactory.CreateDbContextAsync();
        var stored = await readDb.NewsSources.AsNoTracking().SingleAsync(source => source.Id == sourceId);
        Assert.True(stored.CreatedAt > before);
        Assert.True(stored.CreatedAt < DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task IdentitySeeder_CreatesAdminWithIntegerKeyAndAdminRole()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AlertDbContext>(options => options.UseSqlServer(SqlDatabaseFixture.ConnectionString));
        services.AddIdentity<ApplicationUser, IdentityRole<int>>()
            .AddEntityFrameworkStores<AlertDbContext>();

        await using var provider = services.BuildServiceProvider();
        await IdentitySeeder.SeedAsync(
            provider.GetRequiredService<RoleManager<IdentityRole<int>>>(),
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            new AdminSeedOptions($"admin-{Guid.NewGuid():N}"[..20], email, "Admin-test-password-1"));

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(user.Id > 0);
        Assert.True(await users.IsInRoleAsync(user, IdentitySeeder.AdminRole));
    }
}
