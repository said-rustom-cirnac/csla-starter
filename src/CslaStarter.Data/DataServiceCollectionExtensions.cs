using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CslaStarter.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers <see cref="AppDbContext"/> on PostgreSQL with snake_case names.</summary>
    public static IServiceCollection AddCslaStarterData(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(options => options.UseAppDatabase(connectionString));
        return services;
    }
}
