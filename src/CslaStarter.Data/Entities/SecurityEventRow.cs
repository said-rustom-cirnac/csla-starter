namespace CslaStarter.Data.Entities;

/// <summary>A security-relevant event (sign-in, access denied, role change...). Append-only.</summary>
public class SecurityEventRow
{
    public long Id { get; set; }

    public int Kind { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public string Detail { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }
}
