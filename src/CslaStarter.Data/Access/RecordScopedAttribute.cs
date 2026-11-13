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

/// <summary>
/// Adding or removing this row changes what the user in <see cref="UserIdProperty"/> may see or do, and so does changing
/// one of <see cref="WatchedProperties"/>. After commit that user gets an access-changed notice: their open screens
/// re-check access and their revalidation runs at once.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AccessGrantAttribute(string userIdProperty, params string[] watchedProperties) : Attribute
{
    public string UserIdProperty { get; } = userIdProperty;

    public IReadOnlyList<string> WatchedProperties { get; } = watchedProperties;
}
