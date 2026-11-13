using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Security;
using CslaStarter.Data.Access;
using CslaStarter.Data.Auditing;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CslaStarter.Data.Realtime;

/// <summary>
/// Turns each save into one notice per aggregate root it touched (a child row counts as an update of its root), plus
/// an access-changed notice for every user whose grants changed (<see cref="AccessGrantAttribute"/>), and publishes
/// them after commit: as soon as SaveChanges completes, or when the surrounding transaction commits. A failed save or
/// a rolled-back transaction publishes nothing, and a publishing failure never fails the save: notices are hints.
/// </summary>
public sealed partial class ChangePublishingInterceptor(
    IDataAccessContext access, ILogger<ChangePublishingInterceptor> logger, IEntityChangeNotifier? notifier = null)
    : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    private static readonly ConcurrentDictionary<Type, AccessGrantAttribute?> Grants = new();

    // What the save in progress touched, per context. Replaced by every save, so a failed save never leaks.
    private readonly ConditionalWeakTable<DbContext, Touched> _saving = new();

    // Saved inside an explicit transaction: waits for that transaction to commit.
    private readonly ConditionalWeakTable<DbContext, Committing> _committing = new();

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        _ = PublishAsync(Saved(eventData.Context));
        return result;
    }

    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await PublishAsync(Saved(eventData.Context)).ConfigureAwait(false);
        return result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData) => Forget(eventData.Context);

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context);
        return Task.CompletedTask;
    }

    public void SaveChangesCanceled(DbContextEventData eventData) => Forget(eventData.Context);

    public Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context);
        return Task.CompletedTask;
    }

    public DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        TakeCommitting(eventData.Context, null);
        return result;
    }

    public ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        TakeCommitting(eventData.Context, null);
        return ValueTask.FromResult(result);
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        _ = PublishAsync(TakeCommitting(eventData.Context, eventData.TransactionId));

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
        PublishAsync(TakeCommitting(eventData.Context, eventData.TransactionId));

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => TakeCommitting(eventData.Context, null);

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TakeCommitting(eventData.Context, null);
        return Task.CompletedTask;
    }

    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => TakeCommitting(eventData.Context, null);

    public Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        TakeCommitting(eventData.Context, null);
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null || notifier is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var touched = new Touched();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is (EntityState.Added or EntityState.Modified or EntityState.Deleted) && !EntityNaming.IsIgnored(entry.Metadata.ClrType))
            {
                touched.AddRoot(entry);
                touched.AddAccess(entry);
            }
        }

        _saving.AddOrUpdate(context, touched);
    }

    private List<EntityChange> Saved(DbContext? context)
    {
        if (context is null || !_saving.TryGetValue(context, out var touched))
        {
            return [];
        }

        _saving.Remove(context);
        var changes = touched.ToChanges(access);
        if (context.Database.CurrentTransaction is not { } transaction)
        {
            return changes;
        }

        var committing = _committing.GetValue(context, _ => new Committing(transaction.TransactionId));
        if (committing.TransactionId != transaction.TransactionId)
        {
            _committing.AddOrUpdate(context, committing = new Committing(transaction.TransactionId));
        }

        committing.Add(changes);
        return [];
    }

    private void Forget(DbContext? context)
    {
        if (context is not null)
        {
            _saving.Remove(context);
        }
    }

    /// <summary>Removes what waits for a transaction; returns it only when <paramref name="committed"/> is that transaction.</summary>
    private List<EntityChange> TakeCommitting(DbContext? context, Guid? committed)
    {
        if (context is null || !_committing.TryGetValue(context, out var committing))
        {
            return [];
        }

        _committing.Remove(context);
        return committing.TransactionId == committed ? committing.Changes : [];
    }

    private async Task PublishAsync(List<EntityChange> changes)
    {
        if (changes.Count == 0 || notifier is null)
        {
            return;
        }

        try
        {
            await notifier.PublishAsync(changes, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The change is committed; a lost hint must never turn into a failed save.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogPublishFailed(ex, changes.Count);
        }
    }

    private static AccessGrantAttribute? GrantOf(Type type) => Grants.GetOrAdd(type, t => t.GetCustomAttribute<AccessGrantAttribute>());

    [LoggerMessage(EventId = 1100, Level = LogLevel.Warning, Message = "Publishing {Count} change notices failed; open screens may show stale data until they refresh.")]
    private partial void LogPublishFailed(Exception exception, int count);

    private sealed class Touched
    {
        private readonly Dictionary<(string Type, string Key), (ChangeKind Kind, object? Entity)> _roots = [];
        private readonly HashSet<Guid> _users = [];

        public void AddRoot(EntityEntry entry)
        {
            if (entry.Entity is IHasAuditRoot child)
            {
                Merge((child.AuditRootType, child.AuditRootKey), ChangeKind.Updated, null);
                return;
            }

            var kind = entry.State switch
            {
                EntityState.Added => ChangeKind.Inserted,
                EntityState.Deleted => ChangeKind.Deleted,
                _ => ChangeKind.Updated,
            };
            Merge((EntityNaming.TypeName(entry.Metadata.ClrType), EntityNaming.Key(entry)), kind, entry.Entity);
        }

        public void AddAccess(EntityEntry entry)
        {
            if (GrantOf(entry.Metadata.ClrType) is not { } grant)
            {
                return;
            }

            var changed = entry.State is EntityState.Added or EntityState.Deleted
                || grant.WatchedProperties.Any(name => entry.Property(name) is { IsModified: true } p && !Equals(p.OriginalValue, p.CurrentValue));
            if (changed && entry.Property(grant.UserIdProperty).CurrentValue is Guid userId)
            {
                _users.Add(userId);
            }
        }

        /// <summary>Called after the save, when the roots' new versions have been read back.</summary>
        public List<EntityChange> ToChanges(IDataAccessContext access)
        {
            var changes = _roots
                .Select(r => new EntityChange(
                    r.Key.Type,
                    r.Key.Key,
                    r.Value.Kind,
                    r.Value.Kind != ChangeKind.Deleted && r.Value.Entity is IAggregateRoot root ? root.Version : null,
                    access.UserId,
                    access.UserName,
                    access.OriginId))
                .ToList();
            changes.AddRange(_users.Select(id => EntityChange.UserAccess(id, access.UserId, access.UserName, access.OriginId)));
            return changes;
        }

        // The root's own insert or delete wins over "a child changed"; the root's tracked entity gives the version.
        private void Merge((string Type, string Key) root, ChangeKind kind, object? entity)
        {
            if (_roots.TryGetValue(root, out var existing))
            {
                kind = existing.Kind is ChangeKind.Inserted or ChangeKind.Deleted ? existing.Kind : kind;
                entity ??= existing.Entity;
            }

            _roots[root] = (kind, entity);
        }
    }

    private sealed class Committing(Guid transactionId)
    {
        private readonly Dictionary<(string, string), EntityChange> _changes = [];

        public Guid TransactionId { get; } = transactionId;

        public List<EntityChange> Changes => [.. _changes.Values];

        // Several saves in one transaction: one notice per record, insert kept, delete final, latest version.
        public void Add(List<EntityChange> changes)
        {
            foreach (var change in changes)
            {
                var key = (change.EntityType, change.EntityKey);
                _changes[key] = _changes.TryGetValue(key, out var first) && first.Kind == ChangeKind.Inserted && change.Kind == ChangeKind.Updated
                    ? change with { Kind = ChangeKind.Inserted }
                    : change;
            }
        }
    }
}
