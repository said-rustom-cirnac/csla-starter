using CslaStarter.Domain.Platform.Users;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Platform.Users;

/// <summary>
/// The sign-in directory, a platform store: each call uses its own short-lived context, outside any business
/// transaction. The insert skips an existing account, so racing first sign-ins all read the one row that won.
/// </summary>
public sealed class UserDirectory(IDbContextFactory<AppDbContext> contexts) : IUserDirectory
{
    /// <exception cref="ArgumentException">The tenant id or object id is longer than its column.</exception>
    public async Task<SignedInUser> EnsureAsync(string tenantId, string objectId, string displayName, CancellationToken ct)
    {
        // The ids are keys: cutting one would sign the person in as someone else, so a long one is refused.
        RequireFits(tenantId, AppUser.TenantIdMaxLength, nameof(tenantId));
        RequireFits(objectId, AppUser.ObjectIdMaxLength, nameof(objectId));

        // Entra allows longer names than the column holds; a long name must never block sign-in.
        var name = Fit(displayName.Trim(), AppUser.DisplayNameMaxLength);

        await using var db = await contexts.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into app_user (id, tenant_id, object_id, display_name, is_active)
            values ({Guid.CreateVersion7()}, {tenantId}, {objectId}, {name}, true)
            on conflict (tenant_id, object_id) do nothing
            """,
            ct);

        return await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.ObjectId == objectId)
            .Select(u => new SignedInUser(u.Id, u.DisplayName, u.IsActive))
            .SingleAsync(ct);
    }

    private static void RequireFits(string value, int maxLength, string paramName)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"The value is {value.Length} characters long; at most {maxLength} are stored.", paramName);
        }
    }

    /// <summary>The first <paramref name="maxLength"/> UTF-16 units, one fewer when the cut would split a surrogate pair.</summary>
    private static string Fit(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength)];
    }
}
