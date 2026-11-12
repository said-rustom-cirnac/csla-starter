using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Data.Auditing;
using CslaStarter.Data.Entities;
using CslaStarter.Data.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Infrastructure.Identity;

/// <summary>
/// Creates the very first Admin, once, from the deployment pipeline (connected as app_owner). It refuses when any
/// Admin already exists, so it can never be used to grant Admin later.
/// </summary>
public sealed partial class AdminBootstrapper(AppDatabase ownerDatabase, TimeProvider time, ILoggerFactory loggerFactory)
{
    public const int Done = 0;
    public const int Refused = 2;

    private readonly ILogger _logger = loggerFactory.CreateLogger<AdminBootstrapper>();

    public async Task<int> RunAsync(string tenantId, string objectId, string displayName, CancellationToken cancellationToken)
    {
        await using var db = ownerDatabase.CreateContext(new AuditSaveChangesInterceptor(new SystemDataAccessContext("bootstrap-admin"), time));

        if (await db.UserRoles.AnyAsync(r => r.RoleTypeCode == SystemRoles.Admin, cancellationToken))
        {
            LogRefused(_logger);
            return Refused;
        }

        var user = await db.Users.Include(u => u.Roles).SingleOrDefaultAsync(u => u.TenantId == tenantId && u.ObjectId == objectId, cancellationToken);
        if (user is null)
        {
            user = new AppUser { TenantId = tenantId, ObjectId = objectId, DisplayName = displayName };
            db.Users.Add(user);
        }

        user.Roles.Add(new UserRole { RoleTypeCode = SystemRoles.Admin });
        await db.SaveChangesAsync(cancellationToken);

        var securityLog = new SecurityEventLog(ownerDatabase, time, loggerFactory.CreateLogger<SecurityEventLog>());
        await securityLog.WriteAsync(
            new SecurityEvent(SecurityEventKind.AdminBootstrapped, user.Id, user.DisplayName, $"First Admin created for tenant {tenantId}.", null),
            cancellationToken);
        LogCreated(_logger, user.Id);
        return Done;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "bootstrap-admin refused: an Admin already exists. Grant further Admins in the app.")]
    private static partial void LogRefused(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "bootstrap-admin created the first Admin, user {UserId}.")]
    private static partial void LogCreated(ILogger logger, Guid userId);
}
