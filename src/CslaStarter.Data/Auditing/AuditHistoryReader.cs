using System.Reflection;
using CslaStarter.Core.Auditing;
using CslaStarter.Dal;
using CslaStarter.Data.Access;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Auditing;

/// <summary>
/// Reads one record's history (its own rows and its children's), newest first, without the fields the query hides. The
/// history of a <see cref="RecordScopedAttribute"/> record the user can't see is "not found", like the record itself.
/// </summary>
public sealed class AuditHistoryReader(AppDbContext db) : IAuditHistoryReader
{
    private static readonly MethodInfo IsVisibleMethod =
        typeof(AuditHistoryReader).GetMethod(nameof(IsVisibleAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public async Task<AuditPage> GetAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, 200);
        await EnsureVisibleAsync(query.EntityType, query.EntityKey, cancellationToken);

        var events = db.AuditEvents.AsNoTracking().Where(e =>
            (e.EntityType == query.EntityType && e.EntityKey == query.EntityKey)
            || (e.RootType == query.EntityType && e.RootKey == query.EntityKey));
        var hidden = query.HiddenFields?.ToList() ?? [];
        if (hidden.Count > 0)
        {
            events = events.Where(e => !e.Changes.Any() || e.Changes.Any(c => !hidden.Contains(e.EntityType + "." + c.Property)));
        }

        if (query.Property is not null)
        {
            events = events.Where(e => e.Changes.Any(c => c.Property == query.Property));
        }

        var total = await events.CountAsync(cancellationToken);
        var rows = await events
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(e => new
            {
                Event = e,
                Changes = e.Changes
                    .Where(c => (query.Property == null || c.Property == query.Property) && !hidden.Contains(e.EntityType + "." + c.Property))
                    .OrderBy(c => c.Id).ToList(),
            })
            .ToListAsync(cancellationToken);

        var entries = rows.Select(r => new AuditEntry(
                r.Event.Id, r.Event.OccurredAt, r.Event.UserId, r.Event.UserName, r.Event.Action, r.Event.EntityType, r.Event.EntityKey,
                r.Changes.Select(c => new AuditChange(c.Property, c.OldValue, c.NewValue, c.IsMasked, c.TypeCodeOf)).ToList()))
            .ToList();

        return new AuditPage(entries, total);
    }

    private async Task EnsureVisibleAsync(string entityType, string entityKey, CancellationToken cancellationToken)
    {
        var scoped = db.Model.GetEntityTypes().FirstOrDefault(t =>
            t.ClrType.GetCustomAttribute<RecordScopedAttribute>() is not null && EntityNaming.TypeName(t.ClrType) == entityType);
        if (scoped is null)
        {
            return;
        }

        var visible = Guid.TryParse(entityKey, out var id)
            && await (Task<bool>)IsVisibleMethod.MakeGenericMethod(scoped.ClrType).Invoke(this, [id, cancellationToken])!;
        if (!visible)
        {
            throw new NotFoundException(entityType, entityKey);
        }
    }

    // Through the context's record-access filter: hidden rows don't match. Record-scoped entities have a Guid "Id" key.
    private Task<bool> IsVisibleAsync<T>(Guid id, CancellationToken cancellationToken)
        where T : class =>
        db.Set<T>().AnyAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken);
}
