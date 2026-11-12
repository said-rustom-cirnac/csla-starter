using CslaStarter.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CslaStarter.Infrastructure.Health;

/// <summary>Not ready while the database is missing migrations this build expects.</summary>
public sealed class MigrationsHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        return pending.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"Pending migrations: {string.Join(", ", pending)}");
    }
}
