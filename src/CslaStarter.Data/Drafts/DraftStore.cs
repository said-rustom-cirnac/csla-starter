using System.Security.Cryptography;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Security;
using CslaStarter.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CslaStarter.Data.Drafts;

/// <summary>
/// Drafts in the <c>draft</c> table, encrypted with Data Protection under a purpose that includes the user id (so a
/// payload copied to another user's row doesn't decrypt). Only the signed-in user's own drafts are reachable. Each
/// call uses its own short-lived context, so components of one circuit can save drafts at the same time.
/// </summary>
public sealed partial class DraftStore(
    AppDatabase database, IDataAccessContext access, IDataProtectionProvider protection, TimeProvider time, ILogger<DraftStore> logger) : IDraftStore
{
    public const string Purpose = "CslaStarter.Drafts.v1";

    private const int CleanupBatch = 500;

    private static readonly TimeSpan KeepFor = TimeSpan.FromHours(8);

    public async Task SaveAsync(DraftKey key, uint? version, byte[] payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        EnsureOwn(key);
        var now = time.GetUtcNow();
        var encrypted = Protector(key.UserId).Protect(payload);

        try
        {
            await UpsertAsync(key, row => (row.Version, row.Payload, row.SavedAt, row.ExpiresAt) = (version, encrypted, now, now + KeepFor), cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The same user's other tab inserted first; this, the later draft, replaces it.
            await UpsertAsync(key, row => (row.Version, row.Payload, row.SavedAt, row.ExpiresAt) = (version, encrypted, now, now + KeepFor), cancellationToken);
        }
    }

    public async Task<Draft?> GetAsync(DraftKey key, CancellationToken cancellationToken)
    {
        EnsureOwn(key);
        await using var db = database.CreateContext();
        var row = await db.Drafts.AsNoTracking().SingleOrDefaultAsync(
            d => d.UserId == key.UserId && d.EntityType == key.EntityType && d.EntityKey == key.EntityKey && d.ExpiresAt > time.GetUtcNow(),
            cancellationToken);
        if (row is null)
        {
            return null;
        }

        try
        {
            return new Draft(row.Version is { } v ? (uint)v : null, Protector(key.UserId).Unprotect(row.Payload), row.SavedAt);
        }
        catch (CryptographicException e)
        {
            // Tampered with, copied from another user, or its key retired: unusable, so treat it as missing.
            LogUnreadable(e, key.EntityType, key.EntityKey);
            return null;
        }
    }

    public async Task DeleteAsync(DraftKey key, CancellationToken cancellationToken)
    {
        EnsureOwn(key);
        await using var db = database.CreateContext();
        if (await FindAsync(db, key, cancellationToken) is { } row)
        {
            db.Drafts.Remove(row);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Deletes expired drafts in batches; returns how many. Run by the hourly cleanup service.</summary>
    public static async Task<int> DeleteExpiredAsync(AppDatabase database, TimeProvider time, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(time);
        var deleted = 0;
        while (true)
        {
            await using var db = database.CreateContext();
            var now = time.GetUtcNow();
            var expired = await db.Drafts.Where(d => d.ExpiresAt <= now).OrderBy(d => d.ExpiresAt).Take(CleanupBatch).ToListAsync(cancellationToken);
            db.Drafts.RemoveRange(expired);
            await db.SaveChangesAsync(cancellationToken);
            deleted += expired.Count;
            if (expired.Count < CleanupBatch)
            {
                return deleted;
            }
        }
    }

    private async Task UpsertAsync(DraftKey key, Action<DraftRow> update, CancellationToken cancellationToken)
    {
        await using var db = database.CreateContext();
        var row = await FindAsync(db, key, cancellationToken);
        if (row is null)
        {
            row = new DraftRow { UserId = key.UserId, EntityType = key.EntityType, EntityKey = key.EntityKey };
            db.Drafts.Add(row);
        }

        update(row);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Task<DraftRow?> FindAsync(AppDbContext db, DraftKey key, CancellationToken cancellationToken) =>
        db.Drafts.SingleOrDefaultAsync(d => d.UserId == key.UserId && d.EntityType == key.EntityType && d.EntityKey == key.EntityKey, cancellationToken);

    private void EnsureOwn(DraftKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (access.UserId is not { } me || me != key.UserId)
        {
            throw new UnauthorizedAccessException("A draft can only be reached by the user who wrote it.");
        }
    }

    private IDataProtector Protector(Guid userId) => protection.CreateProtector(Purpose, userId.ToString());

    [LoggerMessage(EventId = 1300, Level = LogLevel.Warning, Message = "A draft of {EntityType} {EntityKey} could not be decrypted and is ignored.")]
    private partial void LogUnreadable(Exception exception, string entityType, string entityKey);
}
