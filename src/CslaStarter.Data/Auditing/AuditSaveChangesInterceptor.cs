using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using CslaStarter.Core.Security;
using CslaStarter.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CslaStarter.Data.Auditing;

/// <summary>
/// Stamps created/modified columns and records every insert, update and delete, with old and new values, in the
/// same SaveChanges call (and therefore the same transaction) as the change itself.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IDataAccessContext access, TimeProvider time) : SaveChangesInterceptor
{
    private static readonly HashSet<string> TechnicalColumns =
        [nameof(IAggregateRoot.Version), nameof(IAuditedEntity.CreatedOn), nameof(IAuditedEntity.CreatedBy), nameof(IAuditedEntity.ModifiedOn), nameof(IAuditedEntity.ModifiedBy)];

    private static readonly HashSet<Type> NeverAudited =
        [typeof(AuditEventRow), typeof(AuditChangeRow), typeof(SecurityEventRow), typeof(DataProtectionKey)];

    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Properties = new();

    // Audit rows added by a save that hasn't completed yet, per context. If the save fails, the next save discards them.
    private readonly ConditionalWeakTable<DbContext, List<AuditEventRow>> _pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Forget(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // A previous save on this context failed (concurrency, constraint...) after adding its audit rows: those rows
        // describe a change that never happened, so they must not ride along with this save.
        DiscardUnsaved(context);

        context.ChangeTracker.DetectChanges();
        var now = time.GetUtcNow();
        var events = new List<AuditEventRow>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted) || IsNeverAudited(entry.Metadata.ClrType))
            {
                continue;
            }

            var changes = Changes(entry);
            Stamp(entry, now);

            // A row marked modified with no audited change (a root "touched" for the aggregate's version check, or an
            // [AuditIgnore]-only change) is still saved and stamped, but adds nothing to the history.
            if (entry.State != EntityState.Modified || changes.Count > 0)
            {
                events.Add(Event(entry, changes, now));
            }
        }

        context.AddRange(events);
        _pending.AddOrUpdate(context, events);
    }

    private void Forget(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }

    private void DiscardUnsaved(DbContext context)
    {
        if (!_pending.TryGetValue(context, out var events))
        {
            return;
        }

        foreach (var auditEvent in events)
        {
            foreach (var change in auditEvent.Changes.ToList())
            {
                context.Entry(change).State = EntityState.Detached;
            }

            context.Entry(auditEvent).State = EntityState.Detached;
        }

        _pending.Remove(context);
    }

    private void Stamp(EntityEntry entry, DateTimeOffset now)
    {
        if (entry.Entity is not IAuditedEntity audited || entry.State == EntityState.Deleted)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            audited.CreatedOn = now;
            audited.CreatedBy = access.UserName;
        }
        else
        {
            entry.Property(nameof(IAuditedEntity.CreatedOn)).IsModified = false;
            entry.Property(nameof(IAuditedEntity.CreatedBy)).IsModified = false;
        }

        audited.ModifiedOn = now;
        audited.ModifiedBy = access.UserName;
    }

    private AuditEventRow Event(EntityEntry entry, List<AuditChangeRow> changes, DateTimeOffset now)
    {
        var type = entry.Metadata.ClrType;
        var entityType = type.GetCustomAttribute<AuditEntityAttribute>()?.Name ?? type.Name;
        var entityKey = string.Join('|', entry.Metadata.FindPrimaryKey()!.Properties.Select(p => Format(entry.Property(p.Name).CurrentValue)));
        var root = entry.Entity as IHasAuditRoot;

        var auditEvent = new AuditEventRow
        {
            OccurredAt = now,
            UserId = access.UserId,
            UserName = access.UserName,
            CorrelationId = access.CorrelationId,
            EntityType = entityType,
            EntityKey = entityKey,
            RootType = root?.AuditRootType ?? entityType,
            RootKey = root?.AuditRootKey ?? entityKey,
            Action = entry.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Deleted => "Delete",
                _ => "Update",
            },
        };

        foreach (var change in changes)
        {
            auditEvent.Changes.Add(change);
        }

        return auditEvent;
    }

    private static List<AuditChangeRow> Changes(EntityEntry entry)
    {
        var changes = new List<AuditChangeRow>();
        var surrogateKey = entry.Metadata.FindPrimaryKey()!.Properties is [var single] ? single.Name : null;

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            var clrProperty = ClrProperty(entry.Metadata.ClrType, name);
            if (TechnicalColumns.Contains(name) || name == surrogateKey || clrProperty?.GetCustomAttribute<AuditIgnoreAttribute>() is not null)
            {
                continue;
            }

            var (oldValue, newValue) = entry.State switch
            {
                EntityState.Added => (null, property.CurrentValue),
                EntityState.Deleted => (property.OriginalValue, null),
                _ when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue) => (property.OriginalValue, property.CurrentValue),
                _ => (Unchanged, Unchanged),
            };

            if (ReferenceEquals(oldValue, Unchanged) || (oldValue is null && newValue is null))
            {
                continue;
            }

            var masked = clrProperty?.GetCustomAttribute<AuditMaskAttribute>() is not null;
            changes.Add(new AuditChangeRow
            {
                Property = name,
                OldValue = masked ? null : Format(oldValue),
                NewValue = masked ? null : Format(newValue),
                IsMasked = masked,
                TypeCodeOf = clrProperty?.GetCustomAttribute<AuditTypeCodeAttribute>()?.TypeName,
            });
        }

        return changes;
    }

    private static readonly object Unchanged = new();

    private static bool IsNeverAudited(Type type) =>
        NeverAudited.Contains(type) || type.GetCustomAttribute<AuditIgnoreAttribute>() is not null;

    private static PropertyInfo? ClrProperty(Type type, string name) =>
        Properties.GetOrAdd((type, name), key => key.Item1.GetProperty(key.Item2));

    private static string? Format(object? value) => value switch
    {
        null => null,
        DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
