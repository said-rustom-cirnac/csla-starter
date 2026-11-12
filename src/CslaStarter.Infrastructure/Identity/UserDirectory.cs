using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Data.Auditing;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace CslaStarter.Infrastructure.Identity;

public sealed record DirectoryUser(Guid Id, bool IsActive, IReadOnlySet<int> RoleCodes, string PreferredLanguage);

/// <summary>Finds (or, on first sign-in, creates with no roles) the app user behind an Entra identity.</summary>
public interface IUserDirectory
{
    Task<DirectoryUser> GetOrProvisionAsync(string tenantId, string objectId, string displayName, string? email, CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user as the database has it right now, bypassing the cache; null when they no longer exist.</summary>
    Task<DirectoryUser?> GetFreshAsync(Guid userId, CancellationToken cancellationToken);

    void Invalidate(string tenantId, string objectId);
}

public sealed class UserDirectory(AppDatabase database, IMemoryCache cache, TimeProvider time, ISecurityEventLog securityLog) : IUserDirectory
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    public async Task<DirectoryUser> GetOrProvisionAsync(string tenantId, string objectId, string displayName, string? email, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key(tenantId, objectId), out DirectoryUser? cached) && cached is not null)
        {
            return cached;
        }

        var user = await FindAsync(tenantId, objectId, cancellationToken)
            ?? await ProvisionAsync(tenantId, objectId, displayName, email, cancellationToken);

        var result = ToDirectoryUser(user);
        cache.Set(Key(tenantId, objectId), result, CacheFor);
        return result;
    }

    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var db = CreateContext("revalidation");
        return await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken);
    }

    public async Task<DirectoryUser?> GetFreshAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var db = CreateContext("revalidation");
        var user = await db.Users.AsNoTracking().Include(u => u.Roles).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : ToDirectoryUser(user);
    }

    public void Invalidate(string tenantId, string objectId) => cache.Remove(Key(tenantId, objectId));

    private async Task<AppUser?> FindAsync(string tenantId, string objectId, CancellationToken cancellationToken)
    {
        await using var db = CreateContext("sign-in");
        return await db.Users.AsNoTracking().Include(u => u.Roles)
            .SingleOrDefaultAsync(u => u.TenantId == tenantId && u.ObjectId == objectId, cancellationToken);
    }

    private async Task<AppUser> ProvisionAsync(string tenantId, string objectId, string displayName, string? email, CancellationToken cancellationToken)
    {
        var user = new AppUser { TenantId = tenantId, ObjectId = objectId, DisplayName = Fit(displayName, 200)!, Email = Fit(email, 320) };
        try
        {
            await using var db = CreateContext("sign-in");
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two first requests raced; the other one created the user.
            return await FindAsync(tenantId, objectId, cancellationToken) ?? throw new InvalidOperationException("User vanished after a unique violation.", e);
        }

        await securityLog.WriteAsync(
            new SecurityEvent(SecurityEventKind.SignIn, user.Id, user.DisplayName, "First sign-in: account created with no roles.", null),
            cancellationToken);
        return user;
    }

    private static DirectoryUser ToDirectoryUser(AppUser user) => new(
        user.Id,
        user.IsActive,
        user.IsActive ? user.Roles.Select(r => r.RoleTypeCode).ToHashSet() : new HashSet<int>(),
        user.PreferredLanguage);

    // Entra allows longer names than our columns; a long name must never block sign-in.
    private static string? Fit(string? value, int length) => value is { Length: > 0 } && value.Length > length ? value[..length] : value;

    private AppDbContext CreateContext(string actor) =>
        database.CreateContext(new AuditSaveChangesInterceptor(new SystemDataAccessContext(actor), time));

    private static string Key(string tenantId, string objectId) => $"user:{tenantId}:{objectId}";
}
