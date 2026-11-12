using CslaStarter.Core.Security;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Data.Security;

/// <summary>
/// Writes security events to the append-only security_event table and to the log (for SIEM forwarding). Uses its
/// own short-lived context so it never saves a caller's unrelated pending changes.
/// </summary>
public sealed partial class SecurityEventLog(AppDatabaseConnection connection, TimeProvider time, ILogger<SecurityEventLog> logger) : ISecurityEventLog
{
    private const int MaxDetailLength = 2000;
    private const int MaxUserNameLength = 200;

    public async Task WriteAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(connection.ConnectionString);
        await using var db = new AppDbContext(options.Options);
        db.SecurityEvents.Add(new SecurityEventRow
        {
            Kind = (int)securityEvent.Kind,
            OccurredAt = time.GetUtcNow(),
            UserId = securityEvent.UserId,
            UserName = securityEvent.UserName is { Length: > MaxUserNameLength } name ? name[..MaxUserNameLength] : securityEvent.UserName,
            Detail = securityEvent.Detail.Length > MaxDetailLength ? securityEvent.Detail[..MaxDetailLength] : securityEvent.Detail,
            CorrelationId = securityEvent.CorrelationId,
        });
        await db.SaveChangesAsync(cancellationToken);

        LogSecurityEvent(logger, securityEvent.Kind, securityEvent.UserId, securityEvent.CorrelationId, securityEvent.Detail);
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Security event {Kind} for user {UserId} (correlation {CorrelationId}): {Detail}")]
    private static partial void LogSecurityEvent(ILogger logger, SecurityEventKind kind, Guid? userId, string? correlationId, string detail);
}
