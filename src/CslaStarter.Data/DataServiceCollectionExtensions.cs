using CslaStarter.Core.Auditing;
using CslaStarter.Data.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CslaStarter.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> on PostgreSQL with snake_case names, audit capture and the history
    /// reader. The host must register an <see cref="Core.Security.IDataAccessContext"/> (who is making changes).
    /// </summary>
    public static IServiceCollection AddCslaStarterData(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditHistoryReader, AuditHistoryReader>();
        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseAppDatabase(connectionString)
            .AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>()));
        return services;
    }
}
