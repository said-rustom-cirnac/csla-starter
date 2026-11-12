namespace CslaStarter.Data.Entities;

/// <summary>One changed field of an audit event, with its old and new value. Append-only.</summary>
public class AuditChangeRow
{
    public long Id { get; set; }

    public long AuditEventId { get; set; }

    public string Property { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public bool IsMasked { get; set; }

    public string? TypeCodeOf { get; set; }
}
