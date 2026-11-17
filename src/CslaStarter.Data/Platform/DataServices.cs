using CslaStarter.Data.Platform.Texts;
using CslaStarter.Data.Platform.Users;
using CslaStarter.Domain.Platform.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Data.Platform;

public static class DataServices
{
    /// <summary>
    /// Registers the database and its stores: the <see cref="AppDbContext"/> factory; every class in this assembly that
    /// implements an <c>I…Dal</c> interface, as that interface and scoped (a new DAL needs no registration line); the
    /// sign-in directory; and the text seeder.
    /// </summary>
    public static IServiceCollection AddAppData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<AppDbContext>(options => options.UseAppDatabase(connectionString));

        foreach (var (contract, implementation) in Dals())
        {
            services.AddScoped(contract, implementation);
        }

        services.AddSingleton<IUserDirectory, UserDirectory>();
        services.AddSingleton<TextSeeder>();
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
