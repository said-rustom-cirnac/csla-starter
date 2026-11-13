using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Infrastructure.Health;

/// <summary>/health/live (the process is up) and /health/ready (database reachable and fully migrated). Anonymous.</summary>
public static class HealthSetup
{
    /// <summary>Tag of the checks behind /health/ready.</summary>
    public const string Ready = "ready";

    /// <summary>Uses the app's shared NpgsqlDataSource (registered by AddCslaStarterData).</summary>
    public static IServiceCollection AddCslaStarterHealth(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddNpgSql(name: "database", tags: [Ready])
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
