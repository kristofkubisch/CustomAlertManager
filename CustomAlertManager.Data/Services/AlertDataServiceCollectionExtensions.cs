using CustomAlertManager.Scheduler.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomAlertManager.Data.Services;

public static class AlertDataServiceCollectionExtensions
{
    public static IServiceCollection AddAlertData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<AlertDbContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton<IAlertStore, SqlAlertStore>();
        services.AddSingleton<IRunLock>(new SqlRunLock(connectionString));
        return services;
    }
}
