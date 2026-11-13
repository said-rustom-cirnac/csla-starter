using CslaStarter.Data.Auditing;

namespace CslaStarter.Data.Entities;

/// <summary>
/// One user's unsaved edits of one record, encrypted. Not audited and never announced: it is nobody's data but the
/// user's until they save.
/// </summary>
[AuditIgnore]
public class DraftRow
{
    public Guid UserId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string EntityKey { get; set; } = string.Empty;

    /// <summary>The record's version when the draft was taken (a uint xmin, stored as bigint).</summary>
    public long? Version { get; set; }

#pragma warning disable CA1819 // An opaque encrypted blob.
    public byte[] Payload { get; set; } = [];
#pragma warning restore CA1819

    public DateTimeOffset SavedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
