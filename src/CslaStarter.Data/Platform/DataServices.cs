using CslaStarter.Data.Platform.Texts;
using CslaStarter.Data.Platform.Users;
using CslaStarter.Domain.Platform.Texts;
using CslaStarter.Domain.Platform.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CslaStarter.Data.Platform;

public static class DataServices
{
    /// <summary>
    /// Registers the database and its stores: the <see cref="AppDbContext"/> factory; the platform stores, which depend
    /// only on that factory and are singletons (the translator and hosted services hold them); then every other class
    /// in this assembly that implements an <c>I…Dal</c> interface, as that interface and scoped, found by a scan (a new
    /// DAL needs no registration line). The scan skips an interface that is already registered.
    /// </summary>
    public static IServiceCollection AddAppData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<AppDbContext>(options => options.UseAppDatabase(connectionString));

        services.AddSingleton<ITextDal, TextDal>();
        services.AddSingleton<IUserDirectory, UserDirectory>();
        services.AddSingleton<TextSeeder>();

        foreach (var (contract, implementation) in Dals())
        {
            services.TryAddScoped(contract, implementation);
        }

        return services;
    }

    /// <summary>The one place the provider and the snake_case naming are set, for the app and for <c>dotnet ef</c>.</summary>
    internal static DbContextOptionsBuilder UseAppDatabase(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

    private static IEnumerable<(Type Contract, Type Implementation)> Dals() =>
        from type in typeof(DataServices).Assembly.GetTypes()
        where type is { IsClass: true, IsAbstract: false }
        from contract in type.GetInterfaces()
        where contract.Name.StartsWith('I') && contract.Name.EndsWith("Dal", StringComparison.Ordinal)
        select (contract, type);
}
