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
        // Activity is reported by the session-timeout component from real user input (keyboard, pointer), not from
        // circuit traffic: render acknowledgements of server-pushed updates must not keep a session alive.
        return services;
    }
}
