using CslaStarter.Core.Auditing;
using CslaStarter.Core.Security;
using CslaStarter.Dal.Types;
using CslaStarter.Data.Auditing;
using CslaStarter.Data.Dal;
using CslaStarter.Data.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CslaStarter.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> on PostgreSQL with snake_case names, audit capture and the history
    /// reader. The host must register an <see cref="Core.Security.IDataAccessContext"/> (who is making changes).
    /// </summary>
    public static IServiceCollection AddCslaStarterData(
        this IServiceCollection services, string connectionString, Action<NpgsqlDataSourceBuilder>? configureDataSource = null)
    {
        var database = AppDatabase.Create(connectionString, configureDataSource);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(database);
        services.AddSingleton(database.DataSource);
        services.AddSingleton<ISecurityEventLog, SecurityEventLog>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditHistoryReader, AuditHistoryReader>();
        services.AddScoped<ITypeDal, TypeDal>();
        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseAppDatabase(database.DataSource)
            .AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>()));
        return services;
    }
}
