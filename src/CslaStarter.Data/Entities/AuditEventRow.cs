namespace CslaStarter.Data.Entities;

/// <summary>One audited insert, update or delete of one row. Append-only.</summary>
public class AuditEventRow
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string EntityKey { get; set; } = string.Empty;

    public string RootType { get; set; } = string.Empty;

    public string RootKey { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public ICollection<AuditChangeRow> Changes { get; } = [];
}
