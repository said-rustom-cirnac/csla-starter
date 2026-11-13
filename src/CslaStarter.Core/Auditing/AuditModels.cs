namespace CslaStarter.Core.Auditing;

/// <summary>
/// One page of a record's history. <c>HiddenFields</c> lists the fields the user may not read, as
/// <c>"EntityType.Property"</c>: their changes are left out, and so are entries with no other change.
/// </summary>
public sealed record AuditQuery(
    string EntityType, string EntityKey, string? Property = null, int Page = 1, int PageSize = 50, IReadOnlyCollection<string>? HiddenFields = null);

/// <summary>One changed field. <c>TypeCodeOf</c> names the *Type lookup the values are codes of, so the history can show descriptions.</summary>
public sealed record AuditChange(string Property, string? OldValue, string? NewValue, bool IsMasked, string? TypeCodeOf);

public sealed record AuditEntry(
    long Id,
    DateTimeOffset OccurredAt,
    Guid? UserId,
    string UserName,
    string Action,
    string EntityType,
    string EntityKey,
    IReadOnlyList<AuditChange> Changes);

public sealed record AuditPage(IReadOnlyList<AuditEntry> Entries, int TotalCount);
