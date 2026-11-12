using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Infrastructure.Health;

/// <summary>/health/live (the process is up) and /health/ready (database reachable and fully migrated). Anonymous.</summary>
public static class HealthSetup
{
    private const string Ready = "ready";

    public static IServiceCollection AddCslaStarterHealth(this IServiceCollection services, string connectionString)
    {
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "database", tags: [Ready])
            .AddCheck<MigrationsHealthCheck>("migrations", tags: [Ready]);
        return services;
    }

    public static IEndpointRouteBuilder MapCslaStarterHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(Ready) }).AllowAnonymous();
        return endpoints;
    }
}
