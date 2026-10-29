using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CustomAlertManager.Data;

// Used by the EF Core design-time tools (dotnet ef) to create migrations.
public sealed class AlertDbContextFactory : IDesignTimeDbContextFactory<AlertDbContext>
{
    public AlertDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AlertDbContext>()
            .UseSqlServer(AlertDbContext.DefaultConnectionString)
            .Options);
}
