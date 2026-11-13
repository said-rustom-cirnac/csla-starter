using CslaStarter.Core.Realtime;
using CslaStarter.Infrastructure.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>
/// The change hub and the notifier that carries committed changes to every instance's hub. <c>Realtime:Backplane</c>
/// is <c>Postgres</c> (default: LISTEN/NOTIFY on the app database, for any number of instances) or <c>InMemory</c> (one
/// instance, and tests).
/// </summary>
public static class RealtimeSetup
{
    public static IServiceCollection AddCslaStarterRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IEntityChangeHub, EntityChangeHub>();

        switch (configuration["Realtime:Backplane"] ?? "Postgres")
        {
            case "InMemory":
                services.AddSingleton<IEntityChangeNotifier, InMemoryChangeNotifier>();
                break;
            case "Postgres":
                services.AddSingleton<PostgresChangeBackplane>();
                services.AddSingleton<IEntityChangeNotifier>(provider => provider.GetRequiredService<PostgresChangeBackplane>());
                services.AddHostedService(provider => provider.GetRequiredService<PostgresChangeBackplane>());
                services.AddHealthChecks().AddCheck<BackplaneHealthCheck>("backplane", tags: [HealthSetup.Ready]);
                break;
            default:
                throw new InvalidOperationException("Realtime:Backplane must be Postgres or InMemory.");
        }

        return services;
    }
}
