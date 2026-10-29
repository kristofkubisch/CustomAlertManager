using CustomAlertManager.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CustomAlertManager.Web.Services;

public sealed class DatabaseHealthCheck(IDbContextFactory<AlertDbContext> contextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("The alert database is reachable.")
                : HealthCheckResult.Unhealthy("The alert database cannot be reached.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("The alert database check failed.", ex);
        }
    }
}
