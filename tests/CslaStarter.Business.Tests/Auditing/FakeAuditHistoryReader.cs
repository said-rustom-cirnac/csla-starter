using CslaStarter.Core.Auditing;
using CslaStarter.Dal;

namespace CslaStarter.Business.Tests.Auditing;

public sealed class FakeAuditHistoryReader : IAuditHistoryReader
{
    public List<AuditEntry> Entries { get; } = [];

    /// <summary>Records the reader treats as hidden (record-level access): asking for them is "not found".</summary>
    public HashSet<string> Hidden { get; } = new(StringComparer.Ordinal);

    public AuditQuery? LastQuery { get; private set; }

    public Task<AuditPage> GetAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        LastQuery = query;
        if (Hidden.Contains(query.EntityKey))
        {
            throw new NotFoundException(query.EntityType, query.EntityKey);
        }

        var matching = Entries.Where(e => query.Property is null || e.Changes.Any(c => c.Property == query.Property)).ToList();
        return Task.FromResult(new AuditPage(matching.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(), matching.Count));
    }
}
