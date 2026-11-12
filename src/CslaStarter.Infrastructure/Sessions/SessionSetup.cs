using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CslaStarter.Infrastructure.Sessions;

public static class SessionSetup
{
    public static IServiceCollection AddCslaStarterSessions(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<SessionOptions>(configuration.GetSection("Session"));
        services.AddScoped<SessionTimeoutMonitor>();
        services.AddScoped<CircuitHandler, SessionActivityCircuitHandler>();
        return services;
    }
}
