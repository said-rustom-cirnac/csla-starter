namespace CslaStarter.Business.Auditing;

/// <summary>
/// Marks the business type that guards the history of an audited entity type: who may open the record
/// (<c>GetObject</c>) and which of its fields they may read. One type per entity type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HistoryOfAttribute(string entityType) : Attribute
{
    public string EntityType { get; } = entityType;
}
