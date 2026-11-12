using CslaStarter.Core.Auditing;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Auditing;

/// <summary>Reads one record's history (its own rows and its children's), newest first.</summary>
public sealed class AuditHistoryReader(AppDbContext db) : IAuditHistoryReader
{
    public async Task<AuditPage> GetAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, 200);

        var events = db.AuditEvents.AsNoTracking().Where(e =>
            (e.EntityType == query.EntityType && e.EntityKey == query.EntityKey)
            || (e.RootType == query.EntityType && e.RootKey == query.EntityKey));
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
                Changes = e.Changes.Where(c => query.Property == null || c.Property == query.Property).OrderBy(c => c.Id).ToList(),
            })
            .ToListAsync(cancellationToken);

        var entries = rows.Select(r => new AuditEntry(
                r.Event.Id, r.Event.OccurredAt, r.Event.UserId, r.Event.UserName, r.Event.Action, r.Event.EntityType, r.Event.EntityKey,
                r.Changes.Select(c => new AuditChange(c.Property, c.OldValue, c.NewValue, c.IsMasked, c.TypeCodeOf)).ToList()))
            .ToList();

        return new AuditPage(entries, total);
    }
}
