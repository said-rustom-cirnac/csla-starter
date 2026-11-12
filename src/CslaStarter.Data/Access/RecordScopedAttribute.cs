namespace CslaStarter.Data.Access;

/// <summary>
/// Rows of this entity are visible only to users allowed to see them: the context applies the named query filter
/// <see cref="AppDbContext.RecordAccessFilter"/> to every query. A hidden record reads as "not found".
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RecordScopedAttribute : Attribute;

/// <summary>
/// The only code allowed to call <c>IgnoreQueryFilters([AppDbContext.RecordAccessFilter])</c>. Use it for platform work
/// that must see every row (and say why on the attribute's target). An architecture test enforces this.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BypassRecordAccessAttribute : Attribute;
