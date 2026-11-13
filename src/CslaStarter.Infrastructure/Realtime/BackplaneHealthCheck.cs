using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CslaStarter.Infrastructure.Realtime;

/// <summary>
/// Degraded (still ready) while the backplane isn't listening: this instance serves requests correctly, but open screens
/// don't see other instances' changes until it reconnects.
/// </summary>
public sealed class BackplaneHealthCheck(PostgresChangeBackplane backplane) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(backplane.IsListening
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded("Not listening for change notices from other instances."));
}
