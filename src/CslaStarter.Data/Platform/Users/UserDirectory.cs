using CslaStarter.Domain.Platform.Users;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Platform.Users;

/// <summary>
/// The sign-in directory, a platform store: each call uses its own short-lived context, outside any business
/// transaction. The insert skips an existing account, so racing first sign-ins all read the one row that won.
/// </summary>
public sealed class UserDirectory(IDbContextFactory<AppDbContext> contexts) : IUserDirectory
{
    public async Task<SignedInUser> EnsureAsync(string tenantId, string objectId, string displayName, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into app_user (id, tenant_id, object_id, display_name, is_active)
            values ({Guid.CreateVersion7()}, {tenantId}, {objectId}, {displayName}, true)
            on conflict (tenant_id, object_id) do nothing
            """,
            ct);

        return await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.ObjectId == objectId)
            .Select(u => new SignedInUser(u.Id, u.DisplayName, u.IsActive))
            .SingleAsync(ct);
    }
}
