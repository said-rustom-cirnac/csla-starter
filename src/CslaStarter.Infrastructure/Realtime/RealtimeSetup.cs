using CslaStarter.Core.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>The change hub and the notifier that carries committed changes to every instance's hub.</summary>
public static class RealtimeSetup
{
    public static IServiceCollection AddCslaStarterRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IEntityChangeHub, EntityChangeHub>();
        services.AddSingleton<IEntityChangeNotifier, InMemoryChangeNotifier>();
        return services;
    }
}
